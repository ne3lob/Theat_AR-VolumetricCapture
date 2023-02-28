using System.Collections;
using UnityEngine;
using System;

namespace Depthkit
{
    [System.Serializable]
    public class MaskGenerator : IPropertyTransfer
    {
        public Clip clip;

        [Range(1, 8)]
        public int scale = 1;

        [Range(0.0f, 10.0f)]
        public float invalidateEdgeWidth = 1.0f;

        [Range(1.0f, 10.0f)]
        public float invalidateStrength = 1.0f;

        protected ComputeShader m_maskGeneratorCompute;
        [SerializeField, HideInInspector]
        protected RenderTextureFormat m_maskTextureFormat = RenderTextureFormat.RFloat;
        protected RenderTexture m_maskTexture;
        protected Vector4 m_maskTextureTS; // TODO check if needed

        public static class MaskGeneratorShaderIds
        {
            public static readonly int
                _LatticeSize = Shader.PropertyToID("_LatticeSize"),
                _MaskTexture = Shader.PropertyToID("_MaskTexture"),
                _SobelMultiplier = Shader.PropertyToID("_SobelMultiplier"),
                _MaskTextureTS = Shader.PropertyToID("_MaskTextureTS"),
                _PerspectiveToSlice = Shader.PropertyToID("_PerspectiveToSlice"),
                _SliceCount = Shader.PropertyToID("_SliceCount"),
                _SliceToPerspective = Shader.PropertyToID("_SliceToPerspective"),
                _Downscaled = Shader.PropertyToID("_Downscaled"),
                _FullRes = Shader.PropertyToID("_FullRes"),
                _PaddedUVScaleFactor = Shader.PropertyToID("_PaddedUVScaleFactor"),
                _DownsampledMaskTexture = Shader.PropertyToID("_DownsampledMaskTexture"),
                _SobelInvalidateEdgeWidth = Shader.PropertyToID("_SobelInvalidateEdgeWidth"),
                _SobelInvalidateStrength = Shader.PropertyToID("_SobelInvalidateStrength");
            public static readonly string _UseEdgeMask = "DK_USE_EDGEMASK";
            public static readonly string _DebugEdgeMask = "DK_DEBUG_EDGEMASK";
            public static readonly string _DitherEdgeKW = "DK_SCREEN_DOOR_TRANSPARENCY";
        }

        //passing arrays to shaders requires padding, so just use Vector4
        //https://cmwdexint.com/2017/12/04/computeshader-setfloats/
        private Vector4[] m_perspectivesToSlice;
        public Vector4[] perspectivesToSlice
        {
            get { return m_perspectivesToSlice; }
            set { m_perspectivesToSlice = value; }
        }

        //passing arrays to shaders requires padding, so just use Vector4
        //https://cmwdexint.com/2017/12/04/computeshader-setfloats/
        private Vector4[] m_sliceToPerspective;
        public Vector4[] sliceToPerspective
        {
            get { return m_sliceToPerspective; }
            set { m_sliceToPerspective = value; }
        }

        public int sliceCount
        {
            get { return m_blurFilter != null ? m_blurFilter.slices : 1; }
            set { if (m_blurFilter != null) m_blurFilter.slices = value; }
        }

        public bool enableBlur = false;

        public float blurRadius
        {
            get { return m_blurFilter != null ? m_blurFilter.radius : 0.3f; }
            set { if (m_blurFilter != null) m_blurFilter.radius = value; }
        }

        public Vector2 paddedUVScaleFactor
        {
            get
            {
                return clip == null ? Vector2.zero : new Vector2(clip.metadata.perspectiveResolution.x, clip.metadata.perspectiveResolution.y) / new Vector2(clip.metadata.paddedTextureDimensions.x, clip.metadata.paddedTextureDimensions.y);
            }
        }

        public bool enableMaskDebug = false;

        [SerializeField]
        protected GaussianBlurFilter m_blurFilter;

