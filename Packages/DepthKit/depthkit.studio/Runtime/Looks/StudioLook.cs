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

using UnityEngine;
using UnityEngine.Rendering;

namespace Depthkit
{
    [SelectionBase]
    [ExecuteInEditMode]
    [AddComponentMenu("Depthkit/Studio/Built-in RP/Depthkit Studio Built-in Look")]
    public class StudioLook : ProceduralLook
    {
        protected static Shader s_defaultUnlitPhotoLookShader = null;
        protected static Material s_defaultUnlitPhotoLookMaterial = null;

        protected static Material GetDefaultMaterial()
        {
            if (s_defaultUnlitPhotoLookShader == null)
            {
                s_defaultUnlitPhotoLookShader = Shader.Find("Depthkit/Studio/Depthkit Studio Photo Look Built-in RP");
            }

            if (s_defaultUnlitPhotoLookMaterial == null)
            {
                s_defaultUnlitPhotoLookMaterial = new Material(s_defaultUnlitPhotoLookShader);
            }
            return s_defaultUnlitPhotoLookMaterial;
        }

        public override string GetLookName() { return "Depthkit Studio Look"; }

        protected override void SetDataSources()
        {
            if (meshSource == null)
            {
                meshSource = depthkitClip.GetDataSource<StudioMeshSource>();
            }
        }

        protected override void SetDefaults()
        {
            if (lookMaterial == null)
            {
                lookMaterial = GetDefaultMaterial();
            }
            base.SetDefaults();
        }

    }
}