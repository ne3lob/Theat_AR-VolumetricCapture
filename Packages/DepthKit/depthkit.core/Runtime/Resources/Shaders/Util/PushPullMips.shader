Shader "Depthkit/Util/PushPullMips"
{
    Properties
    {
        _MainTex("Texture", 2D) = "white" {}
        _MipLevel("Mip Levels", Float) = 0.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 100
        ZWrite Off
        Blend Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _MipLevels = 1.0f;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // sample the texture
                float4 dest = float4(0,0,0,0);
                for (float lod = _MipLevels - 1; lod >= 0; lod -= 1)
                {
                    float4 source = tex2Dlod(_MainTex, float4(i.uv, 0, lod));
                    dest = source + (1 - source.a) * dest;
                }
                if (dest.a > 0) {
                    dest.rgb /= dest.a;
                }
                dest.a = 1;

                fixed4 col = dest;

                return col;
            }
            ENDCG
        }
    }
}