        [Range(0.1f, 100.0f)]
        public float sobelMultiplier = 0.5f; //cm

        protected ComputeShader m_sobelFilterCompute;
        protected int m_sobelFilterKId = -1;

        public RenderTexture maskTexture
        {
            get
            {
                return enableBlur ? m_blurFilter != null ? m_blurFilter.texture : m_maskTexture : m_maskTexture;
            }
        }

        [SerializeField]
        private int m_downScale = 0;
        protected ComputeShader m_downScaleCompute;
        protected int m_downScaleKId = -1;

        public int downScale {
            get { return m_downScale; } 
            set
            {
                m_downScale = Math.Max(value, scale);
                DownScaleMaskTexture();
            }
        }

        RenderTexture m_downScaledMaskTexture = null;

        public RenderTexture downScaledMaskTexture
        {
            get
            {
                return downScale != scale ? m_downScaledMaskTexture : enableBlur ? m_blurFilter.texture : m_maskTexture;
            }
        }

        void DownScalePass(RenderTexture src, RenderTexture dst)
        {
            m_downScaleCompute.SetTexture(m_downScaleKId, MaskGeneratorShaderIds._FullRes, src);
            m_downScaleCompute.SetTexture(m_downScaleKId, MaskGeneratorShaderIds._Downscaled, dst);
            Util.DispatchGroups(m_downScaleCompute, m_downScaleKId, dst.width, dst.height, dst.volumeDepth);
        }

        void DownScaleMaskTexture()
        {
            int downlevels = (int)Mathf.Log((float)m_downScale, 2.0f);
            int mainlevels = (int)Mathf.Log((float)scale, 2.0f);
            int levels = downlevels - mainlevels;

            m_downScaleKId = m_downScaleCompute.FindKernel(Util.GetScaled2DKernelName("MinMaxDownscaleByHalf"));

            //only scale if we need to
            if (levels > 0 && maskTexture != null)
            {
                Vector2Int newSize = new Vector2Int(clip.metadata.paddedTextureDimensions.x / m_downScale, clip.metadata.paddedTextureDimensions.y / m_downScale);
                if (m_downScaledMaskTexture == null || m_downScaledMaskTexture.width != newSize.x || m_downScaledMaskTexture.height != newSize.y  || m_downScaledMaskTexture.volumeDepth != maskTexture.volumeDepth) 
                {
                    if(m_downScaledMaskTexture != null && m_downScaledMaskTexture.IsCreated())
                    {
                        m_downScaledMaskTexture.Release();
                    }
                    m_downScaledMaskTexture = new RenderTexture(newSize.x, newSize.y, maskTexture.depth, RenderTextureFormat.ARGBHalf);
                    m_downScaledMaskTexture.dimension = maskTexture.dimension;
                    m_downScaledMaskTexture.volumeDepth = maskTexture.volumeDepth;
                    m_downScaledMaskTexture.filterMode = maskTexture.filterMode;
                    m_downScaledMaskTexture.name = "Downscaled Mask Texture";
                    m_downScaledMaskTexture.enableRandomWrite = true;
                    m_downScaledMaskTexture.autoGenerateMips = maskTexture.autoGenerateMips;
                    m_downScaledMaskTexture.Create();
                }

                if(levels > 1)
                {
                    //use temp target hald the res of the mask texture.
                    var desc = maskTexture.descriptor;
                    desc.graphicsFormat = m_downScaledMaskTexture.descriptor.graphicsFormat;
                    desc.colorFormat = m_downScaledMaskTexture.descriptor.colorFormat;
                    desc.width /= 2;
                    desc.height /= 2;

                    RenderTexture[] tmp = new RenderTexture[2];

                    int current = 0;

                    tmp[0] = RenderTexture.GetTemporary(desc);
                    if(!tmp[0].IsCreated()) tmp[0].Create();

                    DownScalePass(maskTexture, tmp[current]);

                    for (int level = 1; level < levels - 1; ++level)
                    {
                        int next = (current + 1) % 2;

                        desc.width /= 2;
                        desc.height /= 2;
                        tmp[next] = RenderTexture.GetTemporary(desc);
                        if (!tmp[next].IsCreated()) tmp[next].Create();

                        DownScalePass(tmp[current], tmp[next]);

                        RenderTexture.ReleaseTemporary(tmp[current]);
                        current = next;
                    }

                    //commit to final tex
                    DownScalePass(tmp[current], m_downScaledMaskTexture);

                    RenderTexture.ReleaseTemporary(tmp[current]);
                }
                else
                {
                    //commit to final tex
                    DownScalePass(maskTexture, m_downScaledMaskTexture);
                }
            }
        }

