/************************************************************************************

Depthkit Unity SDK License v1
Copyright 2016-2019 Scatter All Rights reserved.  

Licensed under the Scatter Software Development Kit License Agreement (the "License"); 
you may not use this SDK except in compliance with the License, 
which is provided at the time of installation or download, 
or which otherwise accompanies this software in either electronic or hard copy form.  

You may obtain a copy of the License at http://www.depthkit.tv/license-agreement-v1

Unless required by applicable law or agreed to in writing, 
the SDK distributed under the License is distributed on an "AS IS" BASIS, 
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
See the License for the specific language governing permissions and limitations under the License. 

************************************************************************************/

#ifndef _DEPTHKIT_STUDIO_CGINC
#define _DEPTHKIT_STUDIO_CGINC

#include "Packages/nyc.scatter.depthkit.core/Runtime/Resources/Shaders/Includes/Depthkit.cginc"
#include "Packages/nyc.scatter.depthkit.core/Runtime/Resources/Shaders/Includes/SampleEdgeMask.cginc"
#include "Packages/nyc.scatter.depthkit.studio/Runtime/Resources/Shaders/Includes/DepthkitStudioUniforms.cginc"

DK_EDGEMASK_UNIFORMS

#ifdef DK_UNTEXTURED_FRAGMENT_INFER
float4 GetCamContribution(uint perspectiveIndex, float3 surfaceToEyeDir, float3 pos3d, float3 normal, inout float4 fallbackColor1, inout float4 fallbackColor2)
#else
float4 GetCamContribution(uint perspectiveIndex, float3 surfaceToEyeDir, float3 pos3d, float3 normal)
#endif
{
    //ignore cameras from backfacing directions
    float3 sensorPosition = dkGetDepthCameraPosition(perspectiveIndex);
    float3 surfaceToSensorDir = normalize(sensorPosition - pos3d);
    float normalDotSensor = dot(surfaceToSensorDir, normal);
    float normWeight = step(0.0f, normalDotSensor);

    // back-project into NDC camera space then into texture space for sampling depth and colour frames
    float2 perspectiveUV, colorUV, depthUV;
    float3 depthViewSpacePos;
    dkWorldToPerspectiveUV(perspectiveIndex, pos3d, perspectiveUV, depthUV, colorUV, depthViewSpacePos);

    float2 inBounds = step(float2(0.0f, 0.0f), perspectiveUV.xy) * step(perspectiveUV.xy, float2(1.0f, 1.0f));
    float valid = inBounds.x * inBounds.y * (float)(_PerspectiveColorBlending[perspectiveIndex].enablePerspective);

    // calculate weights
    float viewWeight = remap(dot(surfaceToEyeDir, surfaceToSensorDir), -1.0f, 1.0f, 0.0f, 1.0f);
    viewWeight = saturate(pow(viewWeight, lerp(0.0f, _GlobalViewDependentColorBlendWeight, _PerspectiveColorBlending[perspectiveIndex].viewWeightPowerContribution)));
    // normal based weighting
    normWeight *= pow(max(normalDotSensor, 0), _SurfaceNormalColorBlendingPower);

    float mixedWeight = valid * viewWeight * normWeight;

#ifndef DK_UNTEXTURED_FRAGMENT_INFER
    // early out before any texture sampling if possible
    if (mixedWeight <= 0) return float4(0, 0, 0, 0);
#endif

#if defined(DK_USE_EDGEMASK)
    float edgeMaskValue = _PerspectiveColorBlending[perspectiveIndex].edgeMaskStrength * DK_SAMPLE_EDGEMASK(perspectiveUV, perspectiveIndex, 0);
    float edgeBlendMask = smoothstep(_PerspectiveColorBlending[perspectiveIndex].edgeMaskBlendEdgeMin, _PerspectiveColorBlending[perspectiveIndex].edgeMaskBlendEdgeMax, edgeMaskValue);
    float edgeWeight = lerp(1, edgeBlendMask, _PerspectiveColorBlending[perspectiveIndex].edgeMaskEnabled);
    mixedWeight *= edgeWeight;
#endif

    //sample depth and color w/ bilinear interpolation
    float depth = dkSampleDepth(depthUV, perspectiveIndex, perspectiveUV);

    valid *= (float)dkValidateNormalizedDepth(perspectiveIndex, depth);
    mixedWeight *= valid;

    float3 unprojected = dkPerspectiveUVToWorld(perspectiveIndex, perspectiveUV, depth).xyz;

    float disparity = distance(unprojected, pos3d);

    //interpolate over the disparity threshold
    mixedWeight *= smoothstep(_PerViewDisparityThreshold + _PerViewDisparityBlendWidth, _PerViewDisparityThreshold - _PerViewDisparityBlendWidth, disparity);

#if defined(DK_USE_DEBUG_COLOR)
    float3 newColor = dkGetDebugCameraColor(perspectiveIndex);
#elif defined(DK_USE_EDGEMASK) && defined(DK_DEBUG_EDGEMASK)
    float3 newColor = dkGetDebugCameraColor(perspectiveIndex);
    mixedWeight = edgeWeight * valid;
#else
    float3 newColor = dkSampleColor(colorUV);
#endif

#ifdef DK_UNTEXTURED_FRAGMENT_INFER
    // Last level fallback is view dependence and validity only
    float fallbackWeight = valid * viewWeight;
    fallbackColor2 += float4(newColor * fallbackWeight, fallbackWeight);
    // First level fallback also includes normal weight to prevent texture from cameras that can't see the surface at all
    fallbackWeight *= normWeight;
    fallbackColor1 += float4(newColor * fallbackWeight, fallbackWeight);
#endif

    return float4(newColor * mixedWeight, mixedWeight);
}

float3 dkSampleColorViewWeightedReprojection(float3 surfaceToEyeDir, float3 objectPosition, float3 objectNormal)
{
    float4 accumulatedColor = float4(0, 0, 0, 0);

#ifdef DK_UNTEXTURED_FRAGMENT_INFER
    float4 fallbackColor1 = float4(0, 0, 0, 0);
    float4 fallbackColor2 = float4(0, 0, 0, 0);
#endif

    for (uint perspectiveIndex = 0; perspectiveIndex < (uint) _PerspectivesCount; perspectiveIndex++)
    {
#ifdef DK_UNTEXTURED_FRAGMENT_INFER
        accumulatedColor += GetCamContribution(perspectiveIndex, surfaceToEyeDir, objectPosition, objectNormal, fallbackColor1, fallbackColor2);
#else
        accumulatedColor += GetCamContribution(perspectiveIndex, surfaceToEyeDir, objectPosition, objectNormal);
#endif
    }

    if (accumulatedColor.w > 0.0f)
    {
        accumulatedColor.rgb /= accumulatedColor.w;
    }
#ifdef DK_UNTEXTURED_FRAGMENT_INFER
    else if (fallbackColor1.w > 0.0f)
    {
        accumulatedColor.rgb = fallbackColor1.rgb / fallbackColor1.w;
    }
    else if (fallbackColor2.w > 0.0f)
    {
        accumulatedColor.rgb = fallbackColor2.rgb / fallbackColor2.w;
    }
#endif
    else
    {
#ifdef DK_UNTEXTURED_FRAGMENT_CLIP
        discard;
#endif
#ifdef DK_UNTEXTURED_FRAGMENT_COLORIZE
        accumulatedColor.rgb = _UntexturedFragDefaultColor;
#endif
    }

    return accumulatedColor.rgb;
}

#endif // _DEPTHKIT_STUDIO_CGINC
