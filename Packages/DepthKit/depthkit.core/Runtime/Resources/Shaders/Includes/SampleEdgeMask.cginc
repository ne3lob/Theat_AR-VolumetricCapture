#ifndef _DK_SAMPLE_EDGE_MASK_INC
#define _DK_SAMPLE_EDGE_MASK_INC

#define DK_ALPHA_CLIP_THRESHOLD 0.5f

#ifndef DK_EDGEMASK_UNIFORMS_DEF
#define DK_EDGEMASK_UNIFORMS_DEF \
    SamplerState _Mask_LinearClamp; \
    SamplerState _Mask_PointClamp; \
    Texture2DArray<float> _MaskTexture; \
    float4 _PerspectiveToSlice[DK_MAX_NUM_PERSPECTIVES]; \
    float2 _PaddedUVScaleFactor;
#endif

#ifdef DK_DEBUG_EDGEMASK
#ifndef DK_USE_DOWNSAMPLED_EDGEMASK
#define DK_USE_DOWNSAMPLED_EDGEMASK
#endif
#endif

#ifdef DK_USE_DOWNSAMPLED_EDGEMASK
#ifndef DK_SAMPLE_DOWNSAMPLED_EDGEMASK
Texture2DArray<float2> _DownsampledMaskTexture;
#define DK_SAMPLE_DOWNSAMPLED_EDGEMASK(uv, perspectiveIndex) _DownsampledMaskTexture.SampleLevel(_Mask_PointClamp, float3(uv.xy * _PaddedUVScaleFactor, _PerspectiveToSlice[perspectiveIndex].x), 0).rg 
#else
#define DK_SAMPLE_DOWNSAMPLED_EDGEMASK(uv, perspectiveIndex) float2(1, 1)
#endif
#endif

#ifndef DK_CLIP_THRESHOLD
#ifdef DK_USE_EDGEMASK
    #ifdef DK_SCREEN_DOOR_TRANSPARENCY
    #define DK_CLIP_THRESHOLD(perspectiveIndex) 0
    #else
    #define DK_CLIP_THRESHOLD(perspectiveIndex) _PerspectiveToSlice[perspectiveIndex].y
    #endif
#else
#define DK_CLIP_THRESHOLD(perspectiveIndex) DK_ALPHA_CLIP_THRESHOLD
#endif
#endif

#if defined(DK_SCREEN_DOOR_TRANSPARENCY) && defined(DK_USE_EDGEMASK)
static const float4x4 thresholdMatrix =
{
    1.0 / 17.0, 9.0 / 17.0, 3.0 / 17.0, 11.0 / 17.0,
        13.0 / 17.0, 5.0 / 17.0, 15.0 / 17.0, 7.0 / 17.0,
        4.0 / 17.0, 12.0 / 17.0, 2.0 / 17.0, 10.0 / 17.0,
        16.0 / 17.0, 8.0 / 17.0, 14.0 / 17.0, 6.0 / 17.0
};

#ifndef DK_DITHER_ALPHA
#define DK_DITHER_ALPHA(alpha, perspectiveIndex, pos) remap(alpha, _PerspectiveToSlice[perspectiveIndex].y, _PerspectiveToSlice[perspectiveIndex].y + _PerspectiveToSlice[perspectiveIndex].z, 0.0f, 1.0f) - thresholdMatrix[pos.x % 4][pos.y % 4]
#endif
#endif

#ifndef DK_SAMPLE_EDGEMASK
#ifdef DK_USE_EDGEMASK //uv [xy = perspective, zw = packed]
    #define __DK_SAMPLE_EDGEMASK(uv, perspectiveIndex) 1.0f - _MaskTexture.SampleLevel(_Mask_LinearClamp, float3(uv.xy * _PaddedUVScaleFactor, _PerspectiveToSlice[perspectiveIndex].x), 0).r
    #ifdef DK_SCREEN_DOOR_TRANSPARENCY
        #define DK_SAMPLE_EDGEMASK(uv, perspectiveIndex, clipPos) DK_DITHER_ALPHA(__DK_SAMPLE_EDGEMASK(uv, perspectiveIndex), perspectiveIndex, clipPos)
    #else
         #define DK_SAMPLE_EDGEMASK(uv, perspectiveIndex, clipPos) __DK_SAMPLE_EDGEMASK(uv, perspectiveIndex)
    #endif
#else
    #define DK_SAMPLE_EDGEMASK(uv, perspectiveIndex, clipPos) 1.0f
#endif
#endif

/////////////////////////////////

#ifdef DK_USE_EDGEMASK
#define DK_EDGEMASK_UNIFORMS DK_EDGEMASK_UNIFORMS_DEF
#else
#define DK_EDGEMASK_UNIFORMS
#endif

#ifndef DK_FRAGMENT_CLIP
#ifdef DK_SKIP_FRAGMENT_CLIP
#define DK_FRAGMENT_CLIP(alpha, perspectiveIndex)
#else
#define DK_FRAGMENT_CLIP(alpha, perspectiveIndex) clip(alpha - DK_CLIP_THRESHOLD(perspectiveIndex));
#endif
#endif

#endif
