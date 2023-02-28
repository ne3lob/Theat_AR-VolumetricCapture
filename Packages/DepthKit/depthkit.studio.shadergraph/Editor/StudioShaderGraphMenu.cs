/************************************************************************************

Depthkit Unity SDK License v1
Copyright 2016-2019 Scatter All Rights reserved.  

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
using UnityEditor;

namespace Depthkit {
    public class StudioShaderGraphMenu
    {
        [MenuItem ("Assets/Create/Shader/Depthkit/Studio Look - Shader Graph")]
        static void CreateDepthkitStudioSRPLookShaderGraph(MenuCommand menuCommand)
        {
           string filename;
            if(Selection.assetGUIDs.Length > 0)
            {
                string clickedAssetGuid = Selection.assetGUIDs[0];
                string clickedPath      = AssetDatabase.GUIDToAssetPath(clickedAssetGuid); 
                filename = Depthkit.Util.GetNextFileName(clickedPath + "/MyStudioLook", ".shadergraph");
            }
            else
            {
                filename = Depthkit.Util.GetNextFileName("Assets/MyStudioLook", ".shadergraph");
            }
            AssetDatabase.CopyAsset("Packages/nyc.scatter.depthkit.studio.shadergraph/Runtime/Resources/Shaders/Depthkit Studio Photo Look Unlit.shadergraph", filename);
        }
    }
}
