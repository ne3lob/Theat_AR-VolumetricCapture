
DK_EDGEMASK_UNIFORMS

float4x4 _LocalTransform;
float _ShadowAmount;

struct appdata
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float2 packedUV : TEXCOORD0;
    uint   id : SV_VertexID;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f
{
    float2 packedUV : TEXCOORD0;
    float4 pos : SV_POSITION;
#if defined(DK_USE_LIGHTPROBES) && defined(DK_FORWARDBASE_PASS)
    float3 indirect : COLOR1;
#endif
    UNITY_FOG_COORDS(1)
    SHADOW_COORDS(3)
    UNITY_VERTEX_OUTPUT_STEREO
};

v2f vert(appdata v)
{
    v2f o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_OUTPUT(v2f, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    Vertex vert = dkSampleTriangleBuffer(floor(v.id / 3), v.id % 3);

    o.packedUV = vert.uv.zw;
    v.vertex = mul(_LocalTransform, float4(vert.position.xyz, 1));
    v.normal = mul((float3x3)_LocalTransform, vert.normal);

    o.pos = UnityObjectToClipPos(v.vertex);

#if defined(DK_USE_LIGHTPROBES) && defined(DK_FORWARDBASE_PASS)
    float3 worldNormal = UnityObjectToWorldNormal(v.normal);
    o.indirect = max(0, ShadeSH9(half4(worldNormal, 1)));
#endif

    UNITY_TRANSFER_FOG(o, o.pos);
    TRANSFER_SHADOW(o);
    return o;
}

fixed4 frag(v2f i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    uint perspectiveIndex;
    float2 depthUV, colorUV, perspectiveUV;
    dkUnpackUVs(i.packedUV, colorUV, depthUV, perspectiveUV, perspectiveIndex);
    
    float alpha = 1.0f;
    
#if defined(DK_USE_EDGEMASK) && (!defined(DK_CORE_SKIP_SAMPLE_EDGEMASK) || defined(DK_DEBUG_EDGEMASK))
    alpha = DK_SAMPLE_EDGEMASK(perspectiveUV, perspectiveIndex, i.pos);
#endif

#ifndef DK_CORE_SKIP_FRAGMENT_DEPTHSAMPLE
    float depth = dkSampleDepth(depthUV, perspectiveIndex, perspectiveUV);
    alpha = dkValidateNormalizedDepth(perspectiveIndex, depth) ? alpha : -1.f;
#endif
        
#ifdef DK_DEBUG_EDGEMASK
    float3 color = float3(alpha * dkGetDebugCameraColor(perspectiveIndex));
    #ifdef DK_USE_EDGEMASK
        float2 minMaxMask = DK_SAMPLE_DOWNSAMPLED_EDGEMASK(perspectiveUV, perspectiveIndex);
        if (alpha < 0.0)
        { 
            color = float3(0.3, 0.3, 0.3);
        }
        if (alpha == 0.0) //completely invalid
        { 
            color = float3(0.05, 0.05, 0.05);
        }
        // dim if is edge
        if ((1.0f - minMaxMask.y) < DK_CLIP_THRESHOLD(perspectiveIndex))
        {
            color *= .5;
        }
    #else
        if (alpha < DK_ALPHA_CLIP_THRESHOLD) { color = float3(0.3f, 0.3f, 0.3f); }
    #endif
    fixed4 col = fixed4(color.rgb, 1.0);
#else

    #ifndef DK_CORE_SKIP_FRAGMENT_DEPTHSAMPLE
    DK_FRAGMENT_CLIP(alpha, perspectiveIndex)
    #endif

    #ifdef DK_USE_DEBUG_COLOR
        fixed4 col = fixed4(dkGetDebugCameraColor(perspectiveIndex),alpha);
    #else
        float3 color = dkSampleColor(colorUV);

        #if defined(DK_NO_MAIN_LIGHT) && defined(DK_FORWARDBASE_PASS)
            #if defined(DK_USE_LIGHTPROBES)
            color = color * i.indirect;
            #endif
        #else
        float3 shadow = lerp(float3(1.0, 1.0, 1.0), SHADOW_ATTENUATION(i), saturate(_ShadowAmount));
            #if defined(DK_USE_LIGHTPROBES) && defined(DK_FORWARDBASE_PASS)
                color = lerp(color * i.indirect, color, shadow);
            #else
                color *= shadow;
            #endif
        #endif
        fixed4 col = fixed4(color.rgb, alpha);
    #endif
#endif
    
    // apply fog
    UNITY_APPLY_FOG(i.fogCoord, col);
    return col;
}