using Depthkit;
using System;
using UnityEngine;

namespace Depthkit
{
    [System.Serializable]
    public class GaussianBlurFilter
    {
        [Range(0.1f, 16.0f)]
        public float radius = 0.3f;

        [Range(1, 8)]
        public int reductionFactor = 1;

        public int slices = 1;

        private int m_prevReductionFactor = 0;

        protected RenderTexture[] m_textures = null;
        protected int m_currentTexture = 0;
        protected ComputeShader m_blurCompute;

        protected static string s_defaultComputeBlurShaderName = "Shaders/Util/GaussianBlurFilter";

        private Vector4 m_pongSize;

        internal static class BlurShaderIds
        {
            internal static readonly int
                _MainTex = Shader.PropertyToID("_MainTex"),
                _PingData = Shader.PropertyToID("_PingData"),
                _PongData = Shader.PropertyToID("_PongData"),
                _PingSizeTS = Shader.PropertyToID("_PingSizeTS"),
                _PongSize = Shader.PropertyToID("_PongSize"),
                _PowerAmount = Shader.PropertyToID("_PowerAmount"),
                _KernelSize = Shader.PropertyToID("_KernelSize"),
                _GaussianExponential = Shader.PropertyToID("_GaussianExponential"),
                _GaussianNormalization = Shader.PropertyToID("_GaussianNormalization"),
                _Axis = Shader.PropertyToID("_Axis"),
                _Slice = Shader.PropertyToID("_Slice");
        }

        public bool hasTexture
        {
            get
            {
                return m_textures != null;
            }
        }
        public RenderTexture texture
        {
            get
            {
                return m_textures != null ? m_textures[m_currentTexture] : null;
            }
        }

        void CreateTextures(RenderTexture tex)
        {
            if (m_textures == null || m_textures.Length != 2)
            {
                m_textures = new RenderTexture[2];
            }

            if (m_textures[0] != null && m_textures[0].IsCreated())
            {
                m_textures[0].Release();
            }

            m_textures[0] = Util.CopyFromRenderTextureSettings(tex, new Vector2(1.0f / (float)reductionFactor, 1.0f / (float)reductionFactor));

            if (m_textures[1] != null && m_textures[1].IsCreated())
            {
                m_textures[1].Release();
            }

            m_textures[1] = Util.CopyFromRenderTextureSettings(tex, new Vector2(1.0f / (float)reductionFactor, 1.0f / (float)reductionFactor));
        }

        public void Setup(string computeShader = "")
        {
            Util.EnsureComputeShader(ref m_blurCompute, computeShader == string.Empty ? s_defaultComputeBlurShaderName : computeShader);
        }

        public virtual void EnsureTextures(RenderTexture tex)
        {
            if (reductionFactor != m_prevReductionFactor ||
                m_textures == null || 
                m_textures.Length != 2 ||
                m_textures[m_currentTexture].width != tex.width / reductionFactor ||
                m_textures[m_currentTexture].height != tex.height / reductionFactor ||
                m_textures[m_currentTexture].dimension != tex.dimension ||
                m_textures[m_currentTexture].volumeDepth != tex.volumeDepth)
            {
                m_prevReductionFactor = reductionFactor;
                CreateTextures(tex);
                m_pongSize = new Vector2(m_textures[m_currentTexture].width, m_textures[m_currentTexture].height);
            }
        }

        public virtual void DoBlur(RenderTexture tex)
        {
            EnsureTextures(tex);

            RenderTexture currentRT = RenderTexture.active;
            for (int slice = 0; slice < slices; ++slice)
            {
                m_blurCompute.SetInt(BlurShaderIds._Slice, slice);
                BlurPass(tex);
            }
            RenderTexture.active = currentRT;
        }

        protected void BlurPass(RenderTexture tex)
        {
            int pong = (m_currentTexture + 1) % 2;

            double sigmaSq = radius * radius;
            double sigmaSq2 = sigmaSq * 2.0f;
            float gaussianNormalization = (float)(1.0 / Mathf.Sqrt((float)(Mathf.PI * sigmaSq2))); // gaussian normalization coefficient
            float gaussianExponential = (float)(-1.0 / sigmaSq2); // gaussian exponential coefficient
            int kernelSize = Mathf.CeilToInt(radius * 4.0f + 1.0f);

            int kernelIndex = Math.Min(kernelSize/2 - 1, 5); //kernel size is the shader kernel

            m_blurCompute.SetTexture(kernelIndex, BlurShaderIds._PingData, tex);
            m_blurCompute.SetTexture(kernelIndex, BlurShaderIds._PongData, m_textures[pong]);

            m_blurCompute.SetVector(BlurShaderIds._PongSize, m_pongSize);
            m_blurCompute.SetFloat(BlurShaderIds._GaussianExponential, gaussianExponential);
            m_blurCompute.SetFloat(BlurShaderIds._GaussianNormalization, gaussianNormalization);

            // horizontal
            m_blurCompute.SetVector(BlurShaderIds._Axis, new Vector2(1, 0));
            Util.DispatchGroups(m_blurCompute, kernelIndex, m_textures[m_currentTexture].width, m_textures[m_currentTexture].height, 1);

            m_blurCompute.SetTexture(kernelIndex, BlurShaderIds._PingData, m_textures[pong]);
            m_blurCompute.SetTexture(kernelIndex, BlurShaderIds._PongData, m_textures[m_currentTexture]);

            // vertical
            m_blurCompute.SetVector(BlurShaderIds._Axis, new Vector2(0, 1));
            // Note width and height are swapped because we use the same kernel for both and _Axis determines how sampling is done
            // Otherwise we would need separate kernels for both horizontal and vertical passes with different [numthreads()] layouts
            Util.DispatchGroups(m_blurCompute, kernelIndex, m_textures[m_currentTexture].height, m_textures[m_currentTexture].width, 1);
        }

        public void Release()
        {
            if (m_textures != null)
            {
                //release the copy texture
                foreach (RenderTexture tex in m_textures)
                {
                    if (tex != null && tex.IsCreated())
                    {
                        tex.Release();
                    }
                }
                m_textures = null;
            }
        }
    }
}
