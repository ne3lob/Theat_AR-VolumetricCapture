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

Shader "Depthkit/Studio/VolumePreview" 
{
    SubShader
    {
		Tags { "Queue" = "Transparent" "RenderType"="Transparent" }
		LOD 200

        Pass
        {
            Cull Off ZWrite Off
            Fog { Mode off }
            Blend SrcAlpha OneMinusSrcAlpha

            CGPROGRAM
            #include "UnityCG.cginc"
            #pragma target 5.0
            #pragma vertex vert
            #pragma fragment frag
 
            struct Vert
            {
                float3 position;
                float4 color;
            };
 
            StructuredBuffer<Vert> _Points;
            float _SdfAlpha = 0.02;
            float _PointSize = 1.0;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 col : COLOR;
                float size : PSIZE;
            };
 
            v2f vert(uint id : SV_VertexID)
            {
                Vert vert = _Points[id];
                v2f OUT;
                OUT.pos = UnityObjectToClipPos(float4(vert.position, 1));
                OUT.col = vert.color.rgba;
                OUT.size = _PointSize;
                return OUT;
            }
 
            float4 frag(v2f IN) : COLOR
            {
                return float4(IN.col.rgb, IN.col.a * saturate(_SdfAlpha));
            }
 
            ENDCG
 
        }
    }
}
