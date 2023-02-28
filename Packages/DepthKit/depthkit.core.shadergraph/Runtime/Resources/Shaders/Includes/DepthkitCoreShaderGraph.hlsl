
#ifndef DEPTHKIT_FRAGMENT_HLSL_INCLUDED
#define DEPTHKIT_FRAGMENT_HLSL_INCLUDED

#include "Packages/nyc.scatter.depthkit.core/Runtime/Resources/Shaders/Includes/Depthkit.cginc"
#include "Packages/nyc.scatter.depthkit.core/Runtime/Resources/Shaders/Includes/SampleEdgeMask.cginc"

DK_EDGEMASK_UNIFORMS

void DepthkitCoreFragment_float(in float3 screenSpacePosition, in float3 objectPosition, out float3 color, out float alpha, out float2 uv, out float alphaclip)
{
    float2 colorUV;
    float2 depthUV;
    float3 viewspace;
    dkWorldToPerspectiveUV(0, objectPosition, uv, depthUV, colorUV, viewspace);
    float depth = dkSampleDepth(depthUV, 0, uv);
    bool valid = dkValidateNormalizedDepth(0, depth);
    
#ifdef DK_USE_DEBUG_COLOR
    color = dkGetDebugCameraColor(0);
#else
    color = dkSampleColor(colorUV);
#endif
    
    alpha = lerp(-1.f, DK_SAMPLE_EDGEMASK(uv, 0, screenSpacePosition), valid);
    alphaclip = DK_CLIP_THRESHOLD(0);
    
#ifdef DK_DEBUG_EDGEMASK
    color = float3(alpha * dkGetDebugCameraColor(0));
#ifdef DK_USE_EDGEMASK
    float2 minMaxMask = DK_SAMPLE_DOWNSAMPLED_EDGEMASK(uv, 0);
    if (alpha < 0.0)
    { 
        color = float3(0.3, 0.3, 0.3);
    }
    if (alpha == 0.0) //completely invalid
    { 
        color = float3(0.05, 0.05, 0.05);
    }
    // dim if is edge
    if ((1.0f - minMaxMask.y) < DK_CLIP_THRESHOLD(0))
    {
        color *= .5;
    }
#else
        if (alpha < DK_ALPHA_CLIP_THRESHOLD) { color = float3(0.3f, 0.3f, 0.3f); }
#endif
    alpha = 1.0f;
#endif

}

#endif