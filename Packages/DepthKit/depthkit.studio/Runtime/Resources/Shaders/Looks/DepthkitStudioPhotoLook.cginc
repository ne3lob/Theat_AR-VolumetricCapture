
float4x4 _LocalTransform;
float4x4 _LocalTransformInverse;
float _ShadowAmount;

struct appdata
{
    float4 vertex : POSITION;
    float2 uv : TEXCOORD0;
    uint   id : SV_VertexID;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct v2f
{
    float2 uv : TEXCOORD0;
    float4 pos : SV_POSITION;
    float4 world_position : TEXCOORD1;
    float4 object_position : TEXCOORD2;
    float3 object_normal : TEXCOORD3;
#if defined(DK_USE_LIGHTPROBES) && defined(DK_FORWARDBASE_PASS)
    float3 indirect : COLOR0;
#endif
    UNITY_FOG_COORDS(4)
    SHADOW_COORDS(5)
    UNITY_VERTEX_OUTPUT_STEREO
};

v2f vert(appdata v)
{
    v2f o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_OUTPUT(v2f, o);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

    Vertex vert = dkSampleTriangleBuffer(floor(v.id / 3), v.id % 3);
    o.uv = vert.uv.xy;

    o.object_position = float4(vert.position, 1);
    float4 localPosition = mul(_LocalTransform, o.object_position);
    o.world_position = mul(unity_ObjectToWorld, localPosition);
    o.object_normal = vert.normal;

#if defined(DK_TEXTURE_ATLAS)
    v.vertex = o.object_position;

    o.pos = float4((o.uv * 2.0f - 1.0f) * float2(1, -1), vert.uv.z, 1);
    return o;
#else

#if defined(DK_USE_LIGHTPROBES) && defined(DK_FORWARDBASE_PASS)
    float3 worldNormal = UnityObjectToWorldNormal(mul((float3x3)_LocalTransform, o.object_normal));
    o.indirect = max(0, ShadeSH9(half4(worldNormal, 1)));
#endif
    v.vertex = localPosition;
    o.pos = UnityObjectToClipPos(v.vertex);
    UNITY_TRANSFER_FOG(o, o.pos);
    TRANSFER_SHADOW(o);
    return o;
#endif
}

fixed4 frag(v2f i) : SV_Target
{
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

    float3 viewDir = mul((float3x3) _LocalTransformInverse, mul(unity_WorldToObject, normalize(_WorldSpaceCameraPos.xyz - i.world_position.xyz))).xyz;
    float3 color = dkSampleColorViewWeightedReprojection(viewDir, i.object_position.xyz, i.object_normal.xyz);
    
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

    float4 c = fixed4(color, 1);
    // apply fog
    UNITY_APPLY_FOG(i.fogCoord, c);
    return c;
}