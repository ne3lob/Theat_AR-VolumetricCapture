using System;
using System.Collections.Generic;
using UnityEngine;
using System.Runtime.InteropServices;

namespace Depthkit
{
    //data textures are one dataset per triangle (based on the 0th (non-shared) vertex in the triangle)
    [RequireComponent(typeof(Clip))]
    [ExecuteInEditMode]
    [DisallowMultipleComponent]
    [AddComponentMenu("Depthkit/Studio/Sources/Depthkit Studio Mesh Source")]
    public class StudioMeshSource : MeshSource
    {
        public enum VolumeGenerationMethod
        {
            SinglePass,
            MultiPass
        }

        public enum VolumeDensity
        {
            VeryLow = 16,
            Low = 8,
            Medium = 4,
            High = 2,
            Full = 1
        }

        public enum UntexturedGeometrySettings
        {
            Infer,
            Colorize,
            Clip
        }

        public VolumeGenerationMethod generationMethod = VolumeGenerationMethod.SinglePass;

        //Shaders
        private ComputeShader m_generateVolumeCompute;
        private ComputeShader m_generateVolumePreviewCompute;
        private ComputeShader m_extractSurfaceCompute;
        private ComputeShader m_sdfFilterCompute;
        private ComputeShader m_generateNormalWeightsCompute;

        public bool showVolumePreview = false;
        private Material m_volumePreviewMaterial;

        [Range(0.001f, 0.3f)]
        public float volumePreviewAlpha = 0.02f;

        [Range(0.1f, 5.0f)]
        public float volumePreviewPointSize = 1.0f;

        ///LOD SETTINGS
        public bool automaticLevelOfDetail = false;

        [Range(1.0f, 100.0f)]
        public float levelOfDetailDistance = 1.0f;

        private int m_currentLODLevel = 0;

        [Min(1)]
        private int m_currentLODIsoScalar = 1;
        public int currentLevelOfDetailLevel
        {
            set
            {
                m_currentLODLevel = Math.Max(value, 0);
                m_currentLODIsoScalar = (int)Mathf.Pow(2, m_currentLODLevel);
                ScheduleGenerate();
            }
            get { return m_currentLODLevel; }
        }

        private Camera m_mainCamera;

        //SDF settings
        [SerializeField]
        private Bounds m_volumeBounds = new Bounds(Vector3.zero, Vector3.zero);
        public Bounds volumeBounds
        {
            get { return m_volumeBounds; }
            set
            {
                m_volumeBounds = value;
                if (useTriangleMesh)
                {
                    triangleMesh.ResetMeshCube(m_volumeBounds.center, m_volumeBounds.size);
                }
                ScheduleResize();
                ScheduleGenerate();
            }
        }

        public override Bounds GetLocalBounds()
        {
            return m_volumeBounds;
        }

        //volume density is in voxels per meter
        [SerializeField, Range(30.0f, 300.0f)]
        private float m_volumeDensity = 100.0f; //1cm = 1 voxel
        public float volumeDensity
        {
            get { return m_volumeDensity; }
            set
            {
                m_volumeDensity = value;
                ScheduleResize();
                ScheduleGenerate();
            }
        }

        [SerializeField, HideInInspector]
        private Vector3Int m_voxelGridDimensions = Vector3Int.one;

        public int numLevelOfDetailLevels { get; private set; } = 0;

        [SerializeField, HideInInspector]
        private int m_totalVoxelCount;

        //Gaussian Filter Sigma
        [Range(0.3f, 1.12f)]
        public float surfaceSmoothingRadius = 0.3f;

        public bool enableSurfaceSmoothing = false;

#if UNITY_EDITOR
        public bool showLevelOfDetailGizmo = false;
#endif
        private float m_surfaceSensitivityThreshold = 0.0f;

        const float surfaceSensitivityDefault = 0.1f;
        const float weightUnknownDefault = 0.005f;
        const float weightUnseenMaxDefault = 1.0f;
        const float weightUnseenMinDefault = 0.0025f;
        const float weightUnseenFalloffPowerDefault = 10.0f;
        const float weightInFrontMaxDefault = 1.0f;
        const float weightInFrontMinDefault = 0.1f;

        [Range(0.001f, 0.225f)]
        public float surfaceSensitivity = surfaceSensitivityDefault;

        [Range(0.0f, 0.05f)]
        public float weightUnknown = weightUnknownDefault;

        [Range(0.0f, 1.0f)]
        public float weightUnseenMax = weightUnseenMaxDefault;

        [Range(0.0f, 0.01f)]
        public float weightUnseenMin = weightUnseenMinDefault;

        [Range(1.0f, 10.0f)]
        public float weightUnseenFalloffPower = weightUnseenFalloffPowerDefault;

        [Range(0.0f, 1.0f)]
        public float weightInFrontMax = weightInFrontMaxDefault;

        [Range(0.0f, 1.0f)]
        public float weightInFrontMin = weightInFrontMinDefault;

        public Transform volumeViewpoint;

        [Range(0.00f, 8.0f)]
        public float surfaceNormalColorBlendingPower = 1.0f;

        [Range(0.001f, 0.1f)]
        public float perViewDisparityThreshold = 0.025f;

        [Range(0.0001f, 0.1f)]
        public float perViewDisparityBlendWidth = 0.01f;

        [Range(0.0f, 0.1f)]
        public float disparityMin = 0.0f;

        [Range(0.001f, 20.0f)]
        public float globalViewDependentColorBlendWeight = 1.0f;

        [Range(0.001f, 20.0f)]
        public float globalViewDependentGeometryBlendWeight = 1.0f;

        public bool enableViewDependentGeometry = false;

        public UntexturedGeometrySettings untexturedFragmentSetting = UntexturedGeometrySettings.Infer;

        public Color untexturedColor = Color.black;

        //buffers
        private ComputeBuffer[] m_sdfBuffers;
        private int m_currentSdfBuffer = 0;
        private ComputeBuffer m_pointsBuffer;

        private const int m_triangleConnectionTableSize = 256 * 16;
        private const int m_triangleOffsetsSize = 72;
        private const int m_numberOfTrianglesSize = 256;

        private const int m_triangleBufferSize = m_triangleConnectionTableSize + m_triangleOffsetsSize + m_numberOfTrianglesSize;
        private ComputeBuffer m_triangleBuffer;

        protected Material m_halfBlitMaterial;

        private RenderTexture m_normalWeightTexture;
        private Vector4 m_normalWeightTextureTexelSize;
        private Material m_normalWeightGenerationMaterial;

        private int m_generateVolumeKernelGroupSize = 8;
        private int m_extractSurfaceKernelGroupSize = 8;

        private int m_extractSurfaceKId = -1;
        private int m_normalWeightKId = -1;
        private int m_generateVolumeSinglePassKId = -1;
        private int m_generateVolumeMultiPassInitKId = -1;
        private int m_generateVolumeMultiPassAccumulateKId = -1;
        private int m_generateVolumeMultiPassResolveKId = -1;
        private int m_generateVolumePreviewKId = -1;

        public PerspectiveColorBlendingData perspectiveColorBlendingData;
        public PerspectiveGeometryData perspectiveGeometryData;

        public bool[] overrideRadialBias = null;
        public int perspectivesCount = 0;

        [Range(1, 4)]
        public int normalWeightResolutionReduction = 1;

        private bool m_useTextureAtlas = false;
        public bool useTextureAtlas
        {
            set{ m_useTextureAtlas = value; }
        }

#if UNITY_EDITOR
        [HideInInspector]
        public bool[] showPerspectiveGizmo = null;

        protected override void OnAssemblyReload()
        {
            base.OnAssemblyReload();
            EnsureSyncedBuffers();
            EnsurePerPerspectiveBuffer(ref overrideRadialBias, clip.metadata.perspectivesCount, false);
            EnsurePerPerspectiveBuffer(ref showPerspectiveGizmo, clip.metadata.perspectivesCount, false);
            perspectivesCount = clip.metadata.perspectivesCount;
        }

#endif

        public override string DataSourceName()
        {
            return "Depthkit Studio Mesh Source";
        }

        private string GetScaledKernelName(string baseName)
        {
            if (SystemInfo.maxComputeWorkGroupSize >= 16 * 16 && SystemInfo.maxComputeWorkGroupSizeX >= 16 && SystemInfo.maxComputeWorkGroupSizeY >= 16)
            {
                return baseName + "16x16";
            }

            if (SystemInfo.maxComputeWorkGroupSize >= 8 * 8 && SystemInfo.maxComputeWorkGroupSizeX >= 8 && SystemInfo.maxComputeWorkGroupSizeY >= 8)
            {
                return baseName + "8x8";
            }

            return baseName + "4x4";
        }

        private string GetExtractVolumeKernelName()
        {
            string baseName = "ExtractSurfaceFromVolume";

            if (m_useTextureAtlas)
            {
                baseName += "WithUVs";
            }

            if (Application.platform == RuntimePlatform.OSXEditor || Application.platform == RuntimePlatform.OSXPlayer || Application.platform == RuntimePlatform.IPhonePlayer)
            {
                return baseName + "4x4x4";
            }

            return baseName + "8x8x8";
        }

        protected override void OnAwake()
        {
            if (SystemInfo.maxComputeWorkGroupSize < 8 * 8 * 8)
            {
                Debug.LogError("This platform cannot support Depthkit Studio. Max compute work group size too small: " + SystemInfo.maxComputeWorkGroupSize);
            }
            base.OnAwake();
        }

