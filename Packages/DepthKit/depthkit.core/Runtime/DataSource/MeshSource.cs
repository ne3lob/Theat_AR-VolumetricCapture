using UnityEngine;
using UnityEngine.XR;
using System.Linq;
using System;
using System.Runtime.InteropServices;

namespace Depthkit
{
    public abstract class MeshSource : DataSource, IPropertyTransfer
    {
        #region SubMeshes

        public bool recalculateCurrentSurfaceTriangleCount = false;

        private SubMesh[] m_subMeshes;

        // Note: this is to serialize the max triangles, as the m_subMeshes array cannot be serialized.
        [SerializeField]
        private uint[] m_subMeshMaxTriangles;

        public SubMesh GetSubMesh(int index)
        {
            if (m_subMeshes == null || index >= m_subMeshes.Length) return null;
            return m_subMeshes[index];
        }

        public T GetSubMesh<T>(int index) where T : SubMesh
        {
            return GetSubMesh(index) as T;
        }

        public SubMesh CurrentSubMesh()
        {
            if (m_subMeshes == null || currentSubmeshIndex >= m_subMeshes.Length) return null;
            return m_subMeshes[currentSubmeshIndex];
        }

        public T CurrentSubMesh<T>() where T : SubMesh
        {
            return CurrentSubMesh() as T;
        }

        public void ReserveSubMeshes<T>(int count) where T : SubMesh, new()
        {
            if (m_subMeshMaxTriangles == null || m_subMeshMaxTriangles.Length != count)
            {
                m_subMeshMaxTriangles = new uint[count];
                for (int idx = 0; idx < count; idx++)
                {
                    m_subMeshMaxTriangles[idx] = 0;
                }
            }

            if (m_subMeshes == null || m_subMeshes.Length != count)
            {
                if (m_subMeshes != null)
                {
                    foreach (var sm in m_subMeshes)
                    {
                        sm.Release();
                    }
                }
                m_subMeshes = Enumerable.Range(0, count).Select(i =>
                {
                    return new T();
                }).ToArray();
            }

            for (int idx = 0; idx < count; idx++)
            {
                m_subMeshes[idx].source = this;
                m_subMeshes[idx].maxTriangles = m_subMeshMaxTriangles[idx];
                m_subMeshes[idx].useTriangleMesh = m_useTriangleMesh;
            }
        }

        #endregion

        #region Properties
        public static class MeshSourceShaderIds
        {
            public static readonly int
                _RadialBiasPerspInMeters = Shader.PropertyToID("_RadialBiasPerspInMeters");
        }

        protected bool m_forceStereo = false;

        protected uint m_currentSubmeshIndex;
        public uint currentSubmeshIndex
        {
            get
            {
                return m_currentSubmeshIndex;
            }
            set
            {
                m_currentSubmeshIndex = value;
            }
        }

        public ComputeBuffer triangleBuffer
        {
            get
            {
                return m_subMeshes == null ? null : m_subMeshes.Length <= m_currentSubmeshIndex ? null : m_subMeshes[m_currentSubmeshIndex].triangleBuffer;
            }
        }

        public ComputeBuffer triangleBufferDispatchIndirectArgs
        {
            get
            {
                return m_subMeshes == null ? null : m_subMeshes.Length <= m_currentSubmeshIndex ? null : m_subMeshes[m_currentSubmeshIndex].dispatchIndirectArgs;
            }
        }

        public ComputeBuffer triangleBufferDrawIndirectArgs
        {
            get
            {
                return m_subMeshes == null ? null : m_subMeshes.Length <= m_currentSubmeshIndex ? null : m_subMeshes[m_currentSubmeshIndex].drawIndirectArgs;
            }
        }

        public uint maxSurfaceTriangles
        {
            get
            {
                return m_subMeshes == null ? 0 : m_subMeshes.Length <= m_currentSubmeshIndex ? 0 : m_subMeshes[m_currentSubmeshIndex].maxTriangles;
            }
            set
            {
                if (m_subMeshes != null)
                {
                    m_subMeshes[m_currentSubmeshIndex].maxTriangles = value;
                    m_subMeshMaxTriangles[m_currentSubmeshIndex] = value;
                    ScheduleResize();
                    ScheduleGenerate();
                }
            }
        }

        [SerializeField, HideInInspector]
        bool m_useTriangleMesh = false;
        public bool useTriangleMesh
        {
            get
            {
                return m_useTriangleMesh;
            }
            set
            {
                if (m_subMeshes != null)
                {
                    foreach (var persp in m_subMeshes)
                    {
                        persp.useTriangleMesh = value;
                    }
                    m_useTriangleMesh = value;
                }
            }
        }

        public TriangleMesh triangleMesh
        {
            get
            {
                return m_subMeshes == null ? null : m_subMeshes[m_currentSubmeshIndex].triangleMesh;
            }
        }

        #endregion

