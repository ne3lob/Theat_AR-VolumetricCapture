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
using System;
using System.Collections;

namespace Depthkit
{
    /// <summary>
    /// Implementation of the Depthkit player with the Unity VideoPlayer-based backend.
    /// </summary>
    [AddComponentMenu("Depthkit/Players/Depthkit Video Player (Unity)")]
    public class UnityVideoPlayer : Depthkit.ClipPlayer
    {
        //reference to the MovieTexture passed in through Clip
        [SerializeField, HideInInspector]
        protected UnityEngine.Video.VideoPlayer m_mediaPlayer;
        [SerializeField, HideInInspector]
        protected AudioSource m_audioSource;

        public override void CreatePlayer()
        {
            m_mediaPlayer = gameObject.GetComponent<UnityEngine.Video.VideoPlayer>();

            if (m_mediaPlayer == null)
            {
                m_mediaPlayer = gameObject.AddComponent<UnityEngine.Video.VideoPlayer>();
            }

            m_audioSource = gameObject.GetComponent<AudioSource>();

            if (m_audioSource == null)
            {
                m_audioSource = gameObject.AddComponent<AudioSource>();
            }

            m_mediaPlayer.audioOutputMode = UnityEngine.Video.VideoAudioOutputMode.AudioSource;
            m_mediaPlayer.SetTargetAudioSource(0, m_audioSource);
            m_mediaPlayer.renderMode = UnityEngine.Video.VideoRenderMode.APIOnly;
            m_mediaPlayer.prepareCompleted += OnVideoLoadingComplete;
            m_mediaPlayer.EnableAudioTrack(0, true);
        }

        public override bool IsPlayerCreated()
        {
            return m_mediaPlayer != null;
        }

        public override bool IsPlayerSetup()
        {
            if(!IsPlayerCreated())
            {
                return false;
            }

            if(m_mediaPlayer.source == UnityEngine.Video.VideoSource.VideoClip)
            {
                return m_mediaPlayer.clip != null;
            }
            else
            {
                return m_mediaPlayer.url != null && m_mediaPlayer.url != "";
            }
        }

        /// <summary>
        /// Sets the video from a path. Assumed relative to data folder file path.</summary>
        public override void SetVideoPath(string path)
        {
            if (!IsPlayerCreated())
            {
                return;
            }
            // path = path.Replace("\\", "/");
            // if (path.StartsWith(Application.dataPath))
            // {
            //     path = "Assets" + path.Substring(Application.dataPath.Length);
            // }
            // m_mediaPlayer.source = UnityEngine.Video.VideoSource.Url;
            m_mediaPlayer.url = path;
        }

        /// <summary>
        /// Get the absolute path to the video.</summary>
        public override string GetVideoPath()
        {
            if (!IsPlayerSetup())
            {
                return "";
            }

            if (m_mediaPlayer.source == UnityEngine.Video.VideoSource.VideoClip)
            {
                return m_mediaPlayer.clip.originalPath;
            }
            else
            {
                return m_mediaPlayer.url;
            }
        }

        public override void StartVideoLoad()
        {
            
            StartCoroutine(Load());
        }

        public override IEnumerator Load()
        {
            events.OnClipLoadingStarted();
            m_mediaPlayer.Prepare();
            yield return null;
        }

        public void OnVideoLoadingComplete(UnityEngine.Video.VideoPlayer player)
        {
            videoLoaded = true;
            events.OnClipLoadingFinished();
        }

        public override void OnMetadataUpdated(Depthkit.Metadata metadata){ /* do nothing */ }

        public override IEnumerator LoadAndPlay()
        {
            StartVideoLoad();
            while (!videoLoaded)
            {
                yield return null;
            }
            Play();
            yield return null;
        }

        public override void Play()
        {
            m_mediaPlayer.Play();
            events.OnClipPlaybackStarted();
        }
        public override void Pause()
        {
            m_mediaPlayer.Pause();
            events.OnClipPlaybackPaused();
        }
        public override void Stop()
        {
            m_mediaPlayer.Stop();
            events.OnClipPlaybackStopped();
        }

        public override int GetCurrentFrame()
        {
            return (int)m_mediaPlayer.frame;
        }
        public override double GetCurrentTime()
        {
            return m_mediaPlayer.time;
        }

        public override double GetDuration()
        {
            return m_mediaPlayer.clip.length;
        }

        public override Texture GetTexture()
        {
            return m_mediaPlayer.texture;
        }
        public override bool IsTextureFlipped ()
        {
            return false;
        }
        public override GammaCorrection GammaCorrectDepth()
        {
            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
            {
#if UNITY_2018_2_OR_NEWER
                return GammaCorrection.LinearToGammaSpace;
#elif UNITY_2017_1_OR_NEWER
                //https://issuetracker.unity3d.com/issues/regression-videos-are-dark-when-using-linear-color-space?page=1
                Debug.LogWarning("Video Player (Unity) does not display correct color on Windows between version 2017.1 and 2018.2. Use AVPro, switch to Gamma Color Space, or upgrade Unity to use Depthkit with this project.");
                return GammaCorrection.LinearToGammaSpace2x;
#else
                return GammaCorrection.LinearToGammaSpace;
#endif
            }
            else
            {
                return GammaCorrection.None;
            }
        }
        public override GammaCorrection GammaCorrectColor()
        {
            if (QualitySettings.activeColorSpace == ColorSpace.Linear)
            {
#if UNITY_2018_2_OR_NEWER
                return GammaCorrection.None;
#elif UNITY_2017_1_OR_NEWER
                return GammaCorrection.LinearToGammaSpace;
#else
                return GammaCorrection.None;
#endif
            }
            else
            {
                return GammaCorrection.None;
            }
        }
        public override bool IsPlaying()
        {
            return m_mediaPlayer.isPlaying;
        }

        public override void RemoveComponents()
        {
            if(!Application.isPlaying)
            {
                DestroyImmediate(m_mediaPlayer, true);
                DestroyImmediate(m_audioSource, true);
                DestroyImmediate(this, true);
            }
            else
            {
                Destroy(m_mediaPlayer);
                Destroy(m_audioSource);
                Destroy(this);
            }
        }

        public override string GetPlayerTypeName()
        {
            return typeof(Depthkit.UnityVideoPlayer).Name;
        }

        public new static string GetPlayerPrettyName()
        {
            return "Video Player (Unity)";
        }

        public UnityEngine.Video.VideoPlayer GetPlayerBackend()
        {
            return m_mediaPlayer;
        }

        public override void Seek(float toTime)
        {
            if (m_mediaPlayer == null)
            {
                return;
            }

            m_mediaPlayer.time = toTime;
        }

        public override uint GetVideoWidth()
        {
            return m_mediaPlayer != null ? m_mediaPlayer.width : 0;
        }
        public override uint GetVideoHeight()
        {
            return m_mediaPlayer != null ? m_mediaPlayer.height : 0;
        }

        public override bool SupportsPosterFrame()
        {
            return true;
        }
    }
}
