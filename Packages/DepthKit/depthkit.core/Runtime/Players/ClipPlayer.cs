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
using System.Collections;

namespace Depthkit
{

    /// <summary>
    /// The base class that any Depthkit player implementation will derrive from </summary>
    /// <remarks>
    /// This class provides methods that are implemented in child classes to allow
    /// a way for clip to generically interact with a given player backend. </remarks>

    [RequireComponent(typeof(Depthkit.Clip))]
    [ExecuteInEditMode]
    public abstract class ClipPlayer : MonoBehaviour
    {
        public bool videoLoaded { get; protected set; }

        [HideInInspector]
        public Depthkit.PlayerEvents events = new Depthkit.PlayerEvents();

        /// <summary>
        /// creates the appropriate player
        /// </summary>
        public abstract void CreatePlayer();

        /// <summary>
        /// returns player created status
        /// </summary>
        public abstract bool IsPlayerCreated();

        /// <summary>
        /// returns true if the video player has a video configured and is ready to play
        /// </summary>
        public abstract bool IsPlayerSetup();

        /// <summary>
        /// Load the implemented player video.
        /// </summary>
        public abstract IEnumerator Load();

        /// <summary>
        /// Method to dispatch the video loader to start </summary>
        public abstract void StartVideoLoad();

        /// <summary>
        /// Load a video and then play through the implemented player.</summary>
        public abstract IEnumerator LoadAndPlay();

        /// <summary>
        /// Sets the video from a path. Assumed absolute file path.</summary>
        public abstract void SetVideoPath(string path);

        /// <summary>
        /// Get the absolute path to the video.</summary>
        public abstract string GetVideoPath();

        /// <summary>
        /// Callback for metadata updated on the clip.</summary>
        public abstract void OnMetadataUpdated(Depthkit.Metadata metadata);

        /// <summary>
        /// Play through the implemented player. Worth using in combination with VideoLoaded to ensure playback will start when called. </summary>
        public abstract void Play();

        /// <summary>
        /// Pause through the implemented player. </summary>
        public abstract void Pause();

        /// <summary>
        /// Stop playback through the player. </summary>
        public abstract void Stop();

        /// <summary>
        /// Remove the player components from this GameObject. </summary>
        public abstract void RemoveComponents();

        /// <summary>
        /// Return the texture for Depthkit to use by Renderers </summary>
        public abstract Texture GetTexture();

        /// <summary>
        /// Returns if texture generated is flipped </summary>
        public abstract bool IsTextureFlipped();

        /// <summary>
        /// Returns if shader needs to gamma correct depth value on this texture</summary>
        public abstract GammaCorrection GammaCorrectDepth();

        /// Returns if shader needs to gamma correct color value on this shader</summary>
        public abstract GammaCorrection GammaCorrectColor();

        /// <summary>
        /// Return the type name of player being used </summary>
        public abstract string GetPlayerTypeName();

        /// <summary>
        /// Return the pretty name of player being used </summary>
        public static string GetPlayerPrettyName(){ return "Must Provide this function"; }

        /// <summary>
        /// Check if video is playing right now or not </summary>
        public abstract bool IsPlaying();

        /// <summary>
        /// Get the current playback time of the video in seconds. </summary>
        public abstract double GetCurrentTime();

        /// <summary>
        /// Get the current playback frame of the video. </summary>
        public abstract int GetCurrentFrame();

        /// <summary>
        /// Get duration of video in seconds </summary>
        public abstract double GetDuration();

        /// <summary>
        /// Seek to a time point in the video in seconds </summary>
        public abstract void Seek(float toTimeSeconds);

        public abstract uint GetVideoWidth();

        public abstract uint GetVideoHeight();

        public abstract bool SupportsPosterFrame();

    }
}