        public void Setup()
        {
            if (clip == null || !clip.isSetup) return;

            if (m_blurFilter == null)
            {
                m_blurFilter = new GaussianBlurFilter();
            }
            m_blurFilter.Setup();
            m_blurFilter.reductionFactor = 1;
            Util.EnsureComputeShader(ref m_sobelFilterCompute, "Shaders/Util/SobelFilter");
            Util.EnsureComputeShader(ref m_downScaleCompute, "Shaders/Util/MinMaxDownscaleByHalf");

            m_sobelFilterKId = m_sobelFilterCompute.FindKernel(Util.GetScaled2DKernelName("KSobelFilter"));

            if (m_perspectivesToSlice == null || m_perspectivesToSlice.Length != Metadata.MaxPerspectives ||
                m_sliceToPerspective == null || m_sliceToPerspective.Length != Metadata.MaxPerspectives)
            {
                m_perspectivesToSlice = new Vector4[Metadata.MaxPerspectives];
                m_sliceToPerspective = new Vector4[Metadata.MaxPerspectives];
            }
            for (int ind = 0; ind < clip.metadata.perspectivesCount; ++ind)
            {
                // map perspective id to slice index and slice to perspective id
                // they are the same value as the slices are not priortized and are in the same order as the perspectives
                m_perspectivesToSlice[ind] = new Vector4(ind, 0, 0, 0);
                m_sliceToPerspective[ind] = new Vector4(ind, 0, 0, 0);
            }
        }

        public void EnsureTexture()
        {
            if (clip == null || clip.isSetup == false) return;
            Vector2Int originalRes = clip.metadata.perspectiveResolution;
            Vector2Int paddedRes = clip.metadata.paddedTextureDimensions;

            if (Util.EnsureRenderTexture(
                ref m_maskTexture,
                paddedRes.x / scale, paddedRes.y / scale,
                 RenderTextureFormat.RFloat,
                RenderTextureReadWrite.Linear,
                false,
                FilterMode.Bilinear,
                true,
                RenderTextureFormat.RFloat,
                UnityEngine.Rendering.TextureDimension.Tex2DArray,
                sliceCount))
            {
                m_maskTexture.name = "Depthkit Mask Texture";
                m_maskTextureFormat = m_maskTexture.format;
                m_maskTextureTS = new Vector4(1.0f / (originalRes.x / (float)scale), 1.0f / (originalRes.y / (float)scale), originalRes.x / (float)scale, originalRes.y / (float)scale);
            }
            m_blurFilter.EnsureTextures(m_maskTexture);
        }

        public void Release()
        {
            if (m_blurFilter != null) m_blurFilter.Release();
            Util.ReleaseRenderTexture(ref m_maskTexture);
            Util.ReleaseRenderTexture(ref m_downScaledMaskTexture);
        }

