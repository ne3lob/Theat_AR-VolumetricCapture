// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Depthkit/Studio/Depthkit Studio Photo Look Built-in RP"
{
    Properties
    {
        _ShadowAmount("Shadow Amount", Range(0.0,1.0)) = 1.0
        [Toggle(DK_USE_LIGHTPROBES)] _SampleProbes("Use Light Probes", Float) = 1
        [Toggle(DK_USE_DEBUG_COLOR)] _DebugColor("Debug Per Perspective Color", Float) = 0
        [Toggle(DK_USE_EDGEMASK)] _EdgeMask("Enable Edge Mask", Float) = 0
        [Toggle(DK_DEBUG_EDGEMASK)] _DebugEdgeMask("Show Edge Mask Debug", Float) = 0
        [Toggle(DK_NO_MAIN_LIGHT)] _NoMainLight("Disable Main Directional Shadows", Float) = 0
        [KeywordEnum(INFER, COLORIZE, CLIP)] DK_UNTEXTURED_FRAGMENT("Untextured Geometry Settings", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100
        AlphaToMask On
        Cull Off

        Pass
        {
            Tags {"LightMode" = "ForwardBase"}
            Blend One OneMinusSrcAlpha
            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local DK_USE_LIGHTPROBES
            #pragma shader_feature_local DK_USE_DEBUG_COLOR
            #pragma shader_feature_local DK_USE_EDGEMASK
            #pragma shader_feature_local DK_DEBUG_EDGEMASK
            #pragma shader_feature_local DK_NO_MAIN_LIGHT
            #pragma shader_feature_local DK_UNTEXTURED_FRAGMENT_INFER
            #pragma shader_feature_local DK_UNTEXTURED_FRAGMENT_COLORIZE
            #pragma shader_feature_local DK_UNTEXTURED_FRAGMENT_CLIP
            #pragma shader_feature_local DK_TEXTURE_ATLAS

            // make fog work
            #pragma multi_compile_fog

            #pragma multi_compile_fwdbase
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"

            #define DK_USE_BUILT_IN_COLOR_CONVERSION
            #include "Packages/nyc.scatter.depthkit.studio/Runtime/Resources/Shaders/Includes/DepthkitStudio.cginc"
            #define DK_CORE_PACKED_TRIANGLE
            #include "Packages/nyc.scatter.depthkit.core/Runtime/Resources/Shaders/Includes/SampleCoreTriangles.cginc"
            #define DK_FORWARDBASE_PASS
            #include "Packages/nyc.scatter.depthkit.studio/Runtime/Resources/Shaders/Looks/DepthkitStudioPhotoLook.cginc"

            ENDCG
        }
        Pass
        {
            Tags {"LightMode" = "ForwardAdd"}
            BlendOp Max
            Blend One One
            ZWrite Off

            CGPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #pragma shader_feature_local DK_USE_LIGHTPROBES
            #pragma shader_feature_local DK_USE_DEBUG_COLOR
            #pragma shader_feature_local DK_USE_EDGEMASK
            #pragma shader_feature_local DK_DEBUG_EDGEMASK
            #pragma shader_feature_local DK_UNTEXTURED_FRAGMENT_INFER
            #pragma shader_feature_local DK_UNTEXTURED_FRAGMENT_COLORIZE
            #pragma shader_feature_local DK_UNTEXTURED_FRAGMENT_CLIP
            #pragma shader_feature_local DK_TEXTURE_ATLAS

            // make fog work
            #pragma multi_compile_fog

            #pragma multi_compile_fwdadd_fullshadows
            #include "UnityCG.cginc"
            #include "AutoLight.cginc"

            #define DK_USE_BUILT_IN_COLOR_CONVERSION
            #include "Packages/nyc.scatter.depthkit.studio/Runtime/Resources/Shaders/Includes/DepthkitStudio.cginc"
            #define DK_CORE_PACKED_TRIANGLE
            #include "Packages/nyc.scatter.depthkit.core/Runtime/Resources/Shaders/Includes/SampleCoreTriangles.cginc"
            #include "Packages/nyc.scatter.depthkit.studio/Runtime/Resources/Shaders/Looks/DepthkitStudioPhotoLook.cginc"

            ENDCG
        }
        Pass {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
        
            Fog {Mode Off}
            ZWrite On ZTest LEqual Cull Off
            Offset 1, 1
    
            CGPROGRAM
            #pragma vertex caster_vert
            #pragma fragment caster_frag
            #pragma multi_compile_shadowcaster
            #pragma fragmentoption ARB_precision_hint_fastest

            #include "UnityCG.cginc"
            #define DK_USE_BUILT_IN_COLOR_CONVERSION
            #include "Packages/nyc.scatter.depthkit.core/Runtime/Resources/Shaders/Includes/Depthkit.cginc"
            #define DK_CORE_PACKED_TRIANGLE
            #include "Packages/nyc.scatter.depthkit.core/Runtime/Resources/Shaders/Includes/SampleCoreTriangles.cginc"

            float4x4 _LocalTransform;

            struct appdata {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                uint   id : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f {
                V2F_SHADOW_CASTER;
                UNITY_VERTEX_OUTPUT_STEREO
            };
    
            v2f caster_vert( appdata v )
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                Vertex vert;
                vert = dkSampleTriangleBuffer(floor(v.id / 3), v.id % 3);
                v.vertex = mul(_LocalTransform, float4(vert.position, 1)); 
                v.normal = mul((float3x3)_LocalTransform, vert.normal);

                TRANSFER_SHADOW_CASTER_NORMALOFFSET(o)
                return o;
            }
    
            float4 caster_frag( v2f i ) : COLOR
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
                SHADOW_CASTER_FRAGMENT(i)
            }
            ENDCG
    
        }
    }
    Fallback "VertexLit"
}
