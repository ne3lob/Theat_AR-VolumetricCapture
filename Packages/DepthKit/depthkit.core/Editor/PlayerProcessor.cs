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
using System.Collections.Generic;
using System.Reflection;
using System;

namespace Depthkit
{
#if UNITY_EDITOR
    public static class AvailablePlayers
    {
        static AvailablePlayers() 
        {
            Types = new List<string>();
            AssemblyNames = new List<string>();
            PrettyNames = new List<string>();
        }
        public static List<string> Types;
        public static List<string> PrettyNames;
        public static List<string> AssemblyNames;
        public static int DefaultPlayerIndex;

        public static bool Empty(){ return AvailablePlayers.Types.Count == 0; }

        public static void UpdatePlayers()
        {
            AvailablePlayers.Types.Clear();
            AvailablePlayers.PrettyNames.Clear();
            AvailablePlayers.AssemblyNames.Clear();
            var players = Reflector.GetDerivedTypeSet<Depthkit.ClipPlayer>();
            int index = 0;
            foreach(KeyValuePair<string, string> player in players)
            {
                if(player.Key == typeof(Depthkit.UnityVideoPlayer).Name)
                {
                    AvailablePlayers.DefaultPlayerIndex = index;
                }
                Type t = Type.GetType(player.Value);
                AvailablePlayers.PrettyNames.Add(t.GetMethod("GetPlayerPrettyName").Invoke(null, null) as string);
                AvailablePlayers.Types.Add(player.Key);
                AvailablePlayers.AssemblyNames.Add(player.Value);
                ++index;
            }
        }
    }

    public class Depthkit_PlayerProcessor : UnityEditor.AssetModificationProcessor
    {
        public static string[] OnWillSaveAssets(string[] paths)
        {
            AvailablePlayers.UpdatePlayers();
            return paths;
        }

        static void OnWillCreateAsset(string name)
        {
            AvailablePlayers.UpdatePlayers();
        }

        static AssetDeleteResult OnWillDeleteAsset(string name, RemoveAssetOptions options)
        {
            AvailablePlayers.UpdatePlayers();
            return AssetDeleteResult.DidNotDelete;
        }
    }
#endif
}