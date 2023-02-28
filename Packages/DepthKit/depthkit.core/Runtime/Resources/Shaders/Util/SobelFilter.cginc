/************************************************************************************

Depthkit Unity SDK License v1
Copyright 2016-2020 Scatter All Rights reserved.  

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

#include "Packages/nyc.scatter.depthkit.core/Runtime/Resources/Shaders/Includes/Depthkit.cginc"

#ifndef DK_SOBEL_FILTER_UV
#define DK_SOBEL_FILTER_UV(uv) uv
#endif

#ifndef DK_SOBEL_FILTER_OUTPUT_COORD
#define DK_SOBEL_FILTER_OUTPUT_COORD(pixel) pixel.xy
#endif

float _SobelMultiplier;
float4 _MaskTextureTS;
float _SobelInvalidateEdgeWidth;
float _SobelInvalidateStrength = 1.0f;

static const uint BLOCK_THREAD_COUNT = BLOCK_SIZE * BLOCK_SIZE;
static const uint TILE_BORDER = 1;
static const uint TILE_SIZE = BLOCK_SIZE + TILE_BORDER * 2;
static const uint TILE_PIXEL_COUNT = TILE_SIZE * TILE_SIZE;

groupshared float g_samples[TILE_PIXEL_COUNT];

// ------------
//| 7 | 2 | 5 |
//| 1 | 0 | 3 |
//| 6 | 4 | 8 |
// ------------

static const int2 offsets[9] =
{
    { 0, 0 }, //0
    { -1, 0 }, //1
    { 0, -1 }, //2
    { 1, 0 }, //3
    { 0, 1 }, //4
    { 1, -1 }, //5
    { -1, 1 }, //6
    { -1, -1 }, //7
    { 1, 1 } //8
};

// for pixels that have invalid depth values, we don't want to use 0 to compute the sobel, instead we pass in the kernel center depth value.
// This avoids an unecessary dilation of the raw edge due to the sobel detecting the raw edge as an additional edge.
float validFilter(float floorValue, float value)
{
    return value > 0.0 ? value : floorValue;
}

void SobelFilter(uint3 id, uint3 GroupId, uint3 GroupThreadId, uint GroupIndex)
{
    uint ind;

    //////////////////////////////////////
    //Fill a tile's worth of shared memory
    //////////////////////////////////////

    const int2 tileUpperLeft = int2(GroupId.xy * BLOCK_SIZE) - (int2) TILE_BORDER;
    [unroll]
    for (ind = GroupIndex; ind < TILE_PIXEL_COUNT; ind += BLOCK_THREAD_COUNT)
    {
        int2 pixel = tileUpperLeft + (int2) toCoord(ind, TILE_SIZE);
        float2 uv = (pixel + 0.5f) * _MaskTextureTS.xy;
        uv = DK_SOBEL_FILTER_UV(uv);
        float depthMeters;
        float2 depthUV;
        uint perspectiveIndex;
        bool inBounds = any(pixel == clamp(pixel, 0, _MaskTextureTS.zw));
        bool valid = inBounds && dkSampleDepthMeters(uv, depthMeters, depthUV, perspectiveIndex);

        g_samples[ind] = valid ? depthMeters : 0.0f;
    }

    GroupMemoryBarrierWithGroupSync(); //wait for everyone

    int2 tileOffset = GroupThreadId.xy + TILE_BORDER;

    uint id0 = toIndex(tileOffset + offsets[0], TILE_SIZE);
    uint id1 = toIndex(tileOffset + offsets[1], TILE_SIZE);
    uint id2 = toIndex(tileOffset + offsets[2], TILE_SIZE);
    uint id3 = toIndex(tileOffset + offsets[3], TILE_SIZE);
    uint id4 = toIndex(tileOffset + offsets[4], TILE_SIZE);
    uint id5 = toIndex(tileOffset + offsets[5], TILE_SIZE);
    uint id6 = toIndex(tileOffset + offsets[6], TILE_SIZE);
    uint id7 = toIndex(tileOffset + offsets[7], TILE_SIZE);
    uint id8 = toIndex(tileOffset + offsets[8], TILE_SIZE);

    float centralValue = g_samples[id0];
    float sobel_edge_h = validFilter(centralValue, g_samples[id5]) + (2.0 * validFilter(centralValue, g_samples[id3])) + validFilter(centralValue, g_samples[id8])
        - (validFilter(centralValue, g_samples[id7]) + (2.0 * validFilter(centralValue, g_samples[id1])) + validFilter(centralValue, g_samples[id6]));
    float sobel_edge_v = validFilter(centralValue, g_samples[id7]) + (2.0 * validFilter(centralValue, g_samples[id2])) + validFilter(centralValue, g_samples[id5])
        - (validFilter(centralValue, g_samples[id6]) + (2.0 * validFilter(centralValue, g_samples[id4])) + validFilter(centralValue, g_samples[id8]));
    float sobel = abs(sobel_edge_h) + abs(sobel_edge_v); // approximation without sqrt

    float edge = centralValue <= 0.0 ? _SobelInvalidateStrength :  _SobelMultiplier * sobel;

    // invalidate edge border
    float2 oneOverEdgeWidth = 1.0f / _SobelInvalidateEdgeWidth;
    float2 edgeBorder = saturate((float2(_SobelInvalidateEdgeWidth, _SobelInvalidateEdgeWidth) - float2(id.xy)) * oneOverEdgeWidth) + saturate((float2(id.xy) - (_MaskTextureTS.zw - _SobelInvalidateEdgeWidth - 1.0f)) * oneOverEdgeWidth);

    _MaskTexture[DK_SOBEL_FILTER_OUTPUT_COORD(id)] = lerp(edge, _SobelInvalidateStrength, min(edgeBorder.x + edgeBorder.y, 1.0));
}