        public override bool OnSetup()
        {
            Util.EnsureComputeShader(ref m_generateVolumeCompute, "Shaders/DataSource/GenerateVolume", "Depthkit Studio Mesh Source Generate Volume Compute");
            Util.EnsureComputeShader(ref m_extractSurfaceCompute, "Shaders/DataSource/ExtractSurfaceFromVolume", "Depthkit Studio Mesh Source Extract Surface Compute");
            Util.EnsureComputeShader(ref m_generateVolumePreviewCompute, "Shaders/DataSource/GenerateVolumePreview", "Depthkit Studio Mesh Source Generate Volume Preview Compute");
            Util.EnsureComputeShader(ref m_sdfFilterCompute, "Shaders/Util/GaussianBlurBuffer", "Depthkit Studio SDF Filter Compute");
            Util.EnsureComputeShader(ref m_generateNormalWeightsCompute, "Shaders/DataSource/GenerateNormalWeights", "Depthkit Generate Normal Weights Compute");

            if (m_halfBlitMaterial == null)
            {
                m_halfBlitMaterial = new Material(Shader.Find("Depthkit/HalfTextureBlit"));
            }

            if (m_normalWeightGenerationMaterial == null)
            {
                m_normalWeightGenerationMaterial = new Material(Shader.Find("Depthkit/GenerateNormalWeights"));
            }

            m_generateVolumeSinglePassKId = m_generateVolumeCompute.FindKernel("GenerateVolumeSinglePass");

            m_generateVolumeMultiPassInitKId = m_generateVolumeCompute.FindKernel("KGenerateVolumeMultiPassInit");
            m_generateVolumeMultiPassAccumulateKId = m_generateVolumeCompute.FindKernel("KGenerateVolumeMultiPassAccumulate");
            m_generateVolumeMultiPassResolveKId = m_generateVolumeCompute.FindKernel("KGenerateVolumeMultiPassResolve");

            m_generateVolumePreviewKId = m_generateVolumePreviewCompute.FindKernel("GenerateVolumePreview");

            uint sizeX, sizeY, sizeZ;

            m_generateVolumeCompute.GetKernelThreadGroupSizes(m_generateVolumeSinglePassKId, out sizeX, out sizeY, out sizeZ);
            m_generateVolumeKernelGroupSize = (int)sizeX;

            EnsureMaskGenerator();


            return base.OnSetup();
        }
        protected void ResetGPUResources()
        {
            Depthkit.Util.ClearAppendBuffer(triangleBuffer); // clear our append count buffer for number of iso surface vertices emmited
        }

        protected override void AcquireResources()
        {
            if (Depthkit.Util.EnsureComputeBuffer(ComputeBufferType.Default, ref m_triangleBuffer, m_triangleBufferSize, sizeof(int)))
            {
                m_triangleBuffer.SetData(triangleConnectionTable, 0, 0, m_triangleConnectionTableSize);
                m_triangleBuffer.SetData(triangleOffsets, 0, m_triangleConnectionTableSize, m_triangleOffsetsSize);
                m_triangleBuffer.SetData(nrOfTriangles, 0, m_triangleConnectionTableSize + m_triangleOffsetsSize, m_numberOfTrianglesSize);
            }

            base.AcquireResources();
            if (!m_clip.isSetup || m_clip.metadata.textureWidth == 0 || m_clip.metadata.textureHeight == 0) return;
            EnsureSyncedBuffers();
            EnsureTextures();
            EnsureBuffers();

            EnsurePerPerspectiveBuffer(ref overrideRadialBias, clip.metadata.perspectivesCount, false);
            perspectivesCount = clip.metadata.perspectivesCount;
            m_clip.newMetadata += OnNewMetadata;
        }

        protected override void FreeResources()
        {
            if (m_clip.isSetup)
            {
                m_clip.newMetadata -= OnNewMetadata;
            }

            if (m_sdfBuffers != null)
            {
                Util.ReleaseComputeBuffer(ref m_sdfBuffers[0]);
                Util.ReleaseComputeBuffer(ref m_sdfBuffers[1]);
                m_sdfBuffers = null;
            }

            Util.ReleaseComputeBuffer(ref m_pointsBuffer);
            Util.ReleaseComputeBuffer(ref m_triangleBuffer);

            perspectiveColorBlendingData.Release();
            perspectiveGeometryData.Release();

            Util.ReleaseRenderTexture(ref m_normalWeightTexture);
            maskGenerator?.Release();

            overrideRadialBias = null;
            perspectivesCount = 0;
            base.FreeResources();
        }

        internal void EnsureBuffers()
        {
            if (m_sdfBuffers == null)
            {
                m_sdfBuffers = new ComputeBuffer[2];
            }

            if (m_totalVoxelCount == 0) return;

            if (Depthkit.Util.EnsureComputeBuffer(ComputeBufferType.Default, ref m_sdfBuffers[0], m_totalVoxelCount, sizeof(float)))
            {
                m_sdfBuffers[0].name = "Depthkit Studio SDF Buffer 0";
            }
            if (Depthkit.Util.EnsureComputeBuffer(ComputeBufferType.Default, ref m_sdfBuffers[1], m_totalVoxelCount, sizeof(float)))
            {
                m_sdfBuffers[1].name = "Depthkit Studio SDF Buffer 1";
            }
        }

        internal void EnsureTextures()
        {
            if (Util.EnsureRenderTexture(ref m_normalWeightTexture, Math.Max(clip.metadata.textureWidth / normalWeightResolutionReduction, 1), Math.Max(clip.metadata.textureHeight / 2 / normalWeightResolutionReduction, 1), RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear, false, FilterMode.Bilinear))
            {
                m_normalWeightTexture.name = "Depthkit Studio Normal Weights Texture";
                m_normalWeightTextureTexelSize = new Vector4(1.0f / m_normalWeightTexture.width, 1.0f / m_normalWeightTexture.height, m_normalWeightTexture.width, m_normalWeightTexture.height);
            }
        }

        internal void EnsurePerPerspectiveBuffer<T>(ref T[] buffer, int expectedSize, T defaultValue)
        {
            if (buffer == null || buffer.Length != expectedSize)
            {
                buffer = new T[expectedSize];
                for (int i = 0; i < expectedSize; ++i)
                {
                    buffer[i] = defaultValue;
                }
            }
        }

        internal void EnsureSyncedBuffers()
        {
            if (perspectiveGeometryData == null || perspectiveGeometryData.Length != clip.metadata.perspectivesCount)
            {
                perspectiveGeometryData = new PerspectiveGeometryData("Depthkit Per Perspective Geometry Data", clip.metadata.perspectivesCount);
            }

            if (perspectiveColorBlendingData == null || perspectiveColorBlendingData.Length != clip.metadata.perspectivesCount)
            {
                perspectiveColorBlendingData = new PerspectiveColorBlendingData("Depthkit Per Perspective Color Blending Data", clip.metadata.perspectivesCount);
            }
        }

        internal void SetupViewDependence()
        {
            if (volumeViewpoint == null && Camera.main != null)
            {
                volumeViewpoint = Camera.main.transform; //use main camera by default
            }

            for (int perspectiveIndex = 0; perspectiveIndex < clip.metadata.perspectivesCount; ++perspectiveIndex)
            {
                float viewWeight = 1.0f;
                if (enableViewDependentGeometry)
                {
                    Vector3 cameraNormal = transform.localToWorldMatrix * clip.metadata.perspectives[perspectiveIndex].cameraNormal;
                    viewWeight = Mathf.Max(Vector3.Dot(volumeViewpoint.transform.forward, cameraNormal), 0.0001f);

                    float power;
                    if (perspectiveGeometryData.MatchViewDependentColorWeight(perspectiveIndex))
                    {
                        power = Mathf.Lerp(0.0f, globalViewDependentColorBlendWeight, perspectiveColorBlendingData.GetViewDependentColorBlendContribution(perspectiveIndex));
                    }
                    else
                    {
                        power = Mathf.Lerp(0.0f, globalViewDependentGeometryBlendWeight, perspectiveGeometryData.GetViewDependentContribution(perspectiveIndex));
                    }
                    viewWeight = Mathf.Max(Mathf.Clamp01(Mathf.Pow(viewWeight, power)), 0.0001f);
                }
                perspectiveGeometryData.SetViewDependentWeight(perspectiveIndex, viewWeight);
            }
        }

        void OnNewMetadata()
        {
            if(m_volumeBounds.center == Vector3.zero && m_volumeBounds.size == Vector3.one)
            {
                ResetVolumeBounds();
            }
        }

        public void ResetVolumeBounds()
        {
            m_volumeBounds.center = clip.metadata.boundsCenter;
            m_volumeBounds.size = clip.metadata.boundsSize;
        }

        public void ResetSurfaceSensitivity()
        {
            surfaceSensitivity = surfaceSensitivityDefault;
            radialBias = MeshSource.radialBiasDefault;
            weightUnknown = weightUnknownDefault;
            weightUnseenMax = weightUnseenMaxDefault;
            weightUnseenMin = weightUnseenMinDefault;
            weightUnseenFalloffPower = weightUnseenFalloffPowerDefault;
            weightInFrontMax = weightInFrontMaxDefault;
            weightInFrontMin = weightInFrontMinDefault;
            for (int i = 0; i < radialBiasPersp.Length; ++i)
            {
                radialBiasPersp[i] = radialBias;
                overrideRadialBias[i] = false;
            }
        }

        public void LoadFrontBiasedDefaults()
        {
            surfaceSensitivity = 0.2f;
            weightUnknown = 0.005f;
            weightUnseenMax = 1.0f;
            weightUnseenMin = 0.0f;
            weightUnseenFalloffPower = 8.0f;
            weightInFrontMax = 1.0f;
            weightInFrontMin = 0.1f;
        }