        #region RadialBias
        public const float radialBiasMin = 0.0f;
        public const float radialBiasMax = 2.50f;
        public const float radialBiasDefault = 0.80f;
        [Range(radialBiasMin, radialBiasMax)]
        public float radialBias = radialBiasDefault;
        public float[] radialBiasPersp = null;
        // The datatype for the per perspective bias is a float4 because float arrays get pushed to the shader as 4 component float vectors.
        protected Vector4[] radialBiasPerspInMeters = null;
        void EnsureRadialBias()
        {
            // This way of sizing the radialBiasPersp array will cause multiperspective core clips to be sized to 12
            if (radialBiasPersp == null || radialBiasPersp.Length != clip.metadata.perspectivesCount)
            {
                radialBiasPersp = new float[clip.metadata.perspectivesCount];
                for (int i = 0; i < clip.metadata.perspectivesCount; ++i)
                {
                    radialBiasPersp[i] = radialBiasDefault;
                }
                radialBiasPerspInMeters = null;
            }

            // This way of sizing the radialBiasPersp array will cause multiperspective core clips to be sized to 12
            int size = (clip.metadata.perspectivesCount > 1) ? 12 : 1;
            if (radialBiasPerspInMeters == null || radialBiasPerspInMeters.Length != size)
            {
                Vector4 defaultVal = new Vector4(Util.cmToMeters(MeshSource.radialBiasDefault), 0, 0, 0);
                radialBiasPerspInMeters = new Vector4[size];
                for (int i = 0; i < size; ++i)
                {
                    radialBiasPerspInMeters[i] = defaultVal;
                }
            }
        }
        #endregion

        #region DataSource
        protected override void AcquireResources()
        {
            if (m_subMeshes != null)
            {
                int index = 0;
                foreach (var persp in m_subMeshes)
                {
                    persp.EnsureBuffers(index++);
                }
            }

            if (!m_clip.isSetup) return;
            EnsureRadialBias();
            base.AcquireResources();
        }

        protected override void FreeResources()
        {
            if (m_subMeshes != null)
            {
                foreach (var submesh in m_subMeshes)
                {
                    submesh.Release();
                }
            }
            base.FreeResources();
        }

        protected override bool CanGenerate()
        {
            if (m_subMeshes == null) return false;

            bool usesTriangleMesh = false;
            foreach (var persp in m_subMeshes)
            {
                if (persp.useTriangleMesh) usesTriangleMesh = true;
            }

            if (!usesTriangleMesh && (pauseDataGenerationWhenInvisible || pausePlayerWhenInvisible))
            {
                CheckVisibility();
            }
            return m_doGeneration;
        }

        public override bool OnSetup()
        {
            Util.ArgsBufferPrep.Setup();

            //TODO this is a hack b/c the openVR plugin doesn't properly report XRSettings.stereoRenderingMode 
            if (XRSettings.supportedDevices.Length > 0)
            {
                foreach (var dev in XRSettings.supportedDevices)
                {
                    if (dev.Contains("OpenVR"))
                    {
                        m_forceStereo = true;
                        break;
                    }
                }
            }

            return true;
        }

        protected override bool OnResize()
        {
            if (!m_clip.isSetup || m_clip.metadata.textureWidth == 0 || m_clip.metadata.textureHeight == 0) return true;

            // This way of sizing the radialBiasPersp array will cause multiperspective core clips to be sized to 12
            if (radialBiasPersp == null || radialBiasPersp.Length != clip.metadata.perspectivesCount)
            {
                radialBiasPersp = new float[clip.metadata.perspectivesCount];
                for (int i = 0; i < clip.metadata.perspectivesCount; ++i)
                {
                    radialBiasPersp[i] = radialBiasDefault;
                }
                radialBiasPerspInMeters = null;
            }

            // This way of sizing the radialBiasPersp array will cause multiperspective core clips to be sized to 12
            int size = (clip.metadata.perspectivesCount > 1) ? 12 : 1;
            if (radialBiasPerspInMeters == null || radialBiasPerspInMeters.Length != size)
            {
                Vector4 defaultVal = new Vector4(Util.cmToMeters(MeshSource.radialBiasDefault), 0, 0, 0);
                radialBiasPerspInMeters = new Vector4[size];
                for (int i = 0; i < size; ++i)
                {
                    radialBiasPerspInMeters[i] = defaultVal;
                }
            }

            return true;
        }

        protected override void OnUpdate()
        {
            if (m_wasPlaying && !m_pausedFromRenderer)
            {
                CheckVisibility();
            }
            else if (pauseDataGenerationWhenInvisible && !m_seenOnce)
            {
                CheckVisibility();
                m_seenOnce = true;
            }

            if (clip != null && clip.isSetup && radialBiasPersp != null && radialBiasPerspInMeters != null)
            {
                // propagate radialBias value to per perspective
                for (int i = 0; i < radialBiasPersp.Length; ++i)
                {
                    if (radialBiasPerspInMeters != null && radialBiasPerspInMeters.Length > 0)
                    {
                        radialBiasPerspInMeters[i].x = Util.cmToMeters(radialBias);
                    }
                }
            }
            base.OnUpdate();
        }

