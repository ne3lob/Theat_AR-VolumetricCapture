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
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using System.Runtime.InteropServices;

namespace Depthkit
{
    public delegate void ClipEventHandler();

    [ExecuteInEditMode]
    [DefaultExecutionOrder(-20)]
    [AddComponentMenu("Depthkit/Depthkit Clip")]
    public class Clip : MonoBehaviour, IPropertyTransfer
    {
        [Serializable]
        internal class PerspectiveDataBuffer : SyncedStructuredBuffer<Depthkit.Metadata.StructuredPerspectiveData>
        {
            public PerspectiveDataBuffer(Metadata metadata) :
                base("Depthkit Perspective Data Buffer", 0, Depthkit.Metadata.FillPersistentMetadataFromPerspectives(metadata.perspectives))
            { }
        }

        #region Events

        //clip specific events
        public event ClipPlayerEventHandler newFrame;
        public event ClipPlayerEventHandler newPoster;

        private event DataSourceEventHandler m_newMetadata;
        public event DataSourceEventHandler newMetadata
        {
            add
            {
                if (m_newMetadata != null)
                {
                    foreach (DataSourceEventHandler existingHandler in m_newMetadata.GetInvocationList())
                    {
                        if (existingHandler == value)
                        {
                            return;
                        }
                    }
                }
                m_newMetadata += value;
            }
            remove
            {
                if (m_newMetadata != null)
                {
                    foreach (DataSourceEventHandler existingHandler in m_newMetadata.GetInvocationList())
                    {
                        if (existingHandler == value)
                        {
                            m_newMetadata -= value;
                        }
                    }
                }
            }
        }

        protected virtual void OnNewFrame()
        {
            if (newFrame != null) { newFrame(); }
        }
        protected virtual void OnNewMetadata()
        {
            if (m_newMetadata != null) { m_newMetadata(); }
        }
        protected virtual void OnNewPoster()
        {
            if (newPoster != null) { newPoster(); }
        }

        public Depthkit.PlayerEvents playerEvents
        {
            get
            {
                if (player != null)
                {
                    return player.events;
                }
                Debug.LogError("Unable to access events as player is currently null");
                return null;
            }
        }

        #endregion

        #region Metadata

        public enum MetadataSourceType
        {
            TextAsset,
            FilePath,
            StreamingAssetPath,
            JSONString
        }

        /// <summary>
        /// The metadata path. Can be relative to StreamingAssets.</summary>
        [Tooltip("The path to your metadata file. Can be relative to StreamingAssets.")]
        [SerializeField]
        private string m_metadataFilePath;
        public string metadataFilePath
        {
            get
            {
                return m_metadataFilePath;
            }
            set
            {

                if (!string.IsNullOrEmpty(value))
                {
                    string metaDataJson;
                    string path;
                    MetadataSourceType type;

                    if (File.Exists(value))
                    {
                        metaDataJson = System.IO.File.ReadAllText(value);
                        type = MetadataSourceType.FilePath;
                        path = value;
                    }
                    else if (File.Exists(Path.Combine(Application.streamingAssetsPath, value)))
                    {
                        string relPath = Path.Combine(Application.streamingAssetsPath, value);
                        metaDataJson = System.IO.File.ReadAllText(relPath);
                        type = MetadataSourceType.StreamingAssetPath;
                        path = relPath;
                    }
                    else
                    {
                        Debug.LogError("Metadata filepath does not exist: " + value);
                        return;
                    }

                    if (LoadMetadata(metaDataJson))
                    {
                        m_metadataSourceType = type;
                        m_metadataFilePath = path;
                        OnNewMetadata();
                    }
                }
            }
        }

        /// <summary>
        /// The metadata file text asset that corresponds to a given clip. Setting this asset null will clear the current metadata</summary>
        [Tooltip("Your metadata TextAsset file, generated when you bring your metadata file anywhere into Unity's Assets/ folder")]
        [SerializeField]
        private TextAsset m_metadataFile;
        public TextAsset metadataFile
        {
            get
            {
                return m_metadataFile;
            }
            set
            {
                if (value != null)
                {
                    string metaDataJson = value.text;
                    if (LoadMetadata(metaDataJson))
                    {
                        m_metadataFile = value;
                        m_metadataSourceType = MetadataSourceType.TextAsset;
                        OnNewMetadata();
                    }
                }
                else
                {
                    //setting this asset null, will clear the current metadata
                    m_metadataFile = null;
                    m_metadata = new Metadata();
                    m_perspectiveDataBuffer.Release();
                }
            }
        }

        /// <summary>
        /// The Type of Metadata file you're provided to the Depthkit renderer</summary>
        [Tooltip("The type of Metadata file you're providing for this clip.")]
        [SerializeField]
        private MetadataSourceType m_metadataSourceType = MetadataSourceType.TextAsset;
        public MetadataSourceType metadataSourceType
        {
            get
            {
                return metadataSourceType;
            }
        }

