#ifndef DEPTHKIT_STUDIOSAMPLETRIANGLEBUFFER_HLSL_INCLUDE
#define DEPTHKIT_STUDIOSAMPLETRIANGLEBUFFER_HLSL_INCLUDE

#define DK_CORE_PACKED_TRIANGLE
#include "Packages/nyc.scatter.depthkit.core/Runtime/Resources/Shaders/Includes/SampleCoreTriangles.cginc"

void DepthkitStudioSampleTriangleBuffer_float(in float2 triangleUV, out float3 position, out float3 normal)
{
    Vertex vert = dkSampleTriangleBuffer((uint)triangleUV.x, (uint)triangleUV.y);
    position = vert.position;
    normal = vert.normal;
}

#endif