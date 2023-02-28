
#ifndef DEPTHKIT_FRAGMENT_HLSL_INCLUDED
#define DEPTHKIT_FRAGMENT_HLSL_INCLUDED

#include "Packages/nyc.scatter.depthkit.studio/Runtime/Resources/Shaders/Includes/DepthkitStudio.cginc"

void DepthkitStudioFragment_float(in float3 object_position, in float3 object_normal, in float3 view_direction, out float3 color)
{
    color = dkSampleColorViewWeightedReprojection(view_direction, object_position, object_normal);
}

#endif