        [SerializeField]
        private Depthkit.Metadata m_metadata = null;
        public Depthkit.Metadata metadata
        {
            get
            {
                return m_metadata;
            }
        }

        public bool hasMetadata
        {
            get
            {
                return m_metadata.Valid();
            }
        }
        public bool LoadMetadata(string metaDataJson)
        {
            if (metaDataJson == "")
            {
                return false;
            }

            try
            {
                m_metadata = Depthkit.Metadata.CreateFromJSON(metaDataJson);
            }
            catch (System.Exception)
            {
                Debug.LogError("Invalid Depthkit Metadata Format. Make sure you are using the proper metadata export from Depthkit.");
                m_metadata = new Metadata();
                return false;
            }

            //TODO have the player's subscribe to the clip's event.
            if (m_player != null)
            {
                m_player.OnMetadataUpdated(m_metadata);
            }

            m_doResizeData = true;
            m_doGenerateData = true;

            EnsurePerspectiveDataBuffer();

            return true;
        }

        [SerializeField, HideInInspector]
        private PerspectiveDataBuffer m_perspectiveDataBuffer;

        private void EnsurePerspectiveDataBuffer()
        {
            if (hasMetadata) m_perspectiveDataBuffer = new PerspectiveDataBuffer(m_metadata);
        }

        public ComputeBuffer perspectiveDataBuffer
        {
            get
            {
                return m_perspectiveDataBuffer?.buffer;
            }
        }

        #endregion

        #region Player

        [SerializeField]
        private Depthkit.ClipPlayer m_player;
        public Depthkit.ClipPlayer player
        {
            get
            {
                return m_player;
            }
        }
        private void CreatePlayer(Type type)
        {
            //destroy the components that player references
            //use a for loop to get around the component potentially shifting in the event of an undo
            Depthkit.ClipPlayer[] attachedPlayers = GetComponents<Depthkit.ClipPlayer>();
            for (int i = 0; i < attachedPlayers.Length; i++)
            {
                attachedPlayers[i].RemoveComponents();
            }

            m_player = null;

            m_player = gameObject.AddComponent(type) as Depthkit.ClipPlayer;

            player.CreatePlayer();
        }

        public void SetPlayer<T>() where T : ClipPlayer
        {
            m_lastFrame = -1;

            m_player = gameObject.GetComponent<T>();

            if (player != null)
            {
                return;
            }

            CreatePlayer(typeof(T));
        }

        public void SetPlayer(Type type)
        {
            m_lastFrame = -1;

            m_player = gameObject.GetComponent(type) as ClipPlayer;

            if (player != null)
            {
                return;
            }

            CreatePlayer(type);
        }

        public bool playerSetup
        {
            get
            {
                return m_player != null && m_player.IsPlayerSetup();
            }
        }

        private int m_lastFrame = -1;
        public bool playerIsActive
        {
            get
            {
                return (Application.isPlaying || player.IsPlaying()) && isSetup;
            }
        }

        public uint width
        {
            get
            {
                return playerSetup ? player.GetVideoWidth() : 0;
            }
        }

        public uint height
        {
            get
            {
                return playerSetup ? player.GetVideoHeight() : 0;
            }
        }

        public GammaCorrection gammaCorrectDepth
        {
            get
            {
                if (playerIsActive || disablePoster)
                {
                    return player.GammaCorrectDepth();
                }

                return QualitySettings.activeColorSpace == ColorSpace.Linear ? GammaCorrection.LinearToGammaSpace : GammaCorrection.None;
            }
        }

        public GammaCorrection gammaCorrectColor
        {
            get
            {
                return (playerIsActive || disablePoster) ? player.GammaCorrectColor() : GammaCorrection.None;
            }
        }

        private Texture m_currentCPPTexture;
        public Texture cppTexture
        {
            get
            {
                if (!playerIsActive && !disablePoster && player.SupportsPosterFrame())
                {
                    return poster;
                }
                return m_currentCPPTexture;
            }
        }

        public bool textureIsFlipped
        {
            get
            {
                return (playerIsActive || disablePoster) ? player.IsTextureFlipped() : false;
            }
        }

        #endregion

        #region Poster

        [SerializeField]
        private Texture2D m_poster;

        public Texture2D poster
        {
            get
            {
                return m_poster;
            }
            set
            {
                m_poster = value;
                if (m_poster != null)
                {
                    m_doGenerateData = true;
                    OnNewPoster();
                }
            }
        }

        [SerializeField]
        private bool m_disablePoster = false;
        public bool disablePoster
        {
            get
            {
                return m_disablePoster;
            }
            set
            {
                if (value != m_disablePoster)
                {
                    m_disablePoster = value;
                    m_doGenerateData = true;
                }
            }
        }

