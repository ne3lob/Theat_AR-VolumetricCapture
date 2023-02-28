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

Shader "Depthkit/GenerateNormalWeights"{

    Properties{
        [HideInInspector]_MainTex ("Texture", 2D) = "white" {}
    }

    SubShader{
        // markers that specify that we don't need culling 
        // or reading/writing to the depth buffer
        Cull Off
        ZWrite Off 
        ZTest Always
        
        //horizontal
        Pass {
            CGPROGRAM
            #include "UnityCG.cginc"
            #define DK_USE_BUILT_IN_COLOR_CONVERSION
            #include "Packages/nyc.scatter.depthkit.core/Runtime/Resources/Shaders/Includes/Depthkit.cginc"
            
            #pragma vertex vert
            #pragma fragment frag

            sampler2D _MainTex;
            float4 _NormalTexture_TexelSize;
            
            //the object data that's put into the vertex shader
            struct appdata{
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            //the data that's used to generate fragments and can be read by the fragment shader
            struct v2f{
                float4 position : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v){
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_TARGET{
                
                float3 positions[5] = {
                    float3(0,0,0),
                    float3(0,0,0),
                    float3(0,0,0),
                    float3(0,0,0),
                    float3(0,0,0)
                 };

                 float2 offsets[5] = {
                    {0,0},
                    {-_NormalTexture_TexelSize.x,0},
                    {0,-_NormalTexture_TexelSize.y}, 
                    {_NormalTexture_TexelSize.x,0},
                    {0,_NormalTexture_TexelSize.y},
                };

                [unroll(5)]
                for (int sample = 0; sample < 5; sample++)
                {
                    float2 uv = i.uv + offsets[sample];
                    float2 colorUV, depthUV;
                    dkGetColorAndDepthUV(uv, colorUV, depthUV);
                    float2 perspectiveUV = dkGetPerspectiveCoordFromPackedUV(uv);
                    uint perspectiveIndex = dkGetPerspectiveIndexFromCoord(perspectiveUV);
                    float depth = dkSampleDepth(depthUV, perspectiveIndex, perspectiveUV);

                    positions[sample] = dkPackedUVToLocal(uv, depth).xyz;
                }

                float3 normal = normalize(
                    cross(positions[1] - positions[0], positions[2] - positions[0]) +
                    cross(positions[3] - positions[0], positions[4] - positions[0]));
                
                return float4(abs(dot(normal, float3(0.0,0.0,1.0))), 0, 0, 1);
            }
            ENDCG
        }
    }
}