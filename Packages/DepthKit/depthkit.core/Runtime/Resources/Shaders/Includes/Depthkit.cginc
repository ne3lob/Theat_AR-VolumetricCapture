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

#ifndef _DEPTHKIT_CGINC
#define _DEPTHKIT_CGINC

#include "Packages/nyc.scatter.depthkit.core/Runtime/Resources/Shaders/Includes/Defines.cginc"
#include "Packages/nyc.scatter.depthkit.core/Runtime/Resources/Shaders/Includes/Types.cginc"
#ifndef DK_USE_BUILT_IN_COLOR_CONVERSION
#include "Packages/nyc.scatter.depthkit.core/Runtime/Resources/Shaders/Includes/ColorCorrection.cginc"
#endif
#include "Packages/nyc.scatter.depthkit.core/Runtime/Resources/Shaders/Includes/Utils.cginc"
#include "Packages/nyc.scatter.depthkit.core/Runtime/Resources/Shaders/Includes/DebugCameraColors.cginc"
#include "Packages/nyc.scatter.depthkit.core/Runtime/Resources/Shaders/Includes/DepthkitCoreUniforms.cginc"

float3 dkGetDebugCameraColor(uint perspectiveIndex)
{
    return dkDebugCameraColors[perspectiveIndex];
}

uint2 dkGetPerspectiveCoordFromPackedUV(float2 packedUV)
{
    // clamp values to _PerspectivesInX/Y -1 so that a uv of 1 doesn't result in an index of _PerspectivesInX/Y
    return uint2(clamp(floor(packedUV.x * _PerspectivesInX), 0 , _PerspectivesInX-1),
        clamp(floor(packedUV.y * _PerspectivesInY), 0 , _PerspectivesInY-1));
}

uint dkGetPerspectiveIndexFromCoord(uint2 perspectiveCoord)
{
    if (!_TextureFlipped)
    {
        // flip the y index before converting these coordinates to an index to index the correct perspective data
        // (unity image coordinates are at the bottom left so the y index goes at the bottom to the top while depthkit 
        // expects the y index to increase from the top left going down to the bottom
        perspectiveCoord.y = ((_PerspectivesInY - 1) - perspectiveCoord.y);
    }
    uint perspectiveIndex = toIndex(perspectiveCoord, uint2(_PerspectivesInX, _PerspectivesInY));
    // clamp the perspective index to _PerspectivesCount in the case that the number of perspectives is not equal to 
    // _PerspectivesInX * _PerspectivesInY eg: 9 perspectives divided into 5x2
    return clamp(perspectiveIndex, 0, _PerspectivesCount - 1);
}

uint2 dkGetPerspectiveCoordsFromIndex(uint perspectiveIndex)
{
    uint2 perspectiveCoord = toCoord(perspectiveIndex, uint2(_PerspectivesInX, _PerspectivesInY));

    if (!_TextureFlipped)
    {
        // flip the y index before converting these coordinates to an index to index the correct perspective data
        // (unity image coordinates are at the bottom left so the y index goes at the bottom to the top while depthkit 
        // expects the y index to increase from the top left to the bottom
        perspectiveCoord.y = ((_PerspectivesInY - 1) - perspectiveCoord.y);
    }
    return perspectiveCoord;
}

uint dkGetPerspectiveIndexFromPackedUV(float2 packedUV)
{
    return dkGetPerspectiveIndexFromCoord(dkGetPerspectiveCoordFromPackedUV(packedUV));
}

float2 dkGetDepthImageSize(uint perspectiveIndex)
{
    return _PerspectiveDataStructuredBuffer[perspectiveIndex].depthImageSize.xy;
}

float3 dkGetDepthCameraPosition(uint perspectiveIndex)
{
    return _PerspectiveDataStructuredBuffer[perspectiveIndex].cameraPosition;
}

float3 dkGetDepthCameraDirection(uint perspectiveIndex)
{
    return normalize(_PerspectiveDataStructuredBuffer[perspectiveIndex].cameraNormal);
}

