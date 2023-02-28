#ifndef _DK_GAUSSIANBLUR_INC
#define _DK_GAUSSIANBLUR_INC

#include "Packages/nyc.scatter.depthkit.core/Runtime/Resources/Shaders/Includes/Utils.cginc"

SamplerState _LinearClamp;

float2 _PongSize;
float2 _Axis;
float _GaussianExponential;
float _GaussianNormalization;

static const uint BLOCK_WIDTH = BLOCK_SIZE * BLOCK_SIZE; // 64x1x1 dispatch tile
static const int KERNEL_WIDTH = TILE_BORDER;
static const uint TILE_WIDTH = BLOCK_WIDTH + TILE_BORDER * 2;

groupshared float gsamples[TILE_WIDTH];

#ifndef DK_GAUSSIAN_FILTER_SAMPLE_UV
#define DK_GAUSSIAN_FILTER_SAMPLE_UV(uv) uv
#endif

#ifndef DK_GAUSSIAN_FILTER_OUTPUT_COORD
#define DK_GAUSSIAN_FILTER_OUTPUT_COORD(pixel) pixel.xy
#endif

void GaussianBlurFilter(uint3 id, uint3 GroupId, uint3 GroupThreadId, uint GroupIndex)
{
    uint ind;
    const float2 oneOverPongSize = 1.0f / _PongSize;

    const int2 tile_upperleft = AxisSwizzle(_Axis, int2(GroupId.xy)) * AxisMask(_Axis, BLOCK_WIDTH) - TILE_BORDER * _Axis;

    [unroll]
    for (ind = GroupIndex; ind < TILE_WIDTH; ind += BLOCK_WIDTH)
    {
        const int2 pixel = tile_upperleft + ind * int2(_Axis);
        const float2 uv = (pixel.xy + 0.5f) * oneOverPongSize;
        gsamples[ind] = _PingData.SampleLevel(_LinearClamp, DK_GAUSSIAN_FILTER_SAMPLE_UV(uv), 0);
    }

    GroupMemoryBarrierWithGroupSync();

    int2 coord = AxisSwizzle(_Axis, GroupThreadId.xy) + TILE_BORDER * _Axis;

    const int2 pixel = tile_upperleft + coord;

    //make sure there's a pixel here
    if(pixel.x < 0 || pixel.y < 0 || pixel.x >= _PongSize.x || pixel.y >= _PongSize.y) return;

    float2 r = 0.0;

    int sampleOffset = max(coord.x, coord.y); 
    [unroll]
    for (int kernel_ind = -KERNEL_WIDTH; kernel_ind <= KERNEL_WIDTH; kernel_ind++)
    {
        int sampleCoord = kernel_ind + sampleOffset;
        float gaussian = _GaussianNormalization * exp(_GaussianExponential * float(kernel_ind*kernel_ind));
        r += float2(gsamples[sampleCoord] * gaussian, gaussian);
    }
    _PongData[DK_GAUSSIAN_FILTER_OUTPUT_COORD(pixel)] = r.x / r.y;
}

#endif
