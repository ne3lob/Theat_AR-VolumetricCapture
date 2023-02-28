#ifndef _Depthkit_UtilS_CGINC
#define _Depthkit_UtilS_CGINC

#define FLOAT_EPS 0.00001f

float3 rgb2hsv(float3 c)
{
    float4 K = float4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
    float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
    float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));

    float d = q.x - min(q.w, q.y);
    return float3(abs(q.z + (q.w - q.y) / (6.0 * d + FLOAT_EPS)), d / (q.x + FLOAT_EPS), q.x);
}

float3 hsv2rgb(float3 c)
{
    float4 K = float4(1.0f, 2.0f / 3.0f, 1.0f / 3.0f, 3.0f);
    float3 p = abs(frac(c.xxx + K.xyz) * 6.0f - K.www);
    return c.z * lerp(float3(K.xxx), clamp(p - K.xxx, 0.0f, 1.0f), c.y);
}

float remap(float value, float low1, float high1, float low2, float high2) {
    return (low2 + (value - low1) * (high2 - low2) / (high1 - low1));
}

float2 remap2(float2 value, float2 low1, float2 high1, float2 low2, float2 high2) {
    return (low2 + (value - low1) * (high2 - low2) / (high1 - low1));
}

uint2 toCoord(uint idx, uint2 dim)
{
    return uint2(idx % dim.x, idx / dim.x);
}

uint toIndex(uint2 coord, uint2 dim)
{
    return coord.x + coord.y * dim.x;
}

uint2 toCoordClamp(int idx, uint2 dim)
{
    idx = clamp(idx, 0, (dim.x * dim.y) - 1);
    return uint2(idx % dim.x, idx / dim.x);
}

uint toIndexClamp(int2 coord, uint2 dim)
{
    coord = clamp(coord, 0, (int2)dim - 1);
    return coord.x + coord.y * dim.x;
}

uint3 toCoord3D(uint idx, uint3 dim)
{
    return uint3(idx % dim.x, (idx / dim.x) % dim.y, idx / (dim.x * dim.y));
}

uint toIndex3D(uint3 coord, uint3 dim)
{
    return coord.x + coord.y * dim.x + coord.z * dim.x * dim.y;
}

uint3 toCoord3DClamp(int idx, uint3 dim)
{
    idx = clamp(idx, 0, (dim.x * dim.y * dim.z) - 1);
    return uint3(idx % dim.x, (idx / dim.x) % dim.y, idx / (dim.x * dim.y));
}

uint toIndex3DClamp(int3 coord, uint3 dim)
{
    coord = clamp(coord, 0, (int3)dim - 1);
    return coord.x + coord.y * dim.x + coord.z * dim.x * dim.y;
}

float lengthSquared(float3 v)
{
    return dot(v, v);
}

float lengthSquared(float2 v)
{
    return dot(v, v);
}

float distanceSquared(float3 a, float3 b)
{
    return lengthSquared(a - b);
}

float luminance(float3 color)
{
    return dot(color, float3(0.299f, 0.587f, 0.114f));
}

float cmToMeters(float cm)
{
    return cm / 100.0f;
}

float metersTocm(float meters)
{
    return meters * 100.0f;
}

float3 clamp3(float3 val, float3 minimum, float3 maximum)
{
    return min(maximum, max(minimum, val));
}

// returns int2(input, 1) or int2(1, input) depending on Axis
int2 AxisMask(int2 axis, int input)
{
    return axis * input + axis.yx;
}

// Swizzles based on Axis vector
int2 AxisSwizzle(int2 axis, int2 input)
{
    return axis.xx * input.xy + axis.yy * input.yx;
}

//https://forum.unity.com/threads/storing-two-16-bits-halfs-in-one-32-bits-float.987531/
//when in 0-1 range
float dkScale01FloatTo16BitRange(float a)
{
    return log2(saturate(a) + 1.0) * 65535.0;
}
//when in 0-1 range
float dkUnscale01FloatFrom16BitRange(uint a)
{
    return pow(2.0, (a >> 16) / 65535.0) - 1.0;
}

float dkPackFloats(float a, float b)
{
    //Packing
    uint a16 = f32tof16(a);
    uint b16 = f32tof16(b);
    uint packed = (a16 << 16) | (b16 & 0xFFFF);
    return asfloat(packed);
}

void dkUnpackFloats(float input, out float a, out float b)
{
    //Unpacking
    uint uintInput = asuint(input);
    a = f16tof32(uintInput >> 16);
    b = f16tof32(uintInput & 0xFFFF);
}

#endif