//(0 - 1) UV to perspective's uv in packed texture space
float2 dkPerspectiveToPackedUV(uint2 perspectiveCoord, float2 perspectiveUV)
{
    float2 frameScale = { 1.0f / float(_PerspectivesInX), 1.0f / float(_PerspectivesInY) };
    float2 offset = float2(perspectiveCoord) * frameScale;
    return saturate(remap2(perspectiveUV, float2(0.0f, 0.0f), float2(1.0f, 1.0f), offset, offset + frameScale));
}

//(0 - 1) UV to perspective's uv in packed texture space
float2 dkPerspectiveToPackedUV(uint perspectiveIndex, float2 perspectiveUV)
{
    return dkPerspectiveToPackedUV(dkGetPerspectiveCoordsFromIndex(perspectiveIndex), perspectiveUV);
}

//uv from packed texture, aka cpp image space
float2 dkPackedToPerspectiveUV(float2 packedUV, uint2 perspectiveCoord)
{
    float2 frameScale = { 1.0f / float(_PerspectivesInX), 1.0f / float(_PerspectivesInY) };
    float2 offset = float2(perspectiveCoord)*frameScale;
    return saturate(remap2(packedUV, offset, offset + frameScale, float2(0.0f, 0.0f), float2(1.0f, 1.0f)));
}

//uv from packed texture, aka cpp image space
float2 dkPackedToPerspectiveUV(float2 packedUV)
{
    uint2 perspectiveCoord = dkGetPerspectiveCoordFromPackedUV(packedUV);
    return dkPackedToPerspectiveUV(packedUV, perspectiveCoord);
}

float2 dkPerspectiveToCroppedUV(uint perspectiveIndex, float2 uncroppedUV)
{
    return _PerspectiveDataStructuredBuffer[perspectiveIndex].crop.xy + (uncroppedUV * _PerspectiveDataStructuredBuffer[perspectiveIndex].crop.zw);
}

float2 dkCroppedToPerspectiveUV(uint perspectiveIndex, float2 croppedUV)
{
    return (croppedUV - _PerspectiveDataStructuredBuffer[perspectiveIndex].crop.xy) / _PerspectiveDataStructuredBuffer[perspectiveIndex].crop.zw;
}

float dkNormalizedDepthToMeters(uint perspectiveIndex, float normalizedDepth)
{
    return normalizedDepth * (_PerspectiveDataStructuredBuffer[perspectiveIndex].farClip - _PerspectiveDataStructuredBuffer[perspectiveIndex].nearClip) + _PerspectiveDataStructuredBuffer[perspectiveIndex].nearClip;
}

float dkMetersToNormalizedDepth(uint perspectiveIndex, float depthMeters)
{
    return (depthMeters - _PerspectiveDataStructuredBuffer[perspectiveIndex].nearClip) / (_PerspectiveDataStructuredBuffer[perspectiveIndex].farClip - _PerspectiveDataStructuredBuffer[perspectiveIndex].nearClip);
}

float3 dkUnproject(uint perspectiveIndex, float2 pixel, float depthMeters)
{
    return float3((pixel - _PerspectiveDataStructuredBuffer[perspectiveIndex].depthPrincipalPoint.xy) * depthMeters / _PerspectiveDataStructuredBuffer[perspectiveIndex].depthFocalLength.xy, depthMeters);
}

float2 dkProject(uint perspectiveIndex, float3 position)
{
    return (position.xy / position.z) * _PerspectiveDataStructuredBuffer[perspectiveIndex].depthFocalLength.xy + _PerspectiveDataStructuredBuffer[perspectiveIndex].depthPrincipalPoint.xy;
}

float3 dkDepthCameraObjectSpaceToWorldSpace(uint perspectiveIndex, float3 objectPosition)
{
    return mul(_PerspectiveDataStructuredBuffer[perspectiveIndex].extrinsics, float4(objectPosition, 1)).xyz;
}

float3 dkWorldSpaceToDepthCameraObjectSpace(uint perspectiveIndex, float3 objectPosition)
{
    return mul(_PerspectiveDataStructuredBuffer[perspectiveIndex].extrinsicsInverse, float4(objectPosition, 1)).xyz;
}