        protected override bool OnResize()
        {
            if (m_volumeBounds.center == Vector3.zero && m_volumeBounds.size == Vector3.zero)
            {
                m_volumeBounds.center = clip.metadata.boundsCenter;
                m_volumeBounds.size = clip.metadata.boundsSize;
            }

            ReserveSubMeshes<PackedCoreTriangleSubMesh>(1);

            CurrentSubMesh().Init();

            //voxel size in meters
            m_voxelGridDimensions = new Vector3Int((int)Mathf.Floor(m_volumeBounds.size.x * m_volumeDensity), (int)Mathf.Floor(m_volumeBounds.size.y * m_volumeDensity), (int)Mathf.Floor(m_volumeBounds.size.z * m_volumeDensity));
            m_totalVoxelCount = m_voxelGridDimensions.x * m_voxelGridDimensions.y * m_voxelGridDimensions.z;

            int smallestDim = Math.Min(Math.Min(m_voxelGridDimensions.x, m_voxelGridDimensions.y), m_voxelGridDimensions.z);

            numLevelOfDetailLevels = (int)Math.Max(Math.Log(Convert.ToDouble((int)smallestDim), 2.0) - 4.0, 0.0);

            if (!m_clip.isSetup || m_clip.metadata.textureWidth == 0 || m_clip.metadata.textureHeight == 0) return true;
            EnsureSyncedBuffers();
            EnsureBuffers();
            if (enableEdgeMask)
            {
                EnsureMaskGenerator();
            }
            EnsureTextures();
            EnsurePerPerspectiveBuffer(ref overrideRadialBias, clip.metadata.perspectivesCount, false);
            perspectivesCount = clip.metadata.perspectivesCount;
#if UNITY_EDITOR
            EnsurePerPerspectiveBuffer(ref showPerspectiveGizmo, clip.metadata.perspectivesCount, false);
#endif
            return base.OnResize();
        }

        static class StudioMeshSourceShaderIds
        {
            /////////////// Look Uniforms /////////////////
            internal static readonly int _GlobalViewDependentColorBlendWeight = Shader.PropertyToID("_GlobalViewDependentColorBlendWeight");
            internal static readonly int _SurfaceNormalColorBlendingPower = Shader.PropertyToID("_SurfaceNormalColorBlendingPower");
            internal static readonly int _PerViewDisparityThreshold = Shader.PropertyToID("_PerViewDisparityThreshold");
            internal static readonly int _PerViewDisparityBlendWidth = Shader.PropertyToID("_PerViewDisparityBlendWidth");
            internal static readonly int _PerspectiveColorBlending = Shader.PropertyToID("_PerspectiveColorBlending");
            internal static readonly int _UntexturedFragDefaultColor = Shader.PropertyToID("_UntexturedFragDefaultColor");            
            internal static readonly string _UntexturedFragmentInfer = "DK_UNTEXTURED_FRAGMENT_INFER";
            internal static readonly string _UntexturedFragmentColorize = "DK_UNTEXTURED_FRAGMENT_COLORIZE";
            internal static readonly string _UntexturedFragmentClip = "DK_UNTEXTURED_FRAGMENT_CLIP";

            /////////////// SdfCompute Uniforms ///////////////////

            internal static readonly int _SdfBuffer = Shader.PropertyToID("_SdfBuffer");
            internal static readonly int _SdfWeightBuffer = Shader.PropertyToID("_SdfWeightBuffer");
            internal static readonly int _VoxelGridX = Shader.PropertyToID("_VoxelGridX");
            internal static readonly int _VoxelGridY = Shader.PropertyToID("_VoxelGridY");
            internal static readonly int _VoxelGridZ = Shader.PropertyToID("_VoxelGridZ");
            internal static readonly int _VoxelGridf = Shader.PropertyToID("_VoxelGridf");
            internal static readonly int _BoundsSize = Shader.PropertyToID("_BoundsSize");
            internal static readonly int _BoundsCenter = Shader.PropertyToID("_BoundsCenter");
            internal static readonly int _IsoLODScalar = Shader.PropertyToID("_IsoLODScalar");
            internal static readonly int _SdfSensitivity = Shader.PropertyToID("_SdfSensitivity");
            internal static readonly int _WeightUnknown = Shader.PropertyToID("_WeightUnknown");
            internal static readonly int _WeightUnseenMax = Shader.PropertyToID("_WeightUnseenMax");
            internal static readonly int _WeightUnseenMin = Shader.PropertyToID("_WeightUnseenMin");
            internal static readonly int _WeightUnseenFalloffPower = Shader.PropertyToID("_WeightUnseenFalloffPower");
            internal static readonly int _WeightInFrontMax = Shader.PropertyToID("_WeightInFrontMax");
            internal static readonly int _WeightInFrontMin = Shader.PropertyToID("_WeightInFrontMin");
            internal static readonly int _NormalTexture = Shader.PropertyToID("_NormalTexture");
            internal static readonly int _NormalTexture_TexelSize = Shader.PropertyToID("_NormalTexture_TexelSize");
            internal static readonly int _NormalWeights = Shader.PropertyToID("_NormalWeights");
            internal static readonly int _PerspectiveGeometryData = Shader.PropertyToID("_PerspectiveGeometryData");
            internal static readonly int _WSDepth = Shader.PropertyToID("_WSDepth");
            internal static readonly int _CurrentPerspective = Shader.PropertyToID("_CurrentPerspective");
            internal static readonly int _DispatchSize = Shader.PropertyToID("_DispatchSize");

            /////////////// Edge Mask Uniforms ///////////////////
            internal static readonly int _MainTex = Shader.PropertyToID("_MainTex");

            /////////////// Debug Uniforms ///////////////////
            internal static readonly int _Points = Shader.PropertyToID("_Points");
            internal static readonly int _SdfAlpha = Shader.PropertyToID("_SdfAlpha");
            internal static readonly int _PointSize = Shader.PropertyToID("_PointSize");
            internal static readonly int _LocalToWorldMatrix = Shader.PropertyToID("_LocalToWorldMatrix");

            /////////////// Gaussian Blur Uniforms ///////////////////
            internal static readonly int _PingData = Shader.PropertyToID("_PingData");
            internal static readonly int _PongData = Shader.PropertyToID("_PongData");
            internal static readonly int _DataSize = Shader.PropertyToID("_DataSize");
            internal static readonly int _Axis = Shader.PropertyToID("_Axis");
            internal static readonly int _PowerAmount = Shader.PropertyToID("_PowerAmount");
            internal static readonly int _GaussianNormalization = Shader.PropertyToID("_GaussianNormalization");
            internal static readonly int _GaussianExponential = Shader.PropertyToID("_GaussianExponential");

            /////////////// IsoCompute Uniforms ///////////////////
            internal static readonly int _TriangleDataBuffer = Shader.PropertyToID("_TriangleDataBuffer");
            internal static readonly int _SdfThreshold = Shader.PropertyToID("_SdfThreshold");
            internal static readonly int _TriangleBuffer = Shader.PropertyToID("_TriangleBuffer");
            internal static readonly int _TriangleCullingThreshold = Shader.PropertyToID("_TriangleCullingThreshold");
            public static readonly int
                _IndirectArgs = Shader.PropertyToID("_IndirectArgs");
        }

        #region IPropertyTransfer
        public override void SetProperties(ref Material material)
        {
            EnsureTextures();
            perspectiveColorBlendingData.Sync();
            material.SetFloat(StudioMeshSourceShaderIds._GlobalViewDependentColorBlendWeight, globalViewDependentColorBlendWeight);
            material.SetBuffer(StudioMeshSourceShaderIds._PerspectiveColorBlending, perspectiveColorBlendingData.buffer);
            material.SetFloat(StudioMeshSourceShaderIds._SurfaceNormalColorBlendingPower, surfaceNormalColorBlendingPower);
            material.SetFloat(StudioMeshSourceShaderIds._PerViewDisparityThreshold, perViewDisparityThreshold);
            material.SetFloat(StudioMeshSourceShaderIds._PerViewDisparityBlendWidth, perViewDisparityBlendWidth);
            material.SetVector(StudioMeshSourceShaderIds._UntexturedFragDefaultColor, new Vector3(untexturedColor.r, untexturedColor.g, untexturedColor.b));
            Util.EnsureKeyword(ref material, StudioMeshSourceShaderIds._UntexturedFragmentInfer, untexturedFragmentSetting == UntexturedGeometrySettings.Infer);
            Util.EnsureKeyword(ref material, StudioMeshSourceShaderIds._UntexturedFragmentColorize, untexturedFragmentSetting == UntexturedGeometrySettings.Colorize);
            Util.EnsureKeyword(ref material, StudioMeshSourceShaderIds._UntexturedFragmentClip, untexturedFragmentSetting == UntexturedGeometrySettings.Clip);

            if (enableEdgeMask)
                maskGenerator.SetProperties(ref material);
            else
                Util.EnsureKeyword(ref material, MaskGenerator.MaskGeneratorShaderIds._UseEdgeMask, enableEdgeMask);

            base.SetProperties(ref material);
        }

        public override void SetProperties(ref Material material, ref MaterialPropertyBlock block)
        {
            EnsureTextures();
            perspectiveColorBlendingData.Sync();
            block.SetFloat(StudioMeshSourceShaderIds._GlobalViewDependentColorBlendWeight, globalViewDependentColorBlendWeight);
            block.SetBuffer(StudioMeshSourceShaderIds._PerspectiveColorBlending, perspectiveColorBlendingData.buffer);
            block.SetFloat(StudioMeshSourceShaderIds._SurfaceNormalColorBlendingPower, surfaceNormalColorBlendingPower);
            block.SetFloat(StudioMeshSourceShaderIds._PerViewDisparityThreshold, perViewDisparityThreshold);
            block.SetFloat(StudioMeshSourceShaderIds._PerViewDisparityBlendWidth, perViewDisparityBlendWidth);
            block.SetVector(StudioMeshSourceShaderIds._UntexturedFragDefaultColor, new Vector3(untexturedColor.r, untexturedColor.g, untexturedColor.b));
            Util.EnsureKeyword(ref material, StudioMeshSourceShaderIds._UntexturedFragmentInfer, untexturedFragmentSetting == UntexturedGeometrySettings.Infer);
            Util.EnsureKeyword(ref material, StudioMeshSourceShaderIds._UntexturedFragmentColorize, untexturedFragmentSetting == UntexturedGeometrySettings.Colorize);
            Util.EnsureKeyword(ref material, StudioMeshSourceShaderIds._UntexturedFragmentClip, untexturedFragmentSetting == UntexturedGeometrySettings.Clip);

            if(enableEdgeMask)
                maskGenerator.SetProperties(ref material, ref block);
            else
                Util.EnsureKeyword(ref material, MaskGenerator.MaskGeneratorShaderIds._UseEdgeMask, enableEdgeMask);
            base.SetProperties(ref material, ref block);
        }

