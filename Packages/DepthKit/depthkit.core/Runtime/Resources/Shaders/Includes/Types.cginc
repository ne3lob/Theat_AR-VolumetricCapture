#ifndef _DEPTHKIT_TYPES_CGINC
#define _DEPTHKIT_TYPES_CGINC

struct PerspectiveData
{
    float2 depthImageSize;
    float2 depthPrincipalPoint;
    float2 depthFocalLength;
    float farClip;
    float nearClip;
    float4x4 extrinsics;
    float4x4 extrinsicsInverse;
    float4 crop;
    float clipEpsilon;
    float3 cameraPosition;
    float3 cameraNormal;
    float pad;
};

#endif