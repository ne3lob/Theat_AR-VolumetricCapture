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
using System.IO;
using System;

namespace Depthkit
{
    public delegate void DepthkitLookEventHandler();

    [DefaultExecutionOrder(-10)]
    [RequireComponent(typeof(BoxCollider))]
    [ExecuteInEditMode]
    public abstract class Look : MonoBehaviour
    {
        protected static class LookCommonShaderIds
        {
            internal static readonly int _LocalTransformInverse = Shader.PropertyToID("_LocalTransformInverse");
            internal static readonly int _LocalTransform = Shader.PropertyToID("_LocalTransform");
            internal static readonly string _DebugKeyword = "DK_USE_DEBUG_COLOR";
        }

        public Clip depthkitClip = null;
        public bool showPerViewColorDebug = false;
        public bool showCameraFrustums = false;
        public DepthkitLookEventHandler onUpdated;

        private MaterialPropertyBlock m_materialPropertyBlock = null;

        protected void EnsureMaterialPropertyBlock()
        {
            if (UsesMaterialPropertyBlock())
            {
                if (m_materialPropertyBlock == null)
                {
                    m_materialPropertyBlock = new MaterialPropertyBlock();
                }
            }
        }

        protected MaterialPropertyBlock materialPropertyBlock
        {
            get
            {
                EnsureMaterialPropertyBlock();
                return m_materialPropertyBlock;
            }
        }

        [HideInInspector]
        public MeshSource meshSource = null;

        /// The bounding box collider</summary>
        [HideInInspector, SerializeField]
        protected BoxCollider m_collider = null;

        private bool m_bIsInit = false;


        public abstract string GetLookName();
        protected abstract bool UsesMaterial();
        protected abstract Material GetMaterial();
        protected virtual bool UsesMaterialPropertyBlock() { return true; }
        protected virtual MaterialPropertyBlock GetMaterialPropertyBlock() { return materialPropertyBlock; }
        protected abstract void SetDataSources();
        protected virtual bool ValidateDataSources()
        {
            return meshSource != null;
        }
        protected virtual void SetMaterialProperties(ref Material material) { }
        protected virtual void SetMaterialProperties(ref Material material, ref MaterialPropertyBlock block) { }

        protected virtual void OnUpdate()
        {
            SetLookProperties();
        }

        protected virtual void SetDefaults()
        {
            EnsureMaterialPropertyBlock();
            m_collider = GetComponent<BoxCollider>();
        }
        protected virtual void SetLookProperties()
        {
            SyncColliderToBounds();

            var material = GetMaterial();
            if (material == null)
            {
                return;
            }

            if (UsesMaterialPropertyBlock())
            {
                MaterialPropertyBlock block = GetMaterialPropertyBlock();
                block.Clear();
                depthkitClip.SetProperties(ref material, ref block);
                meshSource.SetProperties(ref material, ref block);
                Util.EnsureKeyword(ref material, LookCommonShaderIds._DebugKeyword, showPerViewColorDebug);
                block.SetMatrix(LookCommonShaderIds._LocalTransform, transform.localToWorldMatrix);
                block.SetMatrix(LookCommonShaderIds._LocalTransformInverse, transform.localToWorldMatrix.inverse);
                SetMaterialProperties(ref material, ref block);
            }
            else
            {
                depthkitClip.SetProperties(ref material);
                meshSource.SetProperties(ref material);
                Util.EnsureKeyword(ref material, LookCommonShaderIds._DebugKeyword, showPerViewColorDebug);
                material.SetMatrix(LookCommonShaderIds._LocalTransform, transform.localToWorldMatrix);
                material.SetMatrix(LookCommonShaderIds._LocalTransformInverse, transform.localToWorldMatrix.inverse);
                SetMaterialProperties(ref material);
            }
        }

        void Awake()
        {
            if (m_materialPropertyBlock == null)
            {
                m_materialPropertyBlock = new MaterialPropertyBlock();
            }
            if (depthkitClip == null)
            {
                depthkitClip = GetComponentInParent<Depthkit.Clip>();
            }
            SetDefaults();
        }

        protected virtual bool Init()
        {
            if (depthkitClip == null)
            {
                depthkitClip = GetComponent<Depthkit.Clip>();
            }
            if (!ValidateDataSources())
            {
                if (depthkitClip != null)
                {
                    depthkitClip.newMetadata += OnMetaDataUpdated;
                    OnMetaDataUpdated();
                    SetDataSources();
                }
            }
            return depthkitClip != null && ValidateDataSources();
        }

        void Start()
        {
            m_bIsInit = Init();
        }

#if UNITY_EDITOR
        protected virtual void OnAssemblyReload()
        {
            Awake();
        }
#endif

        void OnEnable()
        {
#if UNITY_EDITOR
            AssemblyReloadEvents.afterAssemblyReload += OnAssemblyReload;
#endif
            if (depthkitClip != null)
            {
                depthkitClip.newMetadata += OnMetaDataUpdated;
            }
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            AssemblyReloadEvents.afterAssemblyReload -= OnAssemblyReload;
#endif
            if (depthkitClip != null)
            {
                depthkitClip.newMetadata -= OnMetaDataUpdated;
            }
        }

        protected void SyncColliderToBounds()
        {
            if (meshSource != null)
            {
                Bounds localBounds = meshSource.GetLocalBounds();
                m_collider.center = localBounds.center;
                m_collider.size = localBounds.size;
            }
            else if (depthkitClip != null)
            {
                m_collider.center = depthkitClip.metadata.boundsCenter;
                m_collider.size = depthkitClip.metadata.boundsSize;
            }
        }

        protected virtual void OnMetaDataUpdated()
        {
            SyncColliderToBounds();
        }

        void LateUpdate()
        {
            if (depthkitClip == null) m_bIsInit = false;
            if (!m_bIsInit)
            {
                m_bIsInit = Init();
            }
            else
            {
                if (!depthkitClip.isSetup) return;
                OnUpdate();
                if (onUpdated != null)
                {
                    onUpdated();
                }
            }
        }
    }
}