        public override void SetProperties(ref ComputeShader compute, int kernel)
        {
            EnsureTextures();
            perspectiveColorBlendingData.Sync();
            compute.SetFloat(StudioMeshSourceShaderIds._GlobalViewDependentColorBlendWeight, globalViewDependentColorBlendWeight);
            compute.SetBuffer(kernel, StudioMeshSourceShaderIds._PerspectiveColorBlending, perspectiveColorBlendingData.buffer);
            compute.SetFloat(StudioMeshSourceShaderIds._SurfaceNormalColorBlendingPower, surfaceNormalColorBlendingPower);
            compute.SetFloat(StudioMeshSourceShaderIds._PerViewDisparityThreshold, perViewDisparityThreshold);
            compute.SetFloat(StudioMeshSourceShaderIds._PerViewDisparityBlendWidth, perViewDisparityBlendWidth);

            if (enableEdgeMask)
                maskGenerator.SetProperties(ref compute, kernel);

            //todo there are no shaderkeywords for compute shaders yet
            base.SetProperties(ref compute, kernel);
        }
        #endregion

        internal void SetCommonComputeProperties(ComputeShader compute, int kernel)
        {
            // LOD example -
            // LOD 0 top level occupies whole voxel grid
            // LOD 1 top level occupies top left octant only - skip histo-pyramid level 1 as is same level as top level
            // LOD 2 top level occupies half top left octant - skip histo pyramid levels 1 & 2
            // LOD 3 top level occupies quarter top left octant - skip histo pyramid levels 1, 2 & 3

            compute.SetInt(StudioMeshSourceShaderIds._VoxelGridX, m_voxelGridDimensions.x);
            compute.SetInt(StudioMeshSourceShaderIds._VoxelGridY, m_voxelGridDimensions.y);
            compute.SetInt(StudioMeshSourceShaderIds._VoxelGridZ, m_voxelGridDimensions.z);
            compute.SetVector(StudioMeshSourceShaderIds._VoxelGridf, new Vector3(m_voxelGridDimensions.x, m_voxelGridDimensions.y, m_voxelGridDimensions.z));

            compute.SetFloat(StudioMeshSourceShaderIds._IsoLODScalar, m_currentLODIsoScalar);
            compute.SetVector(StudioMeshSourceShaderIds._BoundsSize, volumeBounds.size);
            compute.SetVector(StudioMeshSourceShaderIds._BoundsCenter, volumeBounds.center);
            compute.SetFloat(StudioMeshSourceShaderIds._SdfSensitivity, surfaceSensitivity);
            if(radialBiasPerspInMeters != null)
                compute.SetVectorArray(MeshSourceShaderIds._RadialBiasPerspInMeters, radialBiasPerspInMeters);
            compute.SetBuffer(kernel, StudioMeshSourceShaderIds._SdfBuffer, m_sdfBuffers[m_currentSdfBuffer]);
            compute.SetTexture(kernel, StudioMeshSourceShaderIds._NormalTexture, m_normalWeightTexture);
            compute.SetVector(StudioMeshSourceShaderIds._NormalTexture_TexelSize, m_normalWeightTextureTexelSize);
        }

        internal void SetVolumeGenerationPassProperties(ComputeShader compute, int kernel)
        {
            compute.SetFloat(StudioMeshSourceShaderIds._WeightUnknown, weightUnknown);
            compute.SetFloat(StudioMeshSourceShaderIds._WeightUnseenMax, weightUnseenMax);
            compute.SetFloat(StudioMeshSourceShaderIds._WeightUnseenMin, weightUnseenMin);
            compute.SetFloat(StudioMeshSourceShaderIds._WeightUnseenFalloffPower, weightUnseenFalloffPower);
            compute.SetFloat(StudioMeshSourceShaderIds._WeightInFrontMax, weightInFrontMax);
            compute.SetFloat(StudioMeshSourceShaderIds._WeightInFrontMin, weightInFrontMin);

            compute.SetTexture(kernel, StudioMeshSourceShaderIds._NormalTexture, m_normalWeightTexture);
            compute.SetVector(StudioMeshSourceShaderIds._NormalTexture_TexelSize, m_normalWeightTextureTexelSize);

            compute.SetBuffer(kernel, StudioMeshSourceShaderIds._PerspectiveGeometryData, perspectiveGeometryData.buffer);
        }

        private void FilterSdf(Vector3Int dispatchSize)
        {
            int pong = (m_currentSdfBuffer + 1) % 2;
            // pre compute gaussian parameters based on current sigma
            double sigmaSq = surfaceSmoothingRadius * surfaceSmoothingRadius;
            double sigmaSq2 = sigmaSq * 2.0f;
            float gaussianNormalization = (float)(1.0 / Math.Sqrt(Math.PI * sigmaSq2)); // gaussian normalization coefficient
            float gaussianExponential = (float)(-1.0 / sigmaSq2); // gaussian exponential coefficient
            int kernelSize = (int)(Math.Ceiling(Math.PI * sigmaSq)); // compute the kernel size based on the sigma parameter

            m_sdfFilterCompute.SetFloat(StudioMeshSourceShaderIds._GaussianNormalization, gaussianNormalization);
            m_sdfFilterCompute.SetFloat(StudioMeshSourceShaderIds._GaussianExponential, gaussianExponential);

            m_sdfFilterCompute.SetVector(StudioMeshSourceShaderIds._DataSize, new Vector3(m_voxelGridDimensions.x, m_voxelGridDimensions.y, m_voxelGridDimensions.z));

            int kernelIndex = kernelSize - 1;

            // 3 pass 1D gaussian approximates 3D gaussian kernel
            // x axis
            m_sdfFilterCompute.SetBuffer(kernelIndex, StudioMeshSourceShaderIds._PingData, m_sdfBuffers[m_currentSdfBuffer]);
            m_sdfFilterCompute.SetBuffer(kernelIndex, StudioMeshSourceShaderIds._PongData, m_sdfBuffers[pong]);
            m_sdfFilterCompute.SetVector(StudioMeshSourceShaderIds._Axis, new Vector3(1, 0, 0));
            m_sdfFilterCompute.Dispatch(kernelIndex, dispatchSize.x, dispatchSize.y, dispatchSize.z);

            // y axis
            m_sdfFilterCompute.SetBuffer(kernelIndex, StudioMeshSourceShaderIds._PingData, m_sdfBuffers[pong]);
            m_sdfFilterCompute.SetBuffer(kernelIndex, StudioMeshSourceShaderIds._PongData, m_sdfBuffers[m_currentSdfBuffer]);
            m_sdfFilterCompute.SetVector(StudioMeshSourceShaderIds._Axis, new Vector3(0, 1, 0));
            m_sdfFilterCompute.Dispatch(kernelIndex, dispatchSize.x, dispatchSize.y, dispatchSize.z);

            // z axis
            m_sdfFilterCompute.SetBuffer(kernelIndex, StudioMeshSourceShaderIds._PingData, m_sdfBuffers[m_currentSdfBuffer]);
            m_sdfFilterCompute.SetBuffer(kernelIndex, StudioMeshSourceShaderIds._PongData, m_sdfBuffers[pong]);
            m_sdfFilterCompute.SetVector(StudioMeshSourceShaderIds._Axis, new Vector3(0, 0, 1));
            m_sdfFilterCompute.Dispatch(kernelIndex, dispatchSize.x, dispatchSize.y, dispatchSize.z);

            m_currentSdfBuffer = pong;
        }

        //todo keep this function for performance testing and possible older platforms
        private void GenerateNormalWeights()
        {
            clip.SetProperties(ref m_normalWeightGenerationMaterial);
            if (radialBiasPerspInMeters != null)
                m_normalWeightGenerationMaterial.SetVectorArray(MeshSourceShaderIds._RadialBiasPerspInMeters, radialBiasPerspInMeters);
            m_normalWeightGenerationMaterial.SetVector(StudioMeshSourceShaderIds._NormalTexture_TexelSize, m_normalWeightTextureTexelSize);
            Graphics.Blit(clip.cppTexture, m_normalWeightTexture, m_normalWeightGenerationMaterial, 0);
        }

        private void GenerateNormalWeightsCompute()
        {
            m_normalWeightKId = m_generateNormalWeightsCompute.FindKernel(GetScaledKernelName("GeneratePerPixelNormalWeights"));

            if (m_normalWeightKId == -1)
            {
                Debug.LogError("GeneratePerPixelNormalWeights kernel not found");
            }

            clip.SetProperties(ref m_generateNormalWeightsCompute, m_normalWeightKId);
            m_generateNormalWeightsCompute.SetVector(StudioMeshSourceShaderIds._NormalTexture_TexelSize, m_normalWeightTextureTexelSize);
            m_generateNormalWeightsCompute.SetTexture(m_normalWeightKId, StudioMeshSourceShaderIds._NormalWeights, m_normalWeightTexture);
            if (radialBiasPerspInMeters != null)
                m_generateNormalWeightsCompute.SetVectorArray(MeshSourceShaderIds._RadialBiasPerspInMeters, radialBiasPerspInMeters);

            Util.DispatchGroups(m_generateNormalWeightsCompute, m_normalWeightKId, m_normalWeightTexture.width, m_normalWeightTexture.height, 1);
        }

