#ifndef DK_COREMESHSOURCEGENERATENORMALS_INC
#define DK_COREMESHSOURCEGENERATENORMALS_INC

#include "Packages/nyc.scatter.depthkit.core/Runtime/Resources/Shaders/DataSource/DepthkitCoreMeshSourceCommon.cginc"

#ifndef DK_CORE_DISPATCH_CONSTANTS
#define DK_CORE_DISPATCH_CONSTANTS
static const uint BLOCK_THREAD_COUNT = BLOCK_SIZE * BLOCK_SIZE; //64 dispatch threads
static const uint TILE_SIZE = BLOCK_SIZE + TILE_BORDER * 2; //10x10 pixel sampling tile
static const uint TILE_PIXEL_COUNT = TILE_SIZE * TILE_SIZE; //100 pixel samples per group
#endif

#define NUM_HEX_SAMPLES 7

static const int2 hex_coord_offsets[NUM_HEX_SAMPLES] =
{
    int2(0, 0), // 0: center
    int2(-1, 1), // 1: upper left
    int2(-1, 0), // 2: left
    int2(1, -1), // 3: lower right
    int2(1, 0), // 4: right
    int2(0, -1), // 5: down
    int2(0, 1), // 6: up
};

#define NUM_HEX_TRIANGLES 6

static const uint hex_triangle_idx[NUM_HEX_TRIANGLES * 3] =
{
    1, 0, 2,
    0, 5, 2,
    0, 3, 5,
    4, 3, 0,
    6, 4, 0,
    6, 0, 1
};

#ifdef DK_CORE_SMOOTH_NORMALS
groupshared float3 g_vertexnormaldata[TILE_PIXEL_COUNT];
#else
struct NormalData {
    float depth;
    uint perspectiveIndex;
};
groupshared NormalData g_vertexnormaldata[TILE_PIXEL_COUNT];
#endif

void GenerateNormals(uint3 id, uint3 GroupId, uint3 GroupThreadId, uint GroupIndex)
{
#if defined(DK_CORE_NO_NORMALS) || defined(DK_CORE_DEPTHCAMERA_NORMALS)
#ifndef DK_VERTEX_BUFFER_READONLY
#ifdef DK_CORE_NO_NORMALS
    _VertexBuffer[DK_TO_VERTEX_BUFFER_INDEX(id)].normal = float3(0, 0, 0);
#endif
#ifdef DK_CORE_DEPTHCAMERA_NORMALS
    _VertexBuffer[DK_TO_VERTEX_BUFFER_INDEX(id)].normal = dkGetDepthCameraDirection(id.z) * -1.0f;
#endif
#endif
#else

    uint ind;
    const int2 tileSize = int2(TILE_SIZE, TILE_SIZE);

    //////////////////////////
    //LOAD VERTEX POSITION OR VERTEX DEPTH
    //////////////////////////

    const int2 tile_upperleft = int2(GroupId.xy * (int2)BLOCK_SIZE) - (int2)TILE_BORDER;
    float depth;
    [unroll]
    for (ind = GroupIndex; ind < TILE_PIXEL_COUNT; ind += BLOCK_THREAD_COUNT)
    {
        const int3 pixel = int3(tile_upperleft + (int2)toCoord(ind, tileSize), id.z);
        Vertex v = _VertexBuffer[DK_TO_VERTEX_BUFFER_INDEX(pixel)];
#ifdef DK_CORE_SMOOTH_NORMALS
        g_vertexnormaldata[ind] = v.position;
#else
        NormalData d;
        d.depth = dkWorldSpaceToDepthCameraObjectSpace(v.perspectiveIndex, v.position).z;
        d.perspectiveIndex = v.perspectiveIndex;
        g_vertexnormaldata[ind] = d;
#endif
    }

    GroupMemoryBarrierWithGroupSync();

    DK_CORE_CHECK_DISPATCH_VALID

    int2 tileCoord = GroupThreadId.xy + int2(TILE_BORDER, TILE_BORDER);

#ifdef DK_CORE_ADJUSTABLE_NORMALS
    //////////////////////////
    //HEIGHT MAP NORMALS
    //////////////////////////

    float3 normal = 0;
    float3 dir = float3(0, 0, 0);
    uint perspectiveIndex = 0;

    {
        uint x1_idx = toIndex(tileCoord + int2(1, 0), tileSize);
        uint x2_idx = toIndex(tileCoord + int2(-1, 0), tileSize);
        uint y1_idx = toIndex(tileCoord + int2(0, 1), tileSize);
        uint y2_idx = toIndex(tileCoord + int2(0, -1), tileSize);

        NormalData x1 = g_vertexnormaldata[x1_idx];
        NormalData x2 = g_vertexnormaldata[x2_idx];
        NormalData y1 = g_vertexnormaldata[y1_idx];
        NormalData y2 = g_vertexnormaldata[y2_idx];

        perspectiveIndex = x1.perspectiveIndex;

        float dzdx = (x1.depth * _NormalHeight - x2.depth * _NormalHeight) / 2.0;
        float dzdy = (y1.depth * _NormalHeight - y2.depth * _NormalHeight) / 2.0;

        dir += float3(dzdx, dzdy, -1.0);
    }

#ifdef DK_CORE_ADJUSTABLE_NORMALS_SMOOTHER
    {
        uint x1_idx = toIndex(tileCoord + int2(1, 1), tileSize);
        uint x2_idx = toIndex(tileCoord + int2(-1, -1), tileSize);
        uint y1_idx = toIndex(tileCoord + int2(-1, 1), tileSize);
        uint y2_idx = toIndex(tileCoord + int2(1, -1), tileSize);

        NormalData x1 = g_vertexnormaldata[x1_idx];
        NormalData x2 = g_vertexnormaldata[x2_idx];
        NormalData y1 = g_vertexnormaldata[y1_idx];
        NormalData y2 = g_vertexnormaldata[y2_idx];

        float dzdx = (x1.depth * _NormalHeight - x2.depth * _NormalHeight) / 2.0;
        float dzdy = (y1.depth * _NormalHeight - y2.depth * _NormalHeight) / 2.0;

        dir += float3(dzdx, dzdy, -1.0);
    }
#endif
#ifndef DK_VERTEX_BUFFER_READONLY
    _VertexBuffer[DK_TO_VERTEX_BUFFER_INDEX(id)].normal = dkDepthCameraObjectSpaceDirToWorldSpaceDir(perspectiveIndex, normalize(dir));
#endif
#endif


#ifdef DK_CORE_SMOOTH_NORMALS

    //////////////////////////
    //HEX NORMALS
    //////////////////////////

    float3 normal = 0;
    [unroll]
    for (uint tri_ind = 0; tri_ind < NUM_HEX_TRIANGLES; ++tri_ind)
    {
        float3 p1 = g_vertexnormaldata[toIndex(tileCoord + hex_coord_offsets[hex_triangle_idx[tri_ind * 3]], tileSize)];
        float3 p2 = g_vertexnormaldata[toIndex(tileCoord + hex_coord_offsets[hex_triangle_idx[(tri_ind * 3) + 1]], tileSize)];
        float3 p3 = g_vertexnormaldata[toIndex(tileCoord + hex_coord_offsets[hex_triangle_idx[(tri_ind * 3) + 2]], tileSize)];
        normal += normalize(cross(p2 - p1, p3 - p1));
    }
#ifndef DK_VERTEX_BUFFER_READONLY
    _VertexBuffer[DK_TO_VERTEX_BUFFER_INDEX(id)].normal = normalize(normal / 6.0);
#endif
#endif
#endif
}