        #endregion

        #region DataSource

        private List<WeakReference> m_dataSourceRoots;

        private bool m_doResizeData = false;
        private bool m_doGenerateData = false;

        //if the clip doesn't have the generator it will add it
        public T GetDataSource<T>(bool create = true) where T : Depthkit.DataSource
        {
            if (m_dataSourceRoots == null)
            {
                ResetDataSources();
            }
            else
            {
                m_dataSourceRoots.RemoveAll(x => x.Target == null);
            }

            //check if we already had it cached
            foreach (var dataSource in m_dataSourceRoots)
            {
                var g = dataSource.Target as Depthkit.DataSource;
                if (g != null)
                {
                    if (dataSource.Target.GetType().Equals(typeof(T)))
                    {
                        return dataSource.Target as T;
                    }
                }
            }
            if (!create) return null;
            //check if its just not cached yet
            T gen = gameObject.GetComponent<T>();
            if (gen == null)
            {
                //add it
                gen = gameObject.AddComponent<T>();
                gen.ScheduleGenerate();
            }
            //cache it
            m_dataSourceRoots.Add(new WeakReference(gen));
            return gen;
        }

        internal bool DoResize()
        {
            if (!hasMetadata || cppTexture == null)
            {
                return false;
            }
            foreach (var root in m_dataSourceRoots)
            {
                var gen = root.Target as Depthkit.DataSource;
                if (gen != null) gen.Resize();
            }
            return true;
        }

        internal bool DoGenerate()
        {
            if (!hasMetadata || cppTexture == null)
            {
                return false;
            }

            foreach (var root in m_dataSourceRoots)
            {
                var gen = root.Target as Depthkit.DataSource;
                if (gen != null)
                {
                    gen.Generate();
                }
            }
            m_dataSourceRoots.RemoveAll(x => x.Target == null);
            return true;
        }

        internal void ResetDataSources()
        {
            m_dataSourceRoots = new List<WeakReference>();
            var datasources = GetComponents<Depthkit.DataSource>();
            foreach (var gen in datasources)
            {
                if (gen.dataSourceParent == "root")
                {
                    m_dataSourceRoots.Add(new WeakReference(gen));
                }
            }
        }

        #endregion

        #region ShaderProps

        private static readonly float s_edgeChoke = 0.25f;

        internal static class ShaderIds
        {
            internal static readonly int
                _CPPTexture = Shader.PropertyToID("_CPPTexture"),
                _CPPTexture_TexelSize = Shader.PropertyToID("_CPPTexture_TexelSize"),
                _EdgeChoke = Shader.PropertyToID("_EdgeChoke"),
                _TextureFlipped = Shader.PropertyToID("_TextureFlipped"),
                _ColorSpaceCorrectionDepth = Shader.PropertyToID("_ColorSpaceCorrectionDepth"),
                _ColorSpaceCorrectionColor = Shader.PropertyToID("_ColorSpaceCorrectionColor"),
                _PerspectivesCount = Shader.PropertyToID("_PerspectivesCount"),
                _PerspectivesInX = Shader.PropertyToID("_PerspectivesInX"),
                _PerspectivesInY = Shader.PropertyToID("_PerspectivesInY"),
                _PerspectiveDataStructuredBuffer = Shader.PropertyToID("_PerspectiveDataStructuredBuffer");
        }

        public void SetProperties(ref ComputeShader compute, int kernel)
        {
            if (cppTexture == null) return;
            compute.SetTexture(kernel, ShaderIds._CPPTexture, cppTexture);
            compute.SetInt(ShaderIds._TextureFlipped, textureIsFlipped ? 1 : 0);
            compute.SetInt(ShaderIds._ColorSpaceCorrectionDepth, (int)gammaCorrectDepth);
            compute.SetInt(ShaderIds._ColorSpaceCorrectionColor, (int)gammaCorrectColor);
            compute.SetInt(ShaderIds._PerspectivesCount, metadata.perspectivesCount);
            compute.SetInt(ShaderIds._PerspectivesInX, metadata.numColumns);
            compute.SetInt(ShaderIds._PerspectivesInY, metadata.numRows);
            compute.SetFloat(ShaderIds._EdgeChoke, s_edgeChoke);
            if (perspectiveDataBuffer != null)
                compute.SetBuffer(kernel, ShaderIds._PerspectiveDataStructuredBuffer, perspectiveDataBuffer);
        }

