#ifndef _DEPTHKIT_CORE_UNIFORMS_CGINC
#define _DEPTHKIT_CORE_UNIFORMS_CGINC

// CLIP DATA
float _EdgeChoke = 0.5; // per-pixel brightness threshold, used to refine edge geometry from eroneous edge depth samples
StructuredBuffer<PerspectiveData> _PerspectiveDataStructuredBuffer;
int _PerspectivesCount;
int _PerspectivesInX;
int _PerspectivesInY;
int _TextureFlipped;
int _ColorSpaceCorrectionDepth;
int _ColorSpaceCorrectionColor;

Texture2D<float4> _CPPTexture;
SamplerState _LinearClamp;

// MESH SOURCE DATA
// The datatype for the per perspective bias is a float4 because float arrays get pushed to the shader as 4 component float vectors.
float4 _RadialBiasPerspInMeters[DK_MAX_NUM_PERSPECTIVES];

#endif //_DEPTHKIT_CORE_UNIFORMS_CGINC