float3 dkDepthCameraObjectSpaceDirToWorldSpaceDir(uint perspectiveIndex, float3 dir)
{
    return normalize(mul((float3x3)_PerspectiveDataStructuredBuffer[perspectiveIndex].extrinsics, dir).xyz);
}

float3 dkWorldSpaceDirToDepthCameraObjectSpaceDir(uint perspectiveIndex, float3 dir)
{
    return normalize(mul((float3x3)_PerspectiveDataStructuredBuffer[perspectiveIndex].extrinsicsInverse, dir).xyz);
}

void dkGetColorAndDepthUV(float2 packedUV, out float2 colorUV, out float2 depthUV)
{
    uint2 perspectiveCoord = dkGetPerspectiveCoordFromPackedUV(packedUV);

    float uv_yStart = float(perspectiveCoord.y) /float(_PerspectivesInY);
    float uv_yEnd = float(perspectiveCoord.y +1) / float(_PerspectivesInY);
    float uv_yMid = 0.5 / float(_PerspectivesInY);

    colorUV.x = depthUV.x = packedUV.x;
    if (_TextureFlipped)
    {
        // Convert to packedY to perspectiveY range before mapping to color and depth
        float perspectiveY = dkPackedToPerspectiveUV(packedUV, perspectiveCoord).y;

        // color y is lesser that depth y
        // example range in single row: 0 - 0.5 color, 0.5 - 1 depth
        perspectiveY = 1.0 - perspectiveY;
        colorUV.y = uv_yStart+ perspectiveY * uv_yMid;
        depthUV.y = colorUV.y + uv_yMid;
    }
    else
    {
        // unity coordinates are inverted in y therefore the depth y is lesser that the color y
        // example range in single row: 0 - 0.5 depth, 0.5 - 1 color
        depthUV.y = remap(packedUV.y, uv_yStart, uv_yEnd, uv_yStart, uv_yStart+ uv_yMid);
        colorUV.y = depthUV.y + uv_yMid;
    }
}

// perspectiveUV [0-1] is in the perspective's image space, not in packed cpp image space
float4 dkPerspectiveUVToLocalDepthInMeters(uint perspectiveIndex, float2 perspectiveUV, float depthInMeters)
{
    // TODO:: fix with coordinate system clean up
    // This y flip works around the unity coordinate system origin at the bottom left of the image
    perspectiveUV.y = 1.0f - perspectiveUV.y;

    float2 croppedUv = dkPerspectiveToCroppedUV(perspectiveIndex, perspectiveUV);

    float2 pixel = croppedUv * dkGetDepthImageSize(perspectiveIndex);

    float3 worldPosition = dkUnproject(perspectiveIndex, pixel, depthInMeters);

    // TODO:: fix with coordinate system clean up
    // This y flip ensures the geometry is the upright
    worldPosition.y *= -1.0f;

    return float4(worldPosition, 1);
}

// perspectiveUV [0-1] is in the perspective's image space, not in packed cpp image space
float4 dkPerspectiveUVToLocal(uint perspectiveIndex, float2 perspectiveUV, float normalizedDepth)
{
    return dkPerspectiveUVToLocalDepthInMeters(
        perspectiveIndex,
        perspectiveUV,
        dkNormalizedDepthToMeters(perspectiveIndex, normalizedDepth)
    );
}

// perspectiveUV [0-1] is in the perspective's image space, not in packed cpp image space
float4 dkPerspectiveUVToWorld(uint perspectiveIndex, float2 perspectiveUV, float normalizedDepth)
{
    float3 localPosition = dkPerspectiveUVToLocal(perspectiveIndex, perspectiveUV, normalizedDepth).xyz;

    return float4(dkDepthCameraObjectSpaceToWorldSpace(perspectiveIndex, localPosition), 1);
}

