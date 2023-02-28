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
using System.Linq;
using System.Collections.Generic;
using System;

namespace Depthkit
{
    [CustomEditor(typeof(Depthkit.Clip))]
    public class ClipEditor : Editor
    {
        bool m_showAdvanced = false;
        bool m_platformValid = true;
        List<string> m_invalidReasons = new List<string>();

        void OnEnable()
        {
            if(AvailablePlayers.Empty())
            {
                AvailablePlayers.UpdatePlayers();
            }

            m_platformValid = Info.IsPlatformValid(ref m_invalidReasons);
        }

        public override void OnInspectorGUI()
        {
            //update the object with the object variables
            serializedObject.Update();

            //set the clip var as the target of this inspector
            Depthkit.Clip clip = (Depthkit.Clip)target;

            if (!m_platformValid)
            {
                EditorGUILayout.BeginVertical();
                GUI.backgroundColor = Color.red;
                EditorGUILayout.HelpBox("Depthkit Unity Plugin is incompatible with this platform", MessageType.Error);
                foreach (string reason in m_invalidReasons)
                {
                    EditorGUILayout.HelpBox(reason, MessageType.Error);
                }

                EditorGUILayout.EndVertical();
                GUI.backgroundColor = Color.white;
            }

            EditorGUILayout.BeginVertical("Box");
            {
                // PLAYER INFO
                SetupPlayerGUI(clip);
                SetupMetadataGUI(clip);
                SetupPosterGUI(clip);
                EditorGUILayout.Space();
                CheckClipSetup(clip);
            }
            EditorGUILayout.EndVertical();
            m_showAdvanced = EditorGUILayout.Foldout(m_showAdvanced, "Advanced Settings");
            if(m_showAdvanced)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(new GUIContent("Disable Poster", "Disabling the poster will cause the clip not to show anything unless playing. Disable the poster to control the video player via a Timeline Playable."), GUILayout.Width(EditorGUIUtility.labelWidth));
                bool disable = clip.disablePoster;
                bool newDisable = EditorGUILayout.Toggle(disable);
                if(disable != newDisable)
                {
                    Undo.RecordObject(clip, "Set Disable Poster");
                    clip.disablePoster = newDisable;
                    EditorUtility.SetDirty(clip);
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.Space();
            // APPLY PROPERTY MODIFICATIONS
            serializedObject.ApplyModifiedProperties();
        }

        void CheckClipSetup(Depthkit.Clip clip)
        {
            if (clip.isSetup)
            {
                GUI.backgroundColor = Color.green;
                EditorGUILayout.BeginVertical();
                
                EditorGUILayout.HelpBox("Depthkit clip is setup and ready for playback",
                                        MessageType.Info);
            }
            else
            {
                GUI.backgroundColor = Color.red;
                EditorGUILayout.BeginVertical();
                
                EditorGUILayout.HelpBox("Depthkit clip is not setup. \n"
                                        + string.Format("Player Setup: {0} | Metadata Setup: {1}",
                                        clip.playerSetup, clip.hasMetadata),
                                        MessageType.Error);
            }
            EditorGUILayout.EndVertical();
            GUI.backgroundColor = Color.white;
        }

        void SetupPlayerGUI(Depthkit.Clip clip)
        {
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            int choiceIndex = -1;
            ClipPlayer player = clip.player;

            if(player != null)
            {
                for (int i = 0; i < AvailablePlayers.Types.Count; ++i)
                {
                    if (AvailablePlayers.Types[i] == player.GetPlayerTypeName())
                    {
                        choiceIndex = i;
                        break;
                    }
                }
            }
            else
            {
                choiceIndex = AvailablePlayers.DefaultPlayerIndex;
            }

            int finalChoiceIndex = EditorGUILayout.Popup("Player", choiceIndex, AvailablePlayers.PrettyNames.ToArray());
            
            if (finalChoiceIndex < 0 || finalChoiceIndex >= AvailablePlayers.PrettyNames.Count)
                finalChoiceIndex = AvailablePlayers.DefaultPlayerIndex;

            if (finalChoiceIndex != choiceIndex)
            {
                clip.SetPlayer(Type.GetType(AvailablePlayers.AssemblyNames[finalChoiceIndex]));
                EditorUtility.SetDirty(clip);
            }
        }

        void SetupMetadataGUI(Depthkit.Clip clip)
        {
            //TODO other metadata asset types
            EditorGUI.BeginChangeCheck();
            TextAsset metadataFile = EditorGUILayout.ObjectField("Metadata", clip.metadataFile, typeof(TextAsset), false) as TextAsset;
            if(EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(clip, "Set Metadata File");
                clip.metadataFile = metadataFile;
                EditorUtility.SetDirty(clip);
            }
        }

        void SetupPosterGUI(Depthkit.Clip clip)
        {
            Texture2D poster = clip.poster;
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PrefixLabel("Poster");
            Texture2D newPoster = EditorGUILayout.ObjectField(poster, typeof(Texture2D), false) as Texture2D;
            if (poster != newPoster)
            {
                Undo.RecordObject(clip, "Set Poster");
                clip.poster = newPoster;
                EditorUtility.SetDirty(clip);
            }
            EditorGUILayout.EndHorizontal();
        }
    }
}
