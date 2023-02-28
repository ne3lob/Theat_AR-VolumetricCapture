#ifndef DK_COREMESHSOURCEGENERATEVERTS_INC
#define DK_COREMESHSOURCEGENERATEVERTS_INC

#include "Packages/nyc.scatter.depthkit.core/Runtime/Resources/Shaders/DataSource/DepthkitCoreMeshSourceCommon.cginc"

static const uint BLOCK_WIDTH = BLOCK_SIZE * BLOCK_SIZE; // 64x1x1 dispatch tile
static const uint TILE_WIDTH = BLOCK_WIDTH + TILE_BORDER * 2;

struct VertexData {
    float4 uv; //[xy = perspective uv, zw = packed uv]
    uint perspectiveIndex;
    int validFlag;
    float depth;
};

VertexData getVertexData(int3 pixel)
{
    VertexData v;
    // Note: we add 1 to the _PerspectiveTextureSize here to ensure proper UV calculation as we have enough verts for 1 quad per pixel
    float2 dispatchUV = saturate(((float2) pixel.xy) / (_PerspectiveTextureSize + 1.0f));
    v.uv.zw = DK_CORE_DATA_SAMPLE_FN(dispatchUV, pixel)

    if (vertexOutOfBounds(pixel))
    {
        v.validFlag = DK_VERTEX_OUT_OF_BOUNDS | DK_VERTEX_INVALID;
        v.uv = 0;
        v.perspectiveIndex = 0;
        v.depth = 0;
    }
    else
    {
        float2 depthUV;
        float depth;
        uint perspectiveIndex;
        float2 perspUV;
        bool valid = dkLoadDepthNormalized(v.uv.zw, depth, depthUV, perspUV, perspectiveIndex);
        v.uv.xy = perspUV;
        v.perspectiveIndex = perspectiveIndex;

#ifdef DK_CORE_USE_EDGEMASK
        uint4 maskDims;
        _MaskTexture.GetDimensions(0, maskDims.x, maskDims.y, maskDims.z, maskDims.w);

        // Note: we add 1 to the maskDims.xy here to ensure proper UV calculation as we have enough verts for 1 quad per pixel
        float2 maskLinearSample = _MaskTexture.SampleLevel(_Mask_LinearClamp, float3(((float2)pixel.xy) / ((float2)maskDims.xy + 1.0f), DK_SLICE_OFFSET(pixel.z)), 0).rg;
        float2 maskPointSample = _MaskTexture.Load(float4(pixel.xy, DK_SLICE_OFFSET(pixel.z), 0)).rg;

        bool minLinearValid = (1.0f - maskLinearSample.r) >= _PerspectiveToSlice[perspectiveIndex].y;
        bool maxLinearValid = (1.0f - max(maskLinearSample.r, maskLinearSample.g)) >= _PerspectiveToSlice[perspectiveIndex].y + _PerspectiveToSlice[perspectiveIndex].z;
        bool minPointValid = (1.0f - maskPointSample.r) >= _PerspectiveToSlice[perspectiveIndex].y;
        bool maxPointValid = (1.0f - max(maskPointSample.r, maskPointSample.g)) >= _PerspectiveToSlice[perspectiveIndex].y + _PerspectiveToSlice[perspectiveIndex].z;
#else
        bool minLinearValid = false;
        bool maxLinearValid = false;
        bool minPointValid = false;
        bool maxPointValid = false;
#endif
        v.depth = depth;

        v.validFlag = valid ? DK_VERTEX_VALID : DK_VERTEX_INVALID;
        v.validFlag |= minLinearValid ? DK_VERTEX_LINEAR_MIN_THRESHOLD_VALID : DK_VERTEX_INVALID;
        v.validFlag |= maxLinearValid ? DK_VERTEX_LINEAR_MAX_THRESHOLD_VALID : DK_VERTEX_INVALID;
        v.validFlag |= minPointValid ? DK_VERTEX_POINT_MIN_THRESHOLD_VALID : DK_VERTEX_INVALID;
        v.validFlag |= maxPointValid ? DK_VERTEX_POINT_MAX_THRESHOLD_VALID : DK_VERTEX_INVALID;
    }

    return v;
}
groupshared VertexData g_vertexdata[TILE_WIDTH];

float flattenFlangeFilter(int offset)
{
    float maxDepth = 0.f;
    [unroll]
    for (int kernel_ind = -TILE_BORDER; kernel_ind <= TILE_BORDER; kernel_ind++)
    {
        maxDepth = max(g_vertexdata[kernel_ind + offset].depth, maxDepth);
    }
    float depth = g_vertexdata[offset].depth;
    return lerp(maxDepth, depth, depth > 0);
}

