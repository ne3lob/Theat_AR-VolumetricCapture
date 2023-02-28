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

namespace Depthkit
{
    public delegate void ClipPlayerEventHandler();

    /// <summary>
    /// Class that contains events a given player could potentially emit for listening. </summary>
    [System.Serializable]
    public class PlayerEvents 
    {
        public event ClipPlayerEventHandler playbackStarted;
        public event ClipPlayerEventHandler playbackPaused;
        public event ClipPlayerEventHandler playbackStopped;
        public event ClipPlayerEventHandler loadingStarted;
        public event ClipPlayerEventHandler loadingFinished;
        
        public virtual void OnClipPlaybackStarted()
        {   
            if(playbackStarted != null) { playbackStarted(); }
        }
 
        public virtual void OnClipPlaybackPaused()
        {   
            if(playbackPaused != null) { playbackPaused(); }
        }

        public virtual void OnClipPlaybackStopped()
        {   
            if(playbackStopped != null) { playbackStopped(); }
        }

        public virtual void OnClipLoadingStarted()
        {
            if(loadingStarted != null) { loadingStarted(); } 
        }

        public virtual void OnClipLoadingFinished()
        {
            if(loadingFinished != null) { loadingFinished(); }
        }
    }
}