//uv is in packed cpp image space, convert to that perspective's world space
float4 dkPackedUVToWorld(float2 packedUV, float depth)
{
    // get index by converting the perspective coordinates this ensures the mapping between the uvs and
    // the index into the camera array is accurate regardless of image space coordinate origins
    uint2 perspectiveCoord = dkGetPerspectiveCoordFromPackedUV(packedUV);
    float2 perspectiveUV = dkPackedToPerspectiveUV(packedUV, perspectiveCoord);
    return dkPerspectiveUVToWorld(dkGetPerspectiveIndexFromCoord(perspectiveCoord), perspectiveUV, depth);
}

//uv is in packed cpp image space, convert to that perspective's world space
float4 dkPackedUVToLocal(float2 packedUV, float depth)
{
    // get index by converting the perspective coordinates this ensures the mapping between the uvs and
    // the index into the camera array is accurate regardless of image space coordinate origins
    uint2 perspectiveCoord = dkGetPerspectiveCoordFromPackedUV(packedUV);
    float2 perspectiveUV = dkPackedToPerspectiveUV(packedUV, perspectiveCoord);
    return dkPerspectiveUVToLocal(dkGetPerspectiveIndexFromCoord(perspectiveCoord), perspectiveUV, depth);
}

// single perspective world space to packed color and depth uvs, includes their perspective offset. uv contains the 0-1 uv for that perspective's view
void dkWorldToPerspectiveUV(uint perspectiveIndex, float3 position, out float2 perspectiveUV, out float2 depthUV, out float2 colorUV, out float3 depthViewSpacePos)
{
    depthViewSpacePos = dkWorldSpaceToDepthCameraObjectSpace(perspectiveIndex, position);

    // TODO:: fix with coordinate system clean up
    // This y flip ensures the geometry projected to the correct image space
    depthViewSpacePos.y *= -1.0f;

    float2 projected = dkProject(perspectiveIndex, depthViewSpacePos);

    float2 croppedUV = projected / dkGetDepthImageSize(perspectiveIndex);

    perspectiveUV = dkCroppedToPerspectiveUV(perspectiveIndex, croppedUV);

    // TODO:: fix with coordinate system clean up
    // This y flip works around the unity coordinate system origin at the bottom left of the image
    perspectiveUV.y = 1.0 - perspectiveUV.y;


    float2 packedUV = dkPerspectiveToPackedUV(perspectiveIndex, perspectiveUV);

    //adjust y for cpp format
    dkGetColorAndDepthUV(packedUV, colorUV, depthUV);
}

float3 dkColorCorrect_Depth(float3 samplecolor)
{
    // if Unity is rendering in Linear space and we are using unity video player (which must encode frames in gamma space), we apply inverse gamma (1/2.2) to depth frame to get a linear sample
    float3 correctedColor = samplecolor;
    if (_ColorSpaceCorrectionDepth == DK_CORRECT_LINEAR_TO_GAMMA)
    {
        correctedColor = float3(LinearToGammaSpaceExact(samplecolor.r),
                                 LinearToGammaSpaceExact(samplecolor.g),
                                 LinearToGammaSpaceExact(samplecolor.b));
    }
    else if (_ColorSpaceCorrectionDepth == DK_CORRECT_GAMMA_TO_LINEAR)
    {
        correctedColor = float3(GammaToLinearSpaceExact(samplecolor.r), 
                                 GammaToLinearSpaceExact(samplecolor.g), 
                                 GammaToLinearSpaceExact(samplecolor.b));
    }
    else if (_ColorSpaceCorrectionDepth == DK_CORRECT_LINEAR_TO_GAMMA_2X)
    {
        correctedColor = float3(
            LinearToGammaSpaceExact(LinearToGammaSpaceExact(samplecolor.r)),
            LinearToGammaSpaceExact(LinearToGammaSpaceExact(samplecolor.g)),
            LinearToGammaSpaceExact(LinearToGammaSpaceExact(samplecolor.b)));
    }
    return correctedColor;
} 