        protected override bool OnGenerate()
        {
            if (m_subMeshes == null) return false;

            foreach (var submesh in m_subMeshes)
            {
                submesh.CopyTriangleCount();
                Util.ArgsBufferPrep.PrepareDrawArgs(submesh.trianglesCount, submesh.drawIndirectArgs, m_forceStereo);
            }

            if (recalculateCurrentSurfaceTriangleCount)
            {
                uint tempSubMeshIndex = currentSubmeshIndex;
                for (uint i = 0; i < m_subMeshes.Length; i++)
                {
                    currentSubmeshIndex = i;
                    maxSurfaceTriangles = m_subMeshes[i].calculateMaxTrianglesNeeded();
                }
                currentSubmeshIndex = tempSubMeshIndex;

                recalculateCurrentSurfaceTriangleCount = false;
                ScheduleResize();
                ScheduleGenerate();
            }

            return true;
        }
        #endregion

        #region Visibility

        public bool pauseDataGenerationWhenInvisible = false;
        public bool pausePlayerWhenInvisible = false;

        protected bool m_wasPlaying = false;
        protected bool m_doGeneration = true;
        protected bool m_pausedFromRenderer = false;
        protected bool m_seenOnce = false;

        public virtual Bounds GetLocalBounds()
        {
            return clip != null ?
                new Bounds(clip.metadata.boundsCenter, clip.metadata.boundsSize) :
                new Bounds(Vector3.zero, Vector3.one);
        }

        public virtual Bounds GetWorldBounds()
        {
            Bounds bounds = GetLocalBounds();
            Vector3 alt1 = bounds.center - new Vector3(-bounds.extents.x, -bounds.extents.y, bounds.extents.z);
            Vector3 alt2 = bounds.center + new Vector3(-bounds.extents.x, -bounds.extents.y, bounds.extents.z);
            return GeometryUtility.CalculateBounds(new Vector3[] { bounds.min, bounds.max, alt1, alt2 }, transform.localToWorldMatrix);
        }

        internal void Pause()
        {
            if (pausePlayerWhenInvisible)
            {
                if (clip.player.IsPlaying())
                {
                    m_wasPlaying = true;
                    clip.player.Pause();
                }
            }
            if (pauseDataGenerationWhenInvisible || pausePlayerWhenInvisible)
            {
                m_doGeneration = false;
            }
        }

        internal void Continue()
        {
            if (pausePlayerWhenInvisible)
            {
                if (!clip.player.IsPlaying() && m_wasPlaying)
                {
                    clip.player.Play();
                    m_wasPlaying = false;
                }
            }
            if (pauseDataGenerationWhenInvisible || pausePlayerWhenInvisible)
            {
                m_doGeneration = true;
            }
        }

        private void OnBecameVisible()
        {
            Continue();
            m_pausedFromRenderer = false;
            m_seenOnce = true;
        }

        private void OnBecameInvisible()
        {
            Pause();
            m_pausedFromRenderer = true;
        }

        internal void CheckVisibility()
        {
            bool visible = Util.IsVisible(GetWorldBounds());
            if (visible != m_doGeneration) //these should always be the same
            {
                if (visible)
                {
                    Continue();
                }
                else
                {
                    Pause();
                }
            }
        }
        #endregion

        #region IPropertyTransfer

        public virtual void SetProperties(ref ComputeShader compute, int kernel)
        {
            if (radialBiasPerspInMeters != null)
                compute.SetVectorArray(MeshSourceShaderIds._RadialBiasPerspInMeters, radialBiasPerspInMeters);
            if (m_subMeshes != null && m_subMeshes.Length > currentSubmeshIndex)
                m_subMeshes[currentSubmeshIndex].SetProperties(ref compute, kernel);
        }

        public virtual void SetProperties(ref Material material)
        {
            if (radialBiasPerspInMeters != null)
                material.SetVectorArray(MeshSourceShaderIds._RadialBiasPerspInMeters, radialBiasPerspInMeters);
            if (m_subMeshes != null && m_subMeshes.Length > currentSubmeshIndex)
                m_subMeshes[currentSubmeshIndex].SetProperties(ref material);
        }

        public virtual void SetProperties(ref Material material, ref MaterialPropertyBlock block)
        {
            if (radialBiasPerspInMeters != null)
                block.SetVectorArray(MeshSourceShaderIds._RadialBiasPerspInMeters, radialBiasPerspInMeters);
            if (m_subMeshes != null && m_subMeshes.Length > currentSubmeshIndex)
                m_subMeshes[currentSubmeshIndex].SetProperties(ref material, ref block);
        }
        #endregion
    }
}