#ifndef _DK_CORE_VERTEX_TYPE_CGINC
#define _DK_CORE_VERTEX_TYPE_CGINC

//Vertices are in Object Space
struct Vertex
{
    float4 uv; //[xy = perspective, zw = packed]
    float3 position;
    float3 normal;
    uint perspectiveIndex;
    uint validFlag;
};

Vertex newVertex()
{
    Vertex v;
    v.uv = float4(0, 0, 0, 0);
    v.perspectiveIndex = 0;
    v.position = float3(0, 0, 0);
    v.normal = float3(0, 0, 0);
    v.validFlag = 0;
    return v;
}

#endif