float3 dkColorCorrect_Color(float3 samplecolor)
{
    // if unity is rendering in linear space and we are using external gamma corrected assets, then encode to gamma space pow(2.2) to match the input assets
    float3 correctedColor = samplecolor;
    if (_ColorSpaceCorrectionColor == DK_CORRECT_LINEAR_TO_GAMMA)
    {
        correctedColor = LinearToGammaSpace(samplecolor);
    }
    else if (_ColorSpaceCorrectionColor == DK_CORRECT_GAMMA_TO_LINEAR)
    {
        correctedColor = GammaToLinearSpace(samplecolor);
    }
    else if (_ColorSpaceCorrectionDepth == DK_CORRECT_LINEAR_TO_GAMMA_2X)
    {
        correctedColor = LinearToGammaSpace(LinearToGammaSpace(samplecolor));
    }
    return correctedColor;
} 

// Returns bias adjusted normalized depth
// TODO: stop using normalized depth for anything outside of the internals of this function.
float dkConvertDepthHSV(in float4 depthsample, in uint perspectiveIndex, in float2 perspectiveUV)
{
    float3 depthsamplehsv = rgb2hsv(dkColorCorrect_Depth(depthsample.rgb));
    float filtereddepth = pow(depthsamplehsv.b, 6);
    float preBiasDepth = filtereddepth > _EdgeChoke + DK_BRIGHTNESS_THRESHOLD_OFFSET ? depthsamplehsv.r : 0.0;
    float4 projectionPlanePos = dkPerspectiveUVToLocalDepthInMeters(perspectiveIndex, perspectiveUV, 1.0);

    float depthMeters = dkNormalizedDepthToMeters(perspectiveIndex, preBiasDepth);
    float adjustedDepth = depthMeters - (_RadialBiasPerspInMeters[perspectiveIndex].x * rsqrt(lengthSquared(projectionPlanePos.xyz)));

    return dkMetersToNormalizedDepth(perspectiveIndex, adjustedDepth);
}

//uv is in perspective cpp image space, includes perspective offset
//returns normalized depth loaded to the nearest pixel
float dkLoadDepth(in float2 depthUV, in uint perspectiveIndex, in float2 perspectiveUV)
{
    depthUV = saturate(depthUV);
    int textureWidth, textureHeight, levels;
    _CPPTexture.GetDimensions(0, textureWidth, textureHeight, levels);
    float4 depthsample = _CPPTexture.Load(int3(depthUV * float2(textureWidth, textureHeight), 0));
    return dkConvertDepthHSV(depthsample, perspectiveIndex, perspectiveUV);
}

//uv is in perspective cpp image space, includes perspective offset
//returns normalized depth with binliear interpolation
float dkSampleDepth(in float2 depthUV, in uint perspectiveIndex, in float2 perspectiveUV)
{
    depthUV = saturate(depthUV);
    float4 depthsample = _CPPTexture.SampleLevel(_LinearClamp, depthUV, 0);
    return dkConvertDepthHSV(depthsample, perspectiveIndex, perspectiveUV);
}

//uv is in packed cpp image space, returns crop adjusted uvs for the given perspective
void dkUnpackUVs(in float2 packedUV, out float2 colorUV, out float2 depthUV, out float2 perspectiveUV, out uint perspectiveIndex)
{
    // get index by converting the perspective coordinates this ensures the mapping between the uvs and
    // the index into the camera array is accurate regardless of image space coordinate origins
    uint2 perspectiveCoord = dkGetPerspectiveCoordFromPackedUV(packedUV);
    perspectiveIndex = dkGetPerspectiveIndexFromCoord(perspectiveCoord);
    perspectiveUV = dkPackedToPerspectiveUV(packedUV, perspectiveCoord);
    dkGetColorAndDepthUV(packedUV, colorUV, depthUV);
}

//checks that normalized depth is valid for perspective
bool dkValidateNormalizedDepth(in uint perspectiveIndex, in float depth) {
    bool result = false;
    if ((depth >= _PerspectiveDataStructuredBuffer[perspectiveIndex].clipEpsilon) && (depth <= (1.0f - _PerspectiveDataStructuredBuffer[perspectiveIndex].clipEpsilon))) result = true;
    return result;
}