void GenerateVertices_Horizontal(uint3 id, uint3 GroupId, uint3 GroupThreadId, uint GroupIndex)
{
    //////////////////////////
    //SAMPLE DEPTH AND GENERATE VERTEX
    //////////////////////////

    uint ind;

    const int2 tile_upperleft = int2(GroupId.xy) * int2(BLOCK_WIDTH, 1) - int2(TILE_BORDER, 0);

    [unroll]
    for (ind = GroupIndex; ind < TILE_WIDTH; ind += BLOCK_WIDTH)
    {
        const int3 pixel = int3(tile_upperleft + int2(ind, 0), id.z);
        VertexData d = getVertexData(pixel);
        g_vertexdata[ind] = d;
    }

    GroupMemoryBarrierWithGroupSync();

    DK_CORE_CHECK_DISPATCH_VALID
    
    int sampleOffset = GroupThreadId.x + TILE_BORDER;
    const int3 vertex = int3(tile_upperleft.x + sampleOffset, tile_upperleft.y, id.z);
    
    //make sure there's a vertex here
    if (vertex.x < 0 || vertex.y < 0 || vertex.x >= _LatticeSize.x || vertex.y >= _LatticeSize.y)
        return;
    
    //////////////////////////
    //APPLY FLANGE FLATTEN FILTER TO DEPTH
    //////////////////////////
    
    VertexData d = g_vertexdata[sampleOffset];

    Vertex v = newVertex();
    v.perspectiveIndex = d.perspectiveIndex;
    v.uv = d.uv;
    v.validFlag = d.validFlag;

    // Note: Here we temporarily store the modified depth in the normal.
    // We cannot use any other components as they either already
    // have valid data or will be overwritten in the Vertical pass
    v.normal.x = flattenFlangeFilter(sampleOffset);

#ifndef DK_VERTEX_BUFFER_READONLY
    _VertexBuffer[DK_TO_VERTEX_BUFFER_INDEX(vertex)] = v;
#endif

}

void GenerateVertices_Vertical(uint3 id, uint3 GroupId, uint3 GroupThreadId, uint GroupIndex)
{
    uint ind;
    const int2 tile_upperleft = int2(GroupId.xy) * int2(1, BLOCK_WIDTH) - int2(0, TILE_BORDER);

    //////////////////////////
    //LOAD VERTEX DATA
    //////////////////////////

    [unroll]
    for (ind = GroupIndex; ind < TILE_WIDTH; ind += BLOCK_WIDTH)
    {
        const int3 vertex = int3(tile_upperleft + int2(0, ind), id.z);

        Vertex v = _VertexBuffer[DK_TO_VERTEX_BUFFER_INDEX(vertex)];
        // needed to compute the vertex position
        VertexData d;
        d.uv = v.uv;
        d.perspectiveIndex = v.perspectiveIndex;
        d.depth = v.normal.x;
        g_vertexdata[ind] = d;
    }

    GroupMemoryBarrierWithGroupSync();

    DK_CORE_CHECK_DISPATCH_VALID

    int sampleOffset = GroupThreadId.y + TILE_BORDER;
    const int3 vertex = int3(tile_upperleft.x, tile_upperleft.y + sampleOffset, id.z);
    
    //make sure there's a vertex here
    if (vertex.x < 0 || vertex.y < 0 || vertex.x >= _LatticeSize.x || vertex.y >= _LatticeSize.y)
        return;


    //////////////////////////
    //APPLY FLANGE FLATTEN FILTER TO DEPTH
    //////////////////////////

    VertexData d = g_vertexdata[sampleOffset];
    float updatedDepth = flattenFlangeFilter(sampleOffset);

    float3 position = dkPerspectiveUVToWorld(d.perspectiveIndex, d.uv.xy, updatedDepth).xyz;

#ifndef DK_VERTEX_BUFFER_READONLY
    _VertexBuffer[DK_TO_VERTEX_BUFFER_INDEX(vertex)].position = position;
#endif
}

[numthreads(BLOCK_SIZE * BLOCK_SIZE, 1, 1)]
void KGenerateVertices_Horizontal(uint3 id : SV_DispatchThreadID, uint3 GroupId : SV_GroupID, uint3 GroupThreadId : SV_GroupThreadID, uint GroupIndex : SV_GroupIndex)
{
    GenerateVertices_Horizontal(id, GroupId, GroupThreadId, GroupIndex);
}

[numthreads(BLOCK_SIZE * BLOCK_SIZE, 1, 1)]
void KGenerateVertices_HorizontalWEdgeMask(uint3 id : SV_DispatchThreadID, uint3 GroupId : SV_GroupID, uint3 GroupThreadId : SV_GroupThreadID, uint GroupIndex : SV_GroupIndex)
{
    GenerateVertices_Horizontal(id, GroupId, GroupThreadId, GroupIndex);
}

[numthreads(1, BLOCK_SIZE * BLOCK_SIZE, 1)]
void KGenerateVertices_Vertical(uint3 id : SV_DispatchThreadID, uint3 GroupId : SV_GroupID, uint3 GroupThreadId : SV_GroupThreadID, uint GroupIndex : SV_GroupIndex)
{
    GenerateVertices_Vertical(id, GroupId, GroupThreadId, GroupIndex);
}

[numthreads(1, BLOCK_SIZE * BLOCK_SIZE, 1)]
void KGenerateVertices_VerticalWEdgeMask(uint3 id : SV_DispatchThreadID, uint3 GroupId : SV_GroupID, uint3 GroupThreadId : SV_GroupThreadID, uint GroupIndex : SV_GroupIndex)
{
    GenerateVertices_Vertical(id, GroupId, GroupThreadId, GroupIndex);
}

#endif