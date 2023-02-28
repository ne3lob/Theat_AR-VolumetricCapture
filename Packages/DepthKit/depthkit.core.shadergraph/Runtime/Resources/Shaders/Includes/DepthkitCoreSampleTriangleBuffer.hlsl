#ifndef DEPTHKIT_SAMPLETRIANGLEBUFFER_HLSL_INCLUDE
#define DEPTHKIT_SAMPLETRIANGLEBUFFER_HLSL_INCLUDE

#include "Packages/nyc.scatter.depthkit.core/Runtime/Resources/Shaders/Includes/SampleCoreTriangles.cginc"

void DepthkitCoreSampleTriangleBuffer_float(in float2 triangleUV, out float2 perspectiveUV, out float3 position, out float3 normal, out uint perspective)
{
    Vertex vert = dkSampleTriangleBuffer((uint)triangleUV.x, (uint)triangleUV.y);
    perspective = vert.perspectiveIndex;
    position = vert.position;
    normal = vert.normal;
    perspectiveUV = vert.uv.xy;
}

#endif