//uv is in cpp image space, not including any perspective offset.  
// returned sample is loaded to the nearest pixel
bool dkLoadDepthNormalized(in float2 packedUV, out float depth, out float2 depthUV, out float2 perspectiveUV, out uint perspectiveIndex)
{
    float2 colorUV;
    dkUnpackUVs(packedUV, colorUV, depthUV, perspectiveUV, perspectiveIndex);
    depth = dkLoadDepth(depthUV, perspectiveIndex, perspectiveUV);
    return dkValidateNormalizedDepth(perspectiveIndex, depth);
}

//uv is in cpp image space, not including any perspective offset
// returned sample is bilinearly interpolated
bool dkSampleDepthNormalized(in float2 packedUV, out float depth, out float2 depthUV, out float2 perspectiveUV, out uint perspectiveIndex)
{
    float2 colorUV;
    dkUnpackUVs(packedUV, colorUV, depthUV, perspectiveUV, perspectiveIndex);
    depth = dkSampleDepth(depthUV, perspectiveIndex, perspectiveUV);
    return dkValidateNormalizedDepth(perspectiveIndex, depth);
}

// NOT USED
//uv is in cpp image space, not including any perspective offset
// returned sample is loaded to the nearest pixel
bool dkLoadDepthMeters(in float2 uv, out float depthMeters, out float2 depthUV, out uint perspectiveIndex)
{
    float2 colorUV, perspectiveUV;
    dkUnpackUVs(uv, colorUV, depthUV, perspectiveUV, perspectiveIndex);
    float depth = dkLoadDepth(depthUV, perspectiveIndex, perspectiveUV);
    bool valid = dkValidateNormalizedDepth(perspectiveIndex, depth);
    depthMeters = dkNormalizedDepthToMeters(perspectiveIndex, depth);
    return valid;
}

//uv is in cpp image space, not including any perspective offset
// returned sample is bilinearly interpolated
bool dkSampleDepthMeters(in float2 uv, out float depthMeters, out float2 depthUV, out uint perspectiveIndex)
{
    float2 colorUV, perspectiveUV;
    dkUnpackUVs(uv, colorUV, depthUV, perspectiveUV, perspectiveIndex);
    float depth = dkSampleDepth(depthUV, perspectiveIndex, perspectiveUV);
    bool valid = dkValidateNormalizedDepth(perspectiveIndex, depth);
    depthMeters = dkNormalizedDepthToMeters(perspectiveIndex, depth);
    return valid;
}

// NOT USED
//uvs are in perspective cpp image space, includes perspective offset
bool dkSampleColorAndLoadDepth(in float2 packedUV, out float3 color, out float depth)
{
    float2 depthUV, colorUV, perspectiveUV;
    uint perspectiveIndex;
    dkUnpackUVs(packedUV, colorUV, depthUV, perspectiveUV, perspectiveIndex);
    depth = dkLoadDepth(depthUV, perspectiveIndex, perspectiveUV);
    float4 sampledColor = _CPPTexture.SampleLevel(_LinearClamp, colorUV, 0);
    color = dkColorCorrect_Color(sampledColor.rgb);
    return dkValidateNormalizedDepth(0, depth);
}

//uv is in perspective cpp image space, includes perspective offset
float3 dkSampleColor(in float2 colorUV)
{
    float4 sampledColor = _CPPTexture.SampleLevel(_LinearClamp, colorUV, 0);
    return dkColorCorrect_Color(sampledColor.rgb);
}

// Returns the pixel resolution of a single perspective's color or depth portion
// This is identical for all perspectives
int2 dkGetPerspectiveResolution()
{
    int textureWidth, textureHeight, levels;
    _CPPTexture.GetDimensions(0, textureWidth, textureHeight, levels);

    // Note: dividing textureHeight by two since each perspective contains both a color and depth portion
    return int2((uint)textureWidth / (uint)_PerspectivesInX, (uint)textureHeight / 2u / (uint)_PerspectivesInY);
}

#endif // _DEPTHKIT_CGINC