[numthreads(BLOCK_SIZE, BLOCK_SIZE, 1)]
void KGenerateNormals(uint3 id : SV_DispatchThreadID, uint3 GroupId : SV_GroupID, uint3 GroupThreadId : SV_GroupThreadID, uint GroupIndex : SV_GroupIndex)
{
    GenerateNormals(id, GroupId, GroupThreadId, GroupIndex);
}

[numthreads(BLOCK_SIZE, BLOCK_SIZE, 1)]
void KGenerateNormalsAdjustable(uint3 id : SV_DispatchThreadID, uint3 GroupId : SV_GroupID, uint3 GroupThreadId : SV_GroupThreadID, uint GroupIndex : SV_GroupIndex)
{
    GenerateNormals(id, GroupId, GroupThreadId, GroupIndex);
}

[numthreads(BLOCK_SIZE, BLOCK_SIZE, 1)]
void KGenerateNormalsAdjustableSmoother(uint3 id : SV_DispatchThreadID, uint3 GroupId : SV_GroupID, uint3 GroupThreadId : SV_GroupThreadID, uint GroupIndex : SV_GroupIndex)
{
    GenerateNormals(id, GroupId, GroupThreadId, GroupIndex);
}

[numthreads(BLOCK_SIZE, BLOCK_SIZE, 1)]
void KGenerateNormalsNone(uint3 id : SV_DispatchThreadID, uint3 GroupId : SV_GroupID, uint3 GroupThreadId : SV_GroupThreadID, uint GroupIndex : SV_GroupIndex)
{
    GenerateNormals(id, GroupId, GroupThreadId, GroupIndex);
}

[numthreads(BLOCK_SIZE, BLOCK_SIZE, 1)]
void KGenerateNormalsDepthCamera(uint3 id : SV_DispatchThreadID, uint3 GroupId : SV_GroupID, uint3 GroupThreadId : SV_GroupThreadID, uint GroupIndex : SV_GroupIndex)
{
    GenerateNormals(id, GroupId, GroupThreadId, GroupIndex);
}

#endif