        private void GenerateEdgeBlendMask()
        {
            if (enableEdgeMask)
            { 
                maskGenerator.GenerateMask();
            }
        }

        private Vector3Int DispatchSize(int kernelGroupSize)
        {
            Vector3 gridf = new Vector3(m_voxelGridDimensions.x, m_voxelGridDimensions.y, m_voxelGridDimensions.z);
            Vector3 dispatchSizef = (gridf / kernelGroupSize) / m_currentLODIsoScalar;
            return new Vector3Int((int)Mathf.Ceil(dispatchSizef.x), (int)Mathf.Ceil(dispatchSizef.y), (int)Mathf.Ceil(dispatchSizef.z));
        }

        void GenerateVolumePass(Vector3Int dispatchSize, int kernel)
        {
            SetCommonComputeProperties(m_generateVolumeCompute, kernel);
            SetVolumeGenerationPassProperties(m_generateVolumeCompute, kernel);
            clip.SetProperties(ref m_generateVolumeCompute, kernel);
            m_generateVolumeCompute.Dispatch(kernel, dispatchSize.x, dispatchSize.y, dispatchSize.z);
        }

        private void GenerateVolume()
        {
            EnsureTextures();
            GenerateNormalWeightsCompute();
            GenerateEdgeBlendMask();

            SetupViewDependence();

            perspectiveGeometryData.Sync();

            switch (generationMethod)
            {
                case VolumeGenerationMethod.SinglePass:
                    GenerateVolumeSinglePass();
                    break;
                case VolumeGenerationMethod.MultiPass:
                    GenerateVolumeMultiPass(m_generateVolumeMultiPassInitKId, m_generateVolumeMultiPassAccumulateKId, m_generateVolumeMultiPassResolveKId);
                    break;
            }
        }

        List<int> ActivePerspectives()
        {
            List<int> activePerspectives = new List<int>();
            for (int perspective = 0; perspective < m_clip.metadata.perspectivesCount; ++perspective)
            {
                if (perspectiveGeometryData.EnableGeometry(perspective))
                {
                    activePerspectives.Add(perspective);
                }
            }
            return activePerspectives;
        }

        private void GenerateVolumeMultiPass(int kInit, int kAccum, int kResolve)
        {
            if (kInit == -1)
            {
                Debug.LogError("Init Kernel Index not found");
            }
            if (kAccum == -1)
            {
                Debug.LogError("Accumulate Kernel Index not found");
            }
            if (kResolve == -1)
            {
                Debug.LogError("Resolve Kernel Index not found");
            }

            int weightsIndex = (m_currentSdfBuffer + 1) % 2;
            //they should all have the same dispatch size
            var dispatchSize = DispatchSize(8);
            var activePerspectives = ActivePerspectives();
            
            for (int perspective = 0; perspective < activePerspectives.Count; ++perspective)
            {
                m_generateVolumeCompute.SetInt(StudioMeshSourceShaderIds._CurrentPerspective, activePerspectives[perspective]);

                if (perspective == 0)
                {
                    m_generateVolumeCompute.SetBuffer(kInit, StudioMeshSourceShaderIds._SdfWeightBuffer, m_sdfBuffers[weightsIndex]);
                    GenerateVolumePass(dispatchSize, kInit);
                }
                else if (perspective == activePerspectives.Count - 1)
                {
                    m_generateVolumeCompute.SetBuffer(kResolve, StudioMeshSourceShaderIds._SdfWeightBuffer, m_sdfBuffers[weightsIndex]);
                    GenerateVolumePass(dispatchSize, kResolve);
                }
                else
                {
                    m_generateVolumeCompute.SetBuffer(kAccum, StudioMeshSourceShaderIds._SdfWeightBuffer, m_sdfBuffers[weightsIndex]);
                    GenerateVolumePass(dispatchSize, kAccum);
                }
            }

            if (enableSurfaceSmoothing)
            {
                FilterSdf(dispatchSize);
            }
        }

        private void GenerateVolumeSinglePass()
        {
            if (m_generateVolumeSinglePassKId == -1)
            {
                Debug.LogError("GenerateVolumeSinglePass Kernel Index not found");
            }

            var dispatchSize = DispatchSize(m_generateVolumeKernelGroupSize);

            GenerateVolumePass(dispatchSize, m_generateVolumeSinglePassKId);

            if (enableSurfaceSmoothing)
            {
                FilterSdf(dispatchSize);
            }
        }

        private void ExtractSurfaceFromVolume()
        {
            m_extractSurfaceKId = m_extractSurfaceCompute.FindKernel(GetExtractVolumeKernelName());
            m_extractSurfaceCompute.GetKernelThreadGroupSizes(m_extractSurfaceKId, out uint sizeX, out uint sizeY, out uint sizeZ);
            m_extractSurfaceKernelGroupSize = (int)sizeX;

            if (m_extractSurfaceKId == -1)
            {
                Debug.LogError("ExtractSurfaceFromVolume Kernel not found");
                return;
            }

            Vector3Int dispatchSize = DispatchSize(m_extractSurfaceKernelGroupSize);

            SetCommonComputeProperties(m_extractSurfaceCompute, m_extractSurfaceKId);
            clip.SetProperties(ref m_extractSurfaceCompute, m_extractSurfaceKId);
            SetProperties(ref m_extractSurfaceCompute, m_extractSurfaceKId);

            m_extractSurfaceCompute.SetFloat(StudioMeshSourceShaderIds._SdfThreshold, m_surfaceSensitivityThreshold);
            m_extractSurfaceCompute.SetFloat(StudioMeshSourceShaderIds._TriangleCullingThreshold, Mathf.Pow(1.0f - 0.0f, 6.0f)); //TODO remove this?
            m_extractSurfaceCompute.SetBuffer(m_extractSurfaceKId, StudioMeshSourceShaderIds._SdfBuffer, m_sdfBuffers[m_currentSdfBuffer]);
            m_extractSurfaceCompute.SetBuffer(m_extractSurfaceKId, StudioMeshSourceShaderIds._TriangleBuffer, triangleBuffer);
            m_extractSurfaceCompute.SetBuffer(m_extractSurfaceKId, StudioMeshSourceShaderIds._TriangleDataBuffer, m_triangleBuffer);

            m_extractSurfaceCompute.Dispatch(m_extractSurfaceKId, dispatchSize.x, dispatchSize.y, dispatchSize.z);
        }

        private void DrawDebug()
        {
            if (m_sdfBuffers == null) return;
            if (m_generateVolumePreviewCompute == null || m_sdfBuffers[m_currentSdfBuffer] == null)
            {
                Debug.LogError("sdf compute shader or sdf buffers are null");
                return;
            }

            if (m_generateVolumePreviewKId == -1)
            {
                Debug.LogError("GenerateVolumePreview Kernel not found");
                return;
            }

            int kernelGroupSize = 8;
            Vector3Int dispatchSize;

            if (Depthkit.Util.EnsureComputeBuffer(ComputeBufferType.Default, ref m_pointsBuffer, m_totalVoxelCount, sizeof(float) * 3 + sizeof(float) * 4))
            {
                m_pointsBuffer.name = "Depthkit Studio Debug SDF Buffer";
            }
            dispatchSize = DispatchSize(kernelGroupSize);
            SetCommonComputeProperties(m_generateVolumePreviewCompute, m_generateVolumePreviewKId);
            m_generateVolumePreviewCompute.SetBuffer(m_generateVolumePreviewKId, StudioMeshSourceShaderIds._Points, m_pointsBuffer);
            m_generateVolumePreviewCompute.SetMatrix(StudioMeshSourceShaderIds._LocalToWorldMatrix, transform.localToWorldMatrix);
            m_generateVolumePreviewCompute.Dispatch(m_generateVolumePreviewKId, dispatchSize.x, dispatchSize.y, dispatchSize.z);
            
            if (m_volumePreviewMaterial == null)
            {
                m_volumePreviewMaterial = new Material(Shader.Find("Depthkit/Studio/VolumePreview"));
            }

            m_volumePreviewMaterial.SetFloat(StudioMeshSourceShaderIds._SdfAlpha, volumePreviewAlpha);
            m_volumePreviewMaterial.SetBuffer(StudioMeshSourceShaderIds._Points, m_pointsBuffer);
            m_volumePreviewMaterial.SetFloat(StudioMeshSourceShaderIds._PointSize, volumePreviewPointSize);
            m_volumePreviewMaterial.SetPass(0);

            //scale back up by group size
            Vector3Int scaledDims = dispatchSize * kernelGroupSize;

            Graphics.DrawProcedural(
                m_volumePreviewMaterial,
                GetWorldBounds(),
                MeshTopology.Points,
                scaledDims.x * scaledDims.y * scaledDims.z,
                1,
                null,
                null,
                UnityEngine.Rendering.ShadowCastingMode.Off,
                false,
                gameObject.layer);
        }