        public void SobelFilterMask()
        {
            clip.SetProperties(ref m_sobelFilterCompute, m_sobelFilterKId);

            m_sobelFilterCompute.SetInt(MaskGeneratorShaderIds._SliceCount, sliceCount);
            m_sobelFilterCompute.SetVectorArray(MaskGeneratorShaderIds._SliceToPerspective, m_sliceToPerspective);

            m_sobelFilterCompute.SetTexture(m_sobelFilterKId, MaskGeneratorShaderIds._MaskTexture, m_maskTexture);
            m_sobelFilterCompute.SetFloat(MaskGeneratorShaderIds._SobelMultiplier, sobelMultiplier);
            m_sobelFilterCompute.SetVector(MaskGeneratorShaderIds._MaskTextureTS, m_maskTextureTS);
            m_sobelFilterCompute.SetFloat(MaskGeneratorShaderIds._SobelInvalidateEdgeWidth, invalidateEdgeWidth);
            m_sobelFilterCompute.SetFloat(MaskGeneratorShaderIds._SobelInvalidateStrength, invalidateStrength);
            Util.DispatchGroups(m_sobelFilterCompute, m_sobelFilterKId, m_maskTexture.width, m_maskTexture.height, sliceCount);
        }

        public virtual void BlurMask()
        {
            m_blurFilter.DoBlur(m_maskTexture);
        }

        public void GenerateMask()
        {
            EnsureTexture();
            SobelFilterMask();

            if (enableBlur)
            {
                BlurMask();
            }

            if(downScale > 0)
            {
                DownScaleMaskTexture();
            }
        }

        public void SetProperties(ref ComputeShader compute, int kernel)
        {
            if (perspectivesToSlice != null && perspectivesToSlice.Length != 0)
                compute.SetVectorArray(MaskGeneratorShaderIds._PerspectiveToSlice, perspectivesToSlice);
            compute.SetInt(MaskGeneratorShaderIds._SliceCount, sliceCount);
            compute.SetVector(MaskGeneratorShaderIds._PaddedUVScaleFactor, paddedUVScaleFactor);
            if (maskTexture != null)
                compute.SetTexture(kernel, MaskGeneratorShaderIds._MaskTexture, maskTexture);
        }

        public void SetProperties(ref Material material)
        {
            if (perspectivesToSlice != null && perspectivesToSlice.Length != 0)
                material.SetVectorArray(MaskGeneratorShaderIds._PerspectiveToSlice, perspectivesToSlice);
            material.SetInt(MaskGeneratorShaderIds._SliceCount, sliceCount);
            material.SetVector(MaskGeneratorShaderIds._PaddedUVScaleFactor, paddedUVScaleFactor);
            Util.EnsureKeyword(ref material, MaskGeneratorShaderIds._UseEdgeMask, true);
            Util.EnsureKeyword(ref material, MaskGeneratorShaderIds._DebugEdgeMask, enableMaskDebug);
            if (maskTexture != null)
                material.SetTexture(MaskGeneratorShaderIds._MaskTexture, maskTexture);
            if (enableMaskDebug && downScaledMaskTexture != null)
            {
                material.SetTexture(MaskGeneratorShaderIds._DownsampledMaskTexture, downScaledMaskTexture);
            }
        }

        public void SetProperties(ref Material material, ref MaterialPropertyBlock block)
        {
            if(perspectivesToSlice != null && perspectivesToSlice.Length != 0)
                block.SetVectorArray(MaskGeneratorShaderIds._PerspectiveToSlice, perspectivesToSlice);
            block.SetInt(MaskGeneratorShaderIds._SliceCount, sliceCount);
            block.SetVector(MaskGeneratorShaderIds._PaddedUVScaleFactor, paddedUVScaleFactor);
            Util.EnsureKeyword(ref material, MaskGeneratorShaderIds._UseEdgeMask, true);
            Util.EnsureKeyword(ref material, MaskGeneratorShaderIds._DebugEdgeMask, enableMaskDebug);
            if (maskTexture != null)
                block.SetTexture(MaskGeneratorShaderIds._MaskTexture, maskTexture);
            if (enableMaskDebug && downScaledMaskTexture != null)
            {
                block.SetTexture(MaskGeneratorShaderIds._DownsampledMaskTexture, downScaledMaskTexture);
            }
        }
    }
}