        public void SetProperties(ref Material material)
        {
            if (cppTexture == null) return;
            material.SetTexture(ShaderIds._CPPTexture, cppTexture);
            material.SetInt(ShaderIds._TextureFlipped, textureIsFlipped ? 1 : 0);
            material.SetInt(ShaderIds._ColorSpaceCorrectionDepth, (int)gammaCorrectDepth);
            material.SetInt(ShaderIds._ColorSpaceCorrectionColor, (int)gammaCorrectColor);
            material.SetInt(ShaderIds._PerspectivesCount, metadata.perspectivesCount);
            material.SetInt(ShaderIds._PerspectivesInX, metadata.numColumns);
            material.SetInt(ShaderIds._PerspectivesInY, metadata.numRows);
            material.SetFloat(ShaderIds._EdgeChoke, s_edgeChoke);
            if (perspectiveDataBuffer != null)
                material.SetBuffer(ShaderIds._PerspectiveDataStructuredBuffer, perspectiveDataBuffer);
        }

        public void SetProperties(ref Material material, ref MaterialPropertyBlock block)
        {
            if (cppTexture == null) return;
            block.SetTexture(ShaderIds._CPPTexture, cppTexture);
            block.SetInt(ShaderIds._TextureFlipped, textureIsFlipped ? 1 : 0);
            block.SetInt(ShaderIds._ColorSpaceCorrectionDepth, (int)gammaCorrectDepth);
            block.SetInt(ShaderIds._ColorSpaceCorrectionColor, (int)gammaCorrectColor);
            block.SetInt(ShaderIds._PerspectivesCount, metadata.perspectivesCount);
            block.SetInt(ShaderIds._PerspectivesInX, metadata.numColumns);
            block.SetInt(ShaderIds._PerspectivesInY, metadata.numRows);
            block.SetFloat(ShaderIds._EdgeChoke, s_edgeChoke);
            if (perspectiveDataBuffer != null)
                block.SetBuffer(ShaderIds._PerspectiveDataStructuredBuffer, perspectiveDataBuffer);
        }

        #endregion

        public bool isSetup
        {
            get
            {
                return playerSetup && hasMetadata;
            }
        }

#if UNITY_EDITOR
        void OnAssemblyReload()
        {
            EnsurePerspectiveDataBuffer();
        }
#endif

        void OnEnable()
        {
#if UNITY_EDITOR
            AssemblyReloadEvents.afterAssemblyReload += OnAssemblyReload;
            EditorApplication.update += Update;
#endif
            if (m_dataSourceRoots == null)
            {
                ResetDataSources();
            }
            else
            {
                m_dataSourceRoots.RemoveAll(x => x.Target == null);
            }
            foreach (var root in m_dataSourceRoots)
            {
                var gen = root.Target as Depthkit.DataSource;
                if (gen != null)
                {
                    gen.hideFlags = HideFlags.None;
                }
            }
            m_doGenerateData = true;
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            AssemblyReloadEvents.afterAssemblyReload -= OnAssemblyReload;
            EditorApplication.update -= Update;
#endif
            m_perspectiveDataBuffer?.Release();
            if (m_dataSourceRoots == null) return;
            foreach (var root in m_dataSourceRoots)
            {
                var gen = root.Target as Depthkit.DataSource;
                if (gen != null)
                {
                    gen.hideFlags = HideFlags.NotEditable;
                }
            }
        }

        void Start()
        {
            if (m_player == null)
            {
                SetPlayer<UnityVideoPlayer>();
            }
            ResetDataSources();
            m_doResizeData = true;
            m_doGenerateData = true;
            EnsurePerspectiveDataBuffer();
        }

        void Update()
        {
            bool hasNewFrame = false;

            if (isSetup)
            {
                int frame = player.GetCurrentFrame();
                if (frame != -1 && m_lastFrame != frame)
                {
                    m_currentCPPTexture = player.GetTexture();
                    m_lastFrame = frame;
                    m_doGenerateData = true;
                    hasNewFrame = true;
                }
            }

            if (hasNewFrame)
            {
                OnNewFrame();
            }
#if UNITY_EDITOR
            if (isSetup && !Application.isPlaying && player.IsPlaying())
            {
                // Ensure continuous Update calls.
                UnityEditor.EditorApplication.QueuePlayerLoopUpdate();
            }
#endif
        }

        void LateUpdate()
        {
            if (isSetup)
            {
                m_perspectiveDataBuffer.Sync();

                if (m_doResizeData)
                {
                    m_doResizeData = !DoResize();
                }

                if (m_doGenerateData)
                {
                    m_doGenerateData = !DoGenerate();
                }
            }
        }

        void OnDestroy()
        {
            if (m_dataSourceRoots == null) return;
            foreach (var root in m_dataSourceRoots)
            {
                var gen = root.Target as Depthkit.DataSource;
                if (gen != null)
                {
                    gen.Cleanup();
                }
            }
        }

        void OnApplicationQuit()
        {
            if (player != null)
            {
                player.Stop();
            }
        }
    }
}