        protected override bool OnGenerate()
        {
            if (clip.cppTexture == null)
            {
                return false;
            }

            //reset the clip, this is when a script is reloaded, invalidating all our buffers
            if (m_sdfBuffers == null)
            {
                Setup();
                if (!IsSetup()) return false;
                Resize();
            }
            else if (m_sdfBuffers[m_currentSdfBuffer] == null || !m_sdfBuffers[m_currentSdfBuffer].IsValid())
            {
                Setup();
                if (!IsSetup()) return false;
                Resize();
            }
            else
            {
                triangleBuffer?.SetCounterValue(0);
            }

            if (m_generateVolumeCompute == null || m_generateNormalWeightsCompute == null || m_extractSurfaceCompute == null)
            {
                Debug.LogWarning("Couldn't process: Studio Compute shader is null");
                return false;
            }

            //force iso level to be within the rage of the current voxel size
            m_currentLODLevel = (int)Math.Min(m_currentLODLevel, numLevelOfDetailLevels);

            GenerateVolume();
            if (!showVolumePreview)
            {
                ExtractSurfaceFromVolume();
            }

            base.OnGenerate();

            ResetGPUResources();

            return true;
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            if(clip.isSetup && radialBiasPersp != null && radialBiasPerspInMeters != null)
            {
                // convert per perspective radial bias from cm to meters
                for (int i = 0; i < radialBiasPersp.Length; ++i)
                {
                    if (overrideRadialBias.Length > 0 && overrideRadialBias[i])
                    {
                        radialBiasPerspInMeters[i].x = Util.cmToMeters(radialBiasPersp[i]);
                    }
                }
            }
            if (enableViewDependentGeometry && globalViewDependentGeometryBlendWeight > 0)
            {
                if (volumeViewpoint.transform.hasChanged)
                {
                    ScheduleGenerate();
                    volumeViewpoint.transform.hasChanged = false;
                }

                if (transform.hasChanged)
                {
                    ScheduleGenerate();
                    transform.hasChanged = false;
                }
            }

            if (automaticLevelOfDetail)
            {
                if (m_mainCamera == null)
                {
                    m_mainCamera = Camera.main;
                }
                //if the main camera position changed and we are using distance based LOD then force an update
                float dist = Vector3.Magnitude((transform.position + volumeBounds.center) - m_mainCamera.transform.position);
                float nrmdist = dist / (m_mainCamera.farClipPlane / levelOfDetailDistance);
                int newLOD = Math.Min((int)((float)numLevelOfDetailLevels * nrmdist), numLevelOfDetailLevels);
#if UNITY_EDITOR
                if (newLOD != currentLevelOfDetailLevel)
                {
                    Generate();
                }
#endif
                currentLevelOfDetailLevel = newLOD;
            }
            if (showVolumePreview)
            {
                DrawDebug();
            }
            else
            {
                //free up the resources when done
                Util.ReleaseComputeBuffer(ref m_pointsBuffer);
            }
        }

        static int[] triangleOffsets = new int[72]
        {
        // 0
        0,0,0,
        1,0,0,
        // 1
        1,0,0,
        1,0,1,
        // 2
        1,0,1,
        0,0,1,
        // 3
        0,0,1,
        0,0,0,
        // 4
        0,1,0,
        1,1,0,
        // 5
        1,1,0,
        1,1,1,
        // 6
        1,1,1,
        0,1,1,
        // 7
        0,1,1,
        0,1,0,
        // 8
        0,0,0,
        0,1,0,
        // 9
        1,0,0,
        1,1,0,
        // 10
        1,0,1,
        1,1,1,
        // 11
        0,0,1,
        0,1,1
        };

