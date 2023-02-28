#ifndef DK_COREMESHSOURCEGENERATETRIANGLES_INC
#define DK_COREMESHSOURCEGENERATETRIANGLES_INC

#include "Packages/nyc.scatter.depthkit.core/Runtime/Resources/Shaders/DataSource/DepthkitCoreMeshSourceCommon.cginc"

#ifndef DK_CORE_CHECK_DISPATCH_VALID
#define DK_CORE_CHECK_DISPATCH_VALID
#endif

#ifndef DK_CORE_DISPATCH_CONSTANTS
#define DK_CORE_DISPATCH_CONSTANTS

static const uint BLOCK_THREAD_COUNT = BLOCK_SIZE * BLOCK_SIZE;
static const uint TILE_BORDER = 1;

static const uint TILE_SIZE = BLOCK_SIZE + TILE_BORDER * 2; 
static const uint TILE_PIXEL_COUNT = TILE_SIZE * TILE_SIZE;

#endif

bool dkCheckQuadTriangleValid(uint quadMask, uint v1, uint v2, uint v3, out bool isEdge)
{
    isEdge = false;
    bool vertexOutOfBounds = ((v1 & DK_VERTEX_OUT_OF_BOUNDS) > 0) || ((v2 & DK_VERTEX_OUT_OF_BOUNDS) > 0) || ((v3 & DK_VERTEX_OUT_OF_BOUNDS) > 0);
    if (vertexOutOfBounds)
        return false;
#ifdef DK_CORE_USE_EDGEMASK
    isEdge = ((quadMask & DK_VERTEX_POINT_MAX_THRESHOLD_VALID) == 0) || ((v1 & DK_VERTEX_LINEAR_MAX_THRESHOLD_VALID) == 0) || ((v2 & DK_VERTEX_LINEAR_MAX_THRESHOLD_VALID) == 0) || ((v3 & DK_VERTEX_LINEAR_MAX_THRESHOLD_VALID) == 0);
    return ((quadMask & DK_VERTEX_POINT_MIN_THRESHOLD_VALID) > 0) || ((v1 & DK_VERTEX_LINEAR_MIN_THRESHOLD_VALID) > 0) || ((v2 & DK_VERTEX_LINEAR_MIN_THRESHOLD_VALID) > 0) || ((v3 & DK_VERTEX_LINEAR_MIN_THRESHOLD_VALID) > 0);
#else
    uint v1v = v1 & DK_VERTEX_VALID;
    uint v2v = v2 & DK_VERTEX_VALID;
    uint v3v = v3 & DK_VERTEX_VALID;

    uint t = v1v + v2v + v3v;

    switch (t)
    {
        case 3: //all valid
            return true;
        case 0: //all invalid
            return false;
        default: //an edge
            isEdge = true;
            return true;
    }
#endif
}

Triangle setupTriangle(int ind1, int ind2, int ind3)
{
    Triangle t;
    t.vertex[0] = ind1;
    t.vertex[1] = ind2;
    t.vertex[2] = ind3;
    return t;
}

void writeTriangle(in Vertex v1, in Vertex v2, in Vertex v3, in int v1_idx, in int v2_idx, in int v3_idx, in bool isEdge)
{
    Triangle o = setupTriangle(v1_idx, v2_idx, v3_idx);
    o.perspectiveIndex = v1.perspectiveIndex;
#ifdef DK_USE_EDGE_TRIANGLES_BUFFER
    if (isEdge)
    {
        _EdgeTriangleBuffer.Append(o);
    }
    else
    {
        _TriangleBuffer.Append(o);
    }
#else
    _TriangleBuffer.Append(o);
#endif
}

void writeTriangles(in Vertex tl, in Vertex bl, in Vertex tr, in Vertex br, in int tl_idx, in int bl_idx, in int tr_idx, in int br_idx)
{
    bool isEdge;
    if (dkCheckQuadTriangleValid(tl.validFlag, bl.validFlag, br.validFlag, tl.validFlag, isEdge))
    {
        writeTriangle(bl, br, tl, bl_idx, br_idx, tl_idx, isEdge);
    }
    if (dkCheckQuadTriangleValid(tl.validFlag, br.validFlag, tr.validFlag, tl.validFlag, isEdge))
    {
        writeTriangle(br, tr, tl, br_idx, tr_idx, tl_idx, isEdge);
    }
}

groupshared Vertex g_trianglevertexdata[TILE_PIXEL_COUNT];

void GenerateTriangles(uint3 id, uint3 GroupId, uint3 GroupThreadId, uint GroupIndex)
{
    uint ind;
    const int2 tileSize = int2(TILE_SIZE, TILE_SIZE);

    //////////////////////////
    //LOAD VERTEX DATA
    //////////////////////////

    const int2 tile_upperleft = int2(GroupId.xy * (int2)BLOCK_SIZE) - (int2)TILE_BORDER;
    float depth;
    [unroll]
    for (ind = GroupIndex; ind < TILE_PIXEL_COUNT; ind += BLOCK_THREAD_COUNT)
    {
        const int3 pixel = int3(tile_upperleft + (int2)toCoord(ind, tileSize), id.z);
        g_trianglevertexdata[ind] = _VertexBuffer[DK_TO_VERTEX_BUFFER_INDEX(pixel)];
    }

    GroupMemoryBarrierWithGroupSync();

    DK_CORE_CHECK_DISPATCH_VALID

    //////////////////////
    // OUTPUT TRIANGLES
    //////////////////////

    uint2 offset = GroupThreadId.xy + TILE_BORDER;
    uint2 vertex = offset + tile_upperleft;

    //make sure there's a vertex here
    if (vertex.x < 0 || vertex.y < 0 || vertex.x >= (uint)_PerspectiveTextureSize.x || vertex.y >= (uint)_PerspectiveTextureSize.y)
        return;

    int tl_idx = toIndex(offset + vert_coords[0], tileSize);
    int bl_idx = toIndex(offset + vert_coords[1], tileSize);
    int tr_idx = toIndex(offset + vert_coords[2], tileSize);
    int br_idx = toIndex(offset + vert_coords[3], tileSize);

    //get triangle shared vertices and try to output the two triangles for this group thread
    Vertex tl = g_trianglevertexdata[tl_idx];
    Vertex bl = g_trianglevertexdata[bl_idx];
    Vertex tr = g_trianglevertexdata[tr_idx];
    Vertex br = g_trianglevertexdata[br_idx];

    tl_idx = DK_TO_VERTEX_BUFFER_INDEX(int3(vertex + vert_coords[0], id.z));
    bl_idx = DK_TO_VERTEX_BUFFER_INDEX(int3(vertex + vert_coords[1], id.z));
    tr_idx = DK_TO_VERTEX_BUFFER_INDEX(int3(vertex + vert_coords[2], id.z));
    br_idx = DK_TO_VERTEX_BUFFER_INDEX(int3(vertex + vert_coords[3], id.z));

    writeTriangles(tl, bl, tr, br, tl_idx, bl_idx, tr_idx, br_idx);
}

#endif