        //  For each of the possible vertex states listed in cubeEdgeFlags there is a specific triangulation
        //  of the edge intersection points.  triangleConnectionTable lists all of them in the form of
        //  0-5 edge triples with the list terminated by the invalid value -1.
        //  For example: triangleConnectionTable[3] list the 2 triangles formed when corner[0] 
        //  and corner[1] are inside of the surface, but the rest of the cube is not.
        //  triangleConnectionTable[256][16]
        static int[,] triangleConnectionTable = new int[,]
        {
            {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {0, 1, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {1, 8, 3, 9, 8, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {0, 8, 3, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {9, 2, 10, 0, 2, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {2, 8, 3, 2, 10, 8, 10, 9, 8, -1, -1, -1, -1, -1, -1, -1},
            {3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {0, 11, 2, 8, 11, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {1, 9, 0, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {1, 11, 2, 1, 9, 11, 9, 8, 11, -1, -1, -1, -1, -1, -1, -1},
            {3, 10, 1, 11, 10, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {0, 10, 1, 0, 8, 10, 8, 11, 10, -1, -1, -1, -1, -1, -1, -1},
            {3, 9, 0, 3, 11, 9, 11, 10, 9, -1, -1, -1, -1, -1, -1, -1},
            {9, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {4, 3, 0, 7, 3, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {0, 1, 9, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {4, 1, 9, 4, 7, 1, 7, 3, 1, -1, -1, -1, -1, -1, -1, -1},
            {1, 2, 10, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {3, 4, 7, 3, 0, 4, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1},
            {9, 2, 10, 9, 0, 2, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1},
            {2, 10, 9, 2, 9, 7, 2, 7, 3, 7, 9, 4, -1, -1, -1, -1},
            {8, 4, 7, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {11, 4, 7, 11, 2, 4, 2, 0, 4, -1, -1, -1, -1, -1, -1, -1},
            {9, 0, 1, 8, 4, 7, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1},
            {4, 7, 11, 9, 4, 11, 9, 11, 2, 9, 2, 1, -1, -1, -1, -1},
            {3, 10, 1, 3, 11, 10, 7, 8, 4, -1, -1, -1, -1, -1, -1, -1},
            {1, 11, 10, 1, 4, 11, 1, 0, 4, 7, 11, 4, -1, -1, -1, -1},
            {4, 7, 8, 9, 0, 11, 9, 11, 10, 11, 0, 3, -1, -1, -1, -1},
            {4, 7, 11, 4, 11, 9, 9, 11, 10, -1, -1, -1, -1, -1, -1, -1},
            {9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {9, 5, 4, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {0, 5, 4, 1, 5, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {8, 5, 4, 8, 3, 5, 3, 1, 5, -1, -1, -1, -1, -1, -1, -1},
            {1, 2, 10, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {3, 0, 8, 1, 2, 10, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1},
            {5, 2, 10, 5, 4, 2, 4, 0, 2, -1, -1, -1, -1, -1, -1, -1},
            {2, 10, 5, 3, 2, 5, 3, 5, 4, 3, 4, 8, -1, -1, -1, -1},
            {9, 5, 4, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {0, 11, 2, 0, 8, 11, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1},
            {0, 5, 4, 0, 1, 5, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1},
            {2, 1, 5, 2, 5, 8, 2, 8, 11, 4, 8, 5, -1, -1, -1, -1},
            {10, 3, 11, 10, 1, 3, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1},
            {4, 9, 5, 0, 8, 1, 8, 10, 1, 8, 11, 10, -1, -1, -1, -1},
            {5, 4, 0, 5, 0, 11, 5, 11, 10, 11, 0, 3, -1, -1, -1, -1},
            {5, 4, 8, 5, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1},
            {9, 7, 8, 5, 7, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {9, 3, 0, 9, 5, 3, 5, 7, 3, -1, -1, -1, -1, -1, -1, -1},
            {0, 7, 8, 0, 1, 7, 1, 5, 7, -1, -1, -1, -1, -1, -1, -1},
            {1, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {9, 7, 8, 9, 5, 7, 10, 1, 2, -1, -1, -1, -1, -1, -1, -1},
            {10, 1, 2, 9, 5, 0, 5, 3, 0, 5, 7, 3, -1, -1, -1, -1},
            {8, 0, 2, 8, 2, 5, 8, 5, 7, 10, 5, 2, -1, -1, -1, -1},
            {2, 10, 5, 2, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1},
            {7, 9, 5, 7, 8, 9, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1},
            {9, 5, 7, 9, 7, 2, 9, 2, 0, 2, 7, 11, -1, -1, -1, -1},
            {2, 3, 11, 0, 1, 8, 1, 7, 8, 1, 5, 7, -1, -1, -1, -1},
            {11, 2, 1, 11, 1, 7, 7, 1, 5, -1, -1, -1, -1, -1, -1, -1},
            {9, 5, 8, 8, 5, 7, 10, 1, 3, 10, 3, 11, -1, -1, -1, -1},
            {5, 7, 0, 5, 0, 9, 7, 11, 0, 1, 0, 10, 11, 10, 0, -1},
            {11, 10, 0, 11, 0, 3, 10, 5, 0, 8, 0, 7, 5, 7, 0, -1},
            {11, 10, 5, 7, 11, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {0, 8, 3, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {9, 0, 1, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {1, 8, 3, 1, 9, 8, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1},
            {1, 6, 5, 2, 6, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {1, 6, 5, 1, 2, 6, 3, 0, 8, -1, -1, -1, -1, -1, -1, -1},
            {9, 6, 5, 9, 0, 6, 0, 2, 6, -1, -1, -1, -1, -1, -1, -1},
            {5, 9, 8, 5, 8, 2, 5, 2, 6, 3, 2, 8, -1, -1, -1, -1},
            {2, 3, 11, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {11, 0, 8, 11, 2, 0, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1},
            {0, 1, 9, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1},
            {5, 10, 6, 1, 9, 2, 9, 11, 2, 9, 8, 11, -1, -1, -1, -1},
            {6, 3, 11, 6, 5, 3, 5, 1, 3, -1, -1, -1, -1, -1, -1, -1},
            {0, 8, 11, 0, 11, 5, 0, 5, 1, 5, 11, 6, -1, -1, -1, -1},
            {3, 11, 6, 0, 3, 6, 0, 6, 5, 0, 5, 9, -1, -1, -1, -1},
            {6, 5, 9, 6, 9, 11, 11, 9, 8, -1, -1, -1, -1, -1, -1, -1},
            {5, 10, 6, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {4, 3, 0, 4, 7, 3, 6, 5, 10, -1, -1, -1, -1, -1, -1, -1},
            {1, 9, 0, 5, 10, 6, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1},
            {10, 6, 5, 1, 9, 7, 1, 7, 3, 7, 9, 4, -1, -1, -1, -1},
            {6, 1, 2, 6, 5, 1, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1},
            {1, 2, 5, 5, 2, 6, 3, 0, 4, 3, 4, 7, -1, -1, -1, -1},
            {8, 4, 7, 9, 0, 5, 0, 6, 5, 0, 2, 6, -1, -1, -1, -1},
            {7, 3, 9, 7, 9, 4, 3, 2, 9, 5, 9, 6, 2, 6, 9, -1},
            {3, 11, 2, 7, 8, 4, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1},
            {5, 10, 6, 4, 7, 2, 4, 2, 0, 2, 7, 11, -1, -1, -1, -1},
            {0, 1, 9, 4, 7, 8, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1},
            {9, 2, 1, 9, 11, 2, 9, 4, 11, 7, 11, 4, 5, 10, 6, -1},
            {8, 4, 7, 3, 11, 5, 3, 5, 1, 5, 11, 6, -1, -1, -1, -1},
            {5, 1, 11, 5, 11, 6, 1, 0, 11, 7, 11, 4, 0, 4, 11, -1},
            {0, 5, 9, 0, 6, 5, 0, 3, 6, 11, 6, 3, 8, 4, 7, -1},
            {6, 5, 9, 6, 9, 11, 4, 7, 9, 7, 11, 9, -1, -1, -1, -1},
            {10, 4, 9, 6, 4, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {4, 10, 6, 4, 9, 10, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1},
            {10, 0, 1, 10, 6, 0, 6, 4, 0, -1, -1, -1, -1, -1, -1, -1},
            {8, 3, 1, 8, 1, 6, 8, 6, 4, 6, 1, 10, -1, -1, -1, -1},
            {1, 4, 9, 1, 2, 4, 2, 6, 4, -1, -1, -1, -1, -1, -1, -1},
            {3, 0, 8, 1, 2, 9, 2, 4, 9, 2, 6, 4, -1, -1, -1, -1},
            {0, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {8, 3, 2, 8, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1},
            {10, 4, 9, 10, 6, 4, 11, 2, 3, -1, -1, -1, -1, -1, -1, -1},
            {0, 8, 2, 2, 8, 11, 4, 9, 10, 4, 10, 6, -1, -1, -1, -1},
            {3, 11, 2, 0, 1, 6, 0, 6, 4, 6, 1, 10, -1, -1, -1, -1},
            {6, 4, 1, 6, 1, 10, 4, 8, 1, 2, 1, 11, 8, 11, 1, -1},
            {9, 6, 4, 9, 3, 6, 9, 1, 3, 11, 6, 3, -1, -1, -1, -1},
            {8, 11, 1, 8, 1, 0, 11, 6, 1, 9, 1, 4, 6, 4, 1, -1},
            {3, 11, 6, 3, 6, 0, 0, 6, 4, -1, -1, -1, -1, -1, -1, -1},
            {6, 4, 8, 11, 6, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {7, 10, 6, 7, 8, 10, 8, 9, 10, -1, -1, -1, -1, -1, -1, -1},
            {0, 7, 3, 0, 10, 7, 0, 9, 10, 6, 7, 10, -1, -1, -1, -1},
            {10, 6, 7, 1, 10, 7, 1, 7, 8, 1, 8, 0, -1, -1, -1, -1},
            {10, 6, 7, 10, 7, 1, 1, 7, 3, -1, -1, -1, -1, -1, -1, -1},
            {1, 2, 6, 1, 6, 8, 1, 8, 9, 8, 6, 7, -1, -1, -1, -1},
            {2, 6, 9, 2, 9, 1, 6, 7, 9, 0, 9, 3, 7, 3, 9, -1},
            {7, 8, 0, 7, 0, 6, 6, 0, 2, -1, -1, -1, -1, -1, -1, -1},
            {7, 3, 2, 6, 7, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {2, 3, 11, 10, 6, 8, 10, 8, 9, 8, 6, 7, -1, -1, -1, -1},
            {2, 0, 7, 2, 7, 11, 0, 9, 7, 6, 7, 10, 9, 10, 7, -1},
            {1, 8, 0, 1, 7, 8, 1, 10, 7, 6, 7, 10, 2, 3, 11, -1},
            {11, 2, 1, 11, 1, 7, 10, 6, 1, 6, 7, 1, -1, -1, -1, -1},
            {8, 9, 6, 8, 6, 7, 9, 1, 6, 11, 6, 3, 1, 3, 6, -1},
            {0, 9, 1, 11, 6, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {7, 8, 0, 7, 0, 6, 3, 11, 0, 11, 6, 0, -1, -1, -1, -1},
            {7, 11, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {3, 0, 8, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {0, 1, 9, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {8, 1, 9, 8, 3, 1, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1},
            {10, 1, 2, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {1, 2, 10, 3, 0, 8, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1},
            {2, 9, 0, 2, 10, 9, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1},
            {6, 11, 7, 2, 10, 3, 10, 8, 3, 10, 9, 8, -1, -1, -1, -1},
            {7, 2, 3, 6, 2, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {7, 0, 8, 7, 6, 0, 6, 2, 0, -1, -1, -1, -1, -1, -1, -1},
            {2, 7, 6, 2, 3, 7, 0, 1, 9, -1, -1, -1, -1, -1, -1, -1},
            {1, 6, 2, 1, 8, 6, 1, 9, 8, 8, 7, 6, -1, -1, -1, -1},
            {10, 7, 6, 10, 1, 7, 1, 3, 7, -1, -1, -1, -1, -1, -1, -1},
            {10, 7, 6, 1, 7, 10, 1, 8, 7, 1, 0, 8, -1, -1, -1, -1},
            {0, 3, 7, 0, 7, 10, 0, 10, 9, 6, 10, 7, -1, -1, -1, -1},
            {7, 6, 10, 7, 10, 8, 8, 10, 9, -1, -1, -1, -1, -1, -1, -1},
            {6, 8, 4, 11, 8, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {3, 6, 11, 3, 0, 6, 0, 4, 6, -1, -1, -1, -1, -1, -1, -1},
            {8, 6, 11, 8, 4, 6, 9, 0, 1, -1, -1, -1, -1, -1, -1, -1},
            {9, 4, 6, 9, 6, 3, 9, 3, 1, 11, 3, 6, -1, -1, -1, -1},
            {6, 8, 4, 6, 11, 8, 2, 10, 1, -1, -1, -1, -1, -1, -1, -1},
            {1, 2, 10, 3, 0, 11, 0, 6, 11, 0, 4, 6, -1, -1, -1, -1},
            {4, 11, 8, 4, 6, 11, 0, 2, 9, 2, 10, 9, -1, -1, -1, -1},
            {10, 9, 3, 10, 3, 2, 9, 4, 3, 11, 3, 6, 4, 6, 3, -1},
            {8, 2, 3, 8, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1},
            {0, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {1, 9, 0, 2, 3, 4, 2, 4, 6, 4, 3, 8, -1, -1, -1, -1},
            {1, 9, 4, 1, 4, 2, 2, 4, 6, -1, -1, -1, -1, -1, -1, -1},
            {8, 1, 3, 8, 6, 1, 8, 4, 6, 6, 10, 1, -1, -1, -1, -1},
            {10, 1, 0, 10, 0, 6, 6, 0, 4, -1, -1, -1, -1, -1, -1, -1},
            {4, 6, 3, 4, 3, 8, 6, 10, 3, 0, 3, 9, 10, 9, 3, -1},
            {10, 9, 4, 6, 10, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {4, 9, 5, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {0, 8, 3, 4, 9, 5, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1},
            {5, 0, 1, 5, 4, 0, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1},
            {11, 7, 6, 8, 3, 4, 3, 5, 4, 3, 1, 5, -1, -1, -1, -1},
            {9, 5, 4, 10, 1, 2, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1},
            {6, 11, 7, 1, 2, 10, 0, 8, 3, 4, 9, 5, -1, -1, -1, -1},
            {7, 6, 11, 5, 4, 10, 4, 2, 10, 4, 0, 2, -1, -1, -1, -1},
            {3, 4, 8, 3, 5, 4, 3, 2, 5, 10, 5, 2, 11, 7, 6, -1},
            {7, 2, 3, 7, 6, 2, 5, 4, 9, -1, -1, -1, -1, -1, -1, -1},
            {9, 5, 4, 0, 8, 6, 0, 6, 2, 6, 8, 7, -1, -1, -1, -1},
            {3, 6, 2, 3, 7, 6, 1, 5, 0, 5, 4, 0, -1, -1, -1, -1},
            {6, 2, 8, 6, 8, 7, 2, 1, 8, 4, 8, 5, 1, 5, 8, -1},
            {9, 5, 4, 10, 1, 6, 1, 7, 6, 1, 3, 7, -1, -1, -1, -1},
            {1, 6, 10, 1, 7, 6, 1, 0, 7, 8, 7, 0, 9, 5, 4, -1},
            {4, 0, 10, 4, 10, 5, 0, 3, 10, 6, 10, 7, 3, 7, 10, -1},
            {7, 6, 10, 7, 10, 8, 5, 4, 10, 4, 8, 10, -1, -1, -1, -1},
            {6, 9, 5, 6, 11, 9, 11, 8, 9, -1, -1, -1, -1, -1, -1, -1},
            {3, 6, 11, 0, 6, 3, 0, 5, 6, 0, 9, 5, -1, -1, -1, -1},
            {0, 11, 8, 0, 5, 11, 0, 1, 5, 5, 6, 11, -1, -1, -1, -1},
            {6, 11, 3, 6, 3, 5, 5, 3, 1, -1, -1, -1, -1, -1, -1, -1},
            {1, 2, 10, 9, 5, 11, 9, 11, 8, 11, 5, 6, -1, -1, -1, -1},
            {0, 11, 3, 0, 6, 11, 0, 9, 6, 5, 6, 9, 1, 2, 10, -1},
            {11, 8, 5, 11, 5, 6, 8, 0, 5, 10, 5, 2, 0, 2, 5, -1},
            {6, 11, 3, 6, 3, 5, 2, 10, 3, 10, 5, 3, -1, -1, -1, -1},
            {5, 8, 9, 5, 2, 8, 5, 6, 2, 3, 8, 2, -1, -1, -1, -1},
            {9, 5, 6, 9, 6, 0, 0, 6, 2, -1, -1, -1, -1, -1, -1, -1},
            {1, 5, 8, 1, 8, 0, 5, 6, 8, 3, 8, 2, 6, 2, 8, -1},
            {1, 5, 6, 2, 1, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {1, 3, 6, 1, 6, 10, 3, 8, 6, 5, 6, 9, 8, 9, 6, -1},
            {10, 1, 0, 10, 0, 6, 9, 5, 0, 5, 6, 0, -1, -1, -1, -1},
            {0, 3, 8, 5, 6, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {10, 5, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {11, 5, 10, 7, 5, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {11, 5, 10, 11, 7, 5, 8, 3, 0, -1, -1, -1, -1, -1, -1, -1},
            {5, 11, 7, 5, 10, 11, 1, 9, 0, -1, -1, -1, -1, -1, -1, -1},
            {10, 7, 5, 10, 11, 7, 9, 8, 1, 8, 3, 1, -1, -1, -1, -1},
            {11, 1, 2, 11, 7, 1, 7, 5, 1, -1, -1, -1, -1, -1, -1, -1},
            {0, 8, 3, 1, 2, 7, 1, 7, 5, 7, 2, 11, -1, -1, -1, -1},
            {9, 7, 5, 9, 2, 7, 9, 0, 2, 2, 11, 7, -1, -1, -1, -1},
            {7, 5, 2, 7, 2, 11, 5, 9, 2, 3, 2, 8, 9, 8, 2, -1},
            {2, 5, 10, 2, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1},
            {8, 2, 0, 8, 5, 2, 8, 7, 5, 10, 2, 5, -1, -1, -1, -1},
            {9, 0, 1, 5, 10, 3, 5, 3, 7, 3, 10, 2, -1, -1, -1, -1},
            {9, 8, 2, 9, 2, 1, 8, 7, 2, 10, 2, 5, 7, 5, 2, -1},
            {1, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {0, 8, 7, 0, 7, 1, 1, 7, 5, -1, -1, -1, -1, -1, -1, -1},
            {9, 0, 3, 9, 3, 5, 5, 3, 7, -1, -1, -1, -1, -1, -1, -1},
            {9, 8, 7, 5, 9, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {5, 8, 4, 5, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1},
            {5, 0, 4, 5, 11, 0, 5, 10, 11, 11, 3, 0, -1, -1, -1, -1},
            {0, 1, 9, 8, 4, 10, 8, 10, 11, 10, 4, 5, -1, -1, -1, -1},
            {10, 11, 4, 10, 4, 5, 11, 3, 4, 9, 4, 1, 3, 1, 4, -1},
            {2, 5, 1, 2, 8, 5, 2, 11, 8, 4, 5, 8, -1, -1, -1, -1},
            {0, 4, 11, 0, 11, 3, 4, 5, 11, 2, 11, 1, 5, 1, 11, -1},
            {0, 2, 5, 0, 5, 9, 2, 11, 5, 4, 5, 8, 11, 8, 5, -1},
            {9, 4, 5, 2, 11, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {2, 5, 10, 3, 5, 2, 3, 4, 5, 3, 8, 4, -1, -1, -1, -1},
            {5, 10, 2, 5, 2, 4, 4, 2, 0, -1, -1, -1, -1, -1, -1, -1},
            {3, 10, 2, 3, 5, 10, 3, 8, 5, 4, 5, 8, 0, 1, 9, -1},
            {5, 10, 2, 5, 2, 4, 1, 9, 2, 9, 4, 2, -1, -1, -1, -1},
            {8, 4, 5, 8, 5, 3, 3, 5, 1, -1, -1, -1, -1, -1, -1, -1},
            {0, 4, 5, 1, 0, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {8, 4, 5, 8, 5, 3, 9, 0, 5, 0, 3, 5, -1, -1, -1, -1},
            {9, 4, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {4, 11, 7, 4, 9, 11, 9, 10, 11, -1, -1, -1, -1, -1, -1, -1},
            {0, 8, 3, 4, 9, 7, 9, 11, 7, 9, 10, 11, -1, -1, -1, -1},
            {1, 10, 11, 1, 11, 4, 1, 4, 0, 7, 4, 11, -1, -1, -1, -1},
            {3, 1, 4, 3, 4, 8, 1, 10, 4, 7, 4, 11, 10, 11, 4, -1},
            {4, 11, 7, 9, 11, 4, 9, 2, 11, 9, 1, 2, -1, -1, -1, -1},
            {9, 7, 4, 9, 11, 7, 9, 1, 11, 2, 11, 1, 0, 8, 3, -1},
            {11, 7, 4, 11, 4, 2, 2, 4, 0, -1, -1, -1, -1, -1, -1, -1},
            {11, 7, 4, 11, 4, 2, 8, 3, 4, 3, 2, 4, -1, -1, -1, -1},
            {2, 9, 10, 2, 7, 9, 2, 3, 7, 7, 4, 9, -1, -1, -1, -1},
            {9, 10, 7, 9, 7, 4, 10, 2, 7, 8, 7, 0, 2, 0, 7, -1},
            {3, 7, 10, 3, 10, 2, 7, 4, 10, 1, 10, 0, 4, 0, 10, -1},
            {1, 10, 2, 8, 7, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {4, 9, 1, 4, 1, 7, 7, 1, 3, -1, -1, -1, -1, -1, -1, -1},
            {4, 9, 1, 4, 1, 7, 0, 8, 1, 8, 7, 1, -1, -1, -1, -1},
            {4, 0, 3, 7, 4, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {4, 8, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {9, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {3, 0, 9, 3, 9, 11, 11, 9, 10, -1, -1, -1, -1, -1, -1, -1},
            {0, 1, 10, 0, 10, 8, 8, 10, 11, -1, -1, -1, -1, -1, -1, -1},
            {3, 1, 10, 11, 3, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {1, 2, 11, 1, 11, 9, 9, 11, 8, -1, -1, -1, -1, -1, -1, -1},
            {3, 0, 9, 3, 9, 11, 1, 2, 9, 2, 11, 9, -1, -1, -1, -1},
            {0, 2, 11, 8, 0, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {3, 2, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {2, 3, 8, 2, 8, 10, 10, 8, 9, -1, -1, -1, -1, -1, -1, -1},
            {9, 10, 2, 0, 9, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {2, 3, 8, 2, 8, 10, 0, 1, 8, 1, 10, 8, -1, -1, -1, -1},
            {1, 10, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {1, 3, 8, 9, 1, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {0, 9, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {0, 3, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1},
            {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1}
        };

        //number of triangles to use per classification
        static int[] nrOfTriangles = new int[256] { 0, 1, 1, 2, 1, 2, 2, 3, 1, 2, 2, 3, 2, 3, 3, 2, 1, 2, 2, 3, 2, 3, 3, 4, 2, 3, 3, 4, 3, 4, 4, 3, 1, 2, 2, 3, 2, 3, 3, 4, 2, 3, 3, 4, 3, 4, 4, 3, 2, 3, 3, 2, 3, 4, 4, 3, 3, 4, 4, 3, 4, 5, 5, 2, 1, 2, 2, 3, 2, 3, 3, 4, 2, 3, 3, 4, 3, 4, 4, 3, 2, 3, 3, 4, 3, 4, 4, 5, 3, 4, 4, 5, 4, 5, 5, 4, 2, 3, 3, 4, 3, 4, 2, 3, 3, 4, 4, 5, 4, 5, 3, 2, 3, 4, 4, 3, 4, 5, 3, 2, 4, 5, 5, 4, 5, 2, 4, 1, 1, 2, 2, 3, 2, 3, 3, 4, 2, 3, 3, 4, 3, 4, 4, 3, 2, 3, 3, 4, 3, 4, 4, 5, 3, 2, 4, 3, 4, 3, 5, 2, 2, 3, 3, 4, 3, 4, 4, 5, 3, 4, 4, 5, 4, 5, 5, 4, 3, 4, 4, 3, 4, 5, 5, 4, 4, 3, 5, 2, 5, 4, 2, 1, 2, 3, 3, 4, 3, 4, 4, 5, 3, 4, 4, 5, 2, 3, 3, 2, 3, 4, 4, 5, 4, 5, 5, 2, 4, 3, 5, 4, 3, 2, 4, 1, 3, 4, 4, 5, 4, 5, 3, 4, 4, 5, 5, 2, 3, 4, 2, 1, 2, 3, 3, 2, 3, 4, 2, 1, 3, 2, 4, 1, 2, 1, 1, 0 };

        #region MaskGenerator

        [SerializeField]
        public MaskGenerator maskGenerator = null;

        public void EnsureMaskGenerator()
        {
            if (maskGenerator == null)
            {
                maskGenerator = new MaskGenerator();
            }

            if (clip != null)
            {
                maskGenerator.clip = clip;
                maskGenerator.sliceCount = (int)clip.metadata?.perspectivesCount;
            }
            maskGenerator.Setup();
        }

        [SerializeField]
        private bool m_enableEdgeMask = false;
        public bool enableEdgeMask
        {
            get { return maskGenerator != null ? m_enableEdgeMask : false; }
            set {
                m_enableEdgeMask = value;
                EnsureMaskGenerator();
            }
        }
        #endregion

    }
}