using UnityEngine;
using System.Runtime.InteropServices;
using System;

namespace Depthkit
{
    public enum MeshDensity 
    {
        Low = 8,
        Medium = 4,
        High = 2,
        Full = 1
    }

    public enum NormalGenerationTechnique 
    {
        Smooth,
        Adjustable,
        AdjustableSmoother,
        None,
        DepthCamera
    }

    namespace Core
    {
        public struct Vertex
        {
            public Vector4 uv;
            public Vector3 position;
            public Vector3 normal;
            public uint perspectiveIndex;
            public uint valid;

            public string Print()
            {
                return "uv: " + uv.ToString("F4") + " position: " + position.ToString("F4") + " normal: " + normal.ToString("F4");
            }
        }

        public struct PackedTriangle
        {
            public Vertex vertex0;
            public Vertex vertex1;
            public Vertex vertex2;
        }

        public struct IndexedTriangle
        {
            public uint perspective;
            public uint vertex0;
            public uint vertex1;
            public uint vertex2;
        }
    }

    [System.Serializable]
    public class PackedCoreTriangleSubMesh : SubMesh<Core.PackedTriangle>{}

    [System.Serializable]
    public class IndexedCoreTriangleSubMesh : SubMesh<Core.IndexedTriangle>{}

    [ExecuteInEditMode]
    [AddComponentMenu("Depthkit/Core/Sources/Depthkit Core Mesh Source")]
    public class CoreMeshSource : MeshSource
    {
        public float surfaceTriangleCountPercent = 0.70f;

        [SerializeField, HideInInspector]
        private MeshDensity m_meshDensity = 0;

        public MeshDensity meshDensity
        {
            get
            {
                return (m_meshDensity == 0) ? MeshDensity.Medium : m_meshDensity;
            }
            set
            {
                if (m_meshDensity != value && clip.isSetup)
                {
                    m_meshDensity = value;
                    maxSurfaceTriangles = 0;
                    ScheduleResize();
                    ScheduleGenerate();
                }
            }
        }

        [SerializeField, HideInInspector]
        protected Vector2Int m_latticeResolution = Vector2Int.one;

        [SerializeField, HideInInspector]
        protected uint m_latticeMaxTriangles = 0;
        public uint latticeMaxTriangles
        {
            get
            {
                return m_latticeMaxTriangles;
            }
        }
        public Vector2Int latticeResolution
        {
            get { return m_latticeResolution; }
        }

        public Vector2Int scaledPerspectiveResolution
        {
            get { return new Vector2Int(clip.metadata.perspectiveResolution.x / (int)meshDensity, clip.metadata.perspectiveResolution.y / (int)meshDensity); }
        }

        protected void ResizeLattice()
        {
            // Note: we add 1 to the dimensions here to ensure we have enough verts for one quad per pixel of the CPP
            m_latticeResolution = new Vector2Int(Math.Max(1 + clip.metadata.paddedTextureDimensions.x / (int)meshDensity, 1), Math.Max(1 + clip.metadata.paddedTextureDimensions.y / (int)meshDensity, 1));
            m_latticeMaxTriangles = (uint)(m_latticeResolution.x - 1) * (uint)(m_latticeResolution.y - 1) * 2;
            vertexCount = (uint)m_latticeResolution.x * (uint)m_latticeResolution.y;
        }

        public NormalGenerationTechnique normalGenerationTechnique = NormalGenerationTechnique.None;

        [Range(1.0f, 1500.0f)]
        public float adjustableNormalSlope = 255.0f;

        [Range(0.0f, 1.0f)]
        public float edgeCompressionNoiseThreshold = 0.25f;

        [Range(0.0f, 1.0f)]
        public float clipThreshold = 0.5f;

        public bool ditherEdge = false;

        [Range(0.0f, 0.2f)]
        public float ditherWidth = 0.1f;

        #region VertexBuffer
        public uint vertexCount = 0;
        protected ComputeBuffer m_vertexBuffer;
        public ComputeBuffer vertexBuffer { get { return m_vertexBuffer; } }

        #endregion

        #region TriangleGeneration
        protected enum Phase
        {
            Vertices_Horizontal = 0,
            Vertices_Vertical = 1,
            Normals = 2,
            Triangles = 3
        }

        [SerializeField, HideInInspector]
        protected int m_vertexBufferSlices = 1;

        protected static class CoreShaderIds
        {
            public static readonly int
            _EdgeChoke = Shader.PropertyToID("_EdgeChoke"), //override clip default
            _VertexBuffer = Shader.PropertyToID("_VertexBuffer"),
            _NormalHeight = Shader.PropertyToID("_NormalHeight"),
            _LatticeSize = Shader.PropertyToID("_LatticeSize"), // padded to be multiple of 8
            _PerspectiveTextureSize = Shader.PropertyToID("_PerspectiveTextureSize");
            public static readonly string _DitherEdgeKW = "DK_SCREEN_DOOR_TRANSPARENCY";
        }

        protected virtual string GetComputeShaderName()
        {
            return "Shaders/DataSource/DepthkitCoreMeshSource";
        }

        protected ComputeShader m_generateDataCompute;

        protected virtual string GetKernelNamePostfix(string prefix, bool edgeBuffer, bool edgeMask, int submesh = -1)
        {
            return prefix + (edgeBuffer ? "WEdgeBuffer" : "") + (edgeMask ? "WEdgeMask" : "");
        }

        protected int FindKernelId(Phase phase, bool edgeBuffer, bool edgeMask = false, int submesh = -1)
        {
            switch (phase)
            {
                case Phase.Vertices_Horizontal:
                    return m_generateDataCompute.FindKernel(GetKernelNamePostfix("KGenerateVertices_Horizontal", edgeBuffer, edgeMask, submesh));
                case Phase.Vertices_Vertical:
                    return m_generateDataCompute.FindKernel(GetKernelNamePostfix("KGenerateVertices_Vertical", edgeBuffer, edgeMask, submesh));
                case Phase.Normals:
                    switch (normalGenerationTechnique)
                    {
                        case NormalGenerationTechnique.Smooth:
                            return m_generateDataCompute.FindKernel(GetKernelNamePostfix("KGenerateNormals", edgeBuffer, edgeMask, submesh));
                        case NormalGenerationTechnique.Adjustable:
                            return m_generateDataCompute.FindKernel(GetKernelNamePostfix("KGenerateNormalsAdjustable", edgeBuffer, edgeMask, submesh));
                        case NormalGenerationTechnique.AdjustableSmoother:
                            return m_generateDataCompute.FindKernel(GetKernelNamePostfix("KGenerateNormalsAdjustableSmoother", edgeBuffer, edgeMask, submesh));
                        case NormalGenerationTechnique.DepthCamera:
                            return m_generateDataCompute.FindKernel(GetKernelNamePostfix("KGenerateNormalsDepthCamera", edgeBuffer, edgeMask, submesh));
                        case NormalGenerationTechnique.None:
                            return m_generateDataCompute.FindKernel(GetKernelNamePostfix("KGenerateNormalsNone", edgeBuffer, edgeMask, submesh));
                        default: return -1;
                    }
                case Phase.Triangles:
                    return m_generateDataCompute.FindKernel(GetKernelNamePostfix("KGenerateTriangles", edgeBuffer, edgeMask, submesh));
                default: return -1;
            }
        }

        protected void GenerateVertexBuffer()
        {
            // generate all vertices
            // Vertex generation samples the edge mask to create the vertex data
            int kernel = FindKernelId(Phase.Vertices_Horizontal, false, true);
            GenerateVertices(kernel, m_vertexBufferSlices);

            kernel = FindKernelId(Phase.Vertices_Vertical, false);
            GenerateVertices(kernel, m_vertexBufferSlices);

            kernel = FindKernelId(Phase.Normals, false);
            GenerateNormals(kernel, m_vertexBufferSlices);
        }

        protected virtual void GenerateTriangles()
        {
            int kernel = FindKernelId(Phase.Triangles, false);
            GenerateTriangles(kernel, 1);
        }

        void GenerateVertices(int kernel, int slices = 1)
        {
            clip.SetProperties(ref m_generateDataCompute, kernel);

            // mask props
            maskGenerator.SetProperties(ref m_generateDataCompute, kernel);

            // downscaled mask props
            if (maskGenerator.downScaledMaskTexture != null)
                m_generateDataCompute.SetTexture(kernel, MaskGenerator.MaskGeneratorShaderIds._MaskTexture, maskGenerator.downScaledMaskTexture);

            m_generateDataCompute.SetFloat(CoreShaderIds._EdgeChoke, edgeCompressionNoiseThreshold);
            m_generateDataCompute.SetBuffer(kernel, CoreShaderIds._VertexBuffer, vertexBuffer);
            m_generateDataCompute.SetVector(CoreShaderIds._LatticeSize, new Vector2(m_latticeResolution.x, m_latticeResolution.y));
            m_generateDataCompute.SetVector(CoreShaderIds._PerspectiveTextureSize, new Vector2(scaledPerspectiveResolution.x, scaledPerspectiveResolution.y));
            m_generateDataCompute.SetVectorArray(MeshSourceShaderIds._RadialBiasPerspInMeters, radialBiasPerspInMeters);

            Util.DispatchGroups(m_generateDataCompute, kernel, m_latticeResolution.x, m_latticeResolution.y, slices);
        }

        void GenerateNormals(int kernel, int perspectives = 1)
        {
            clip.SetProperties(ref m_generateDataCompute, kernel);
            m_generateDataCompute.SetFloat(CoreShaderIds._EdgeChoke, edgeCompressionNoiseThreshold);
            m_generateDataCompute.SetBuffer(kernel, CoreShaderIds._VertexBuffer, vertexBuffer);
            m_generateDataCompute.SetVector(CoreShaderIds._LatticeSize, new Vector2(m_latticeResolution.x, m_latticeResolution.y));
            m_generateDataCompute.SetVector(CoreShaderIds._PerspectiveTextureSize, new Vector2(scaledPerspectiveResolution.x, scaledPerspectiveResolution.y));
            if (normalGenerationTechnique == NormalGenerationTechnique.Adjustable || normalGenerationTechnique == NormalGenerationTechnique.AdjustableSmoother)
                m_generateDataCompute.SetFloat(CoreShaderIds._NormalHeight, adjustableNormalSlope);
            Util.DispatchGroups(m_generateDataCompute, kernel, m_latticeResolution.x, m_latticeResolution.y, perspectives);
        }

        void GenerateTriangles(int kernel, int perspectives = 1)
        {
            triangleBuffer.SetCounterValue(0);

            clip.SetProperties(ref m_generateDataCompute, kernel);
            m_generateDataCompute.SetFloat(CoreShaderIds._EdgeChoke, edgeCompressionNoiseThreshold);
            m_generateDataCompute.SetBuffer(kernel, CoreShaderIds._VertexBuffer, vertexBuffer);
            m_generateDataCompute.SetVector(CoreShaderIds._LatticeSize, new Vector2(m_latticeResolution.x, m_latticeResolution.y));
            m_generateDataCompute.SetVector(CoreShaderIds._PerspectiveTextureSize, new Vector2(scaledPerspectiveResolution.x, scaledPerspectiveResolution.y));
            m_generateDataCompute.SetBuffer(kernel, SubMesh.TriangleDataShaderIds._TriangleBuffer, triangleBuffer);

            Util.DispatchGroups(m_generateDataCompute, kernel, scaledPerspectiveResolution.x, scaledPerspectiveResolution.y, perspectives);
        }
        #endregion

        #region DataSource
        public override string DataSourceName()
        {
            return "Depthkit Core Mesh Source";
        }

        protected void EnsureVertexBuffer()
        {
            if (vertexCount > 0)
            {
                if (Util.EnsureComputeBuffer(ComputeBufferType.Structured, ref m_vertexBuffer, (int)vertexCount, Marshal.SizeOf(typeof(Core.Vertex))))
                {
                    m_vertexBuffer.name = "Depthkit Core Mesh Source Vertex Buffer";
                }
            }
        }
        protected override void AcquireResources()
        {
            base.AcquireResources();
            if (m_latticeResolution.x > 0 && m_latticeResolution.y > 0)
            {
                if (clip != null && maskGenerator != null && maskGenerator.clip == null) maskGenerator.clip = clip;
            }

            EnsureVertexBuffer();
        }

        protected override void FreeResources()
        {
            Util.ReleaseComputeBuffer(ref m_vertexBuffer);
            maskGenerator?.Release();
            base.FreeResources();
        }

        public override bool OnSetup()
        {
            Util.EnsureComputeShader(ref m_generateDataCompute, GetComputeShaderName());
            EnsureMaskGenerator();
            return base.OnSetup();
        }

        //allows us to call mesh source onResize directly from inherited classes
        protected bool baseResize()
        {
            return base.OnResize();
        }

        protected override bool OnResize()
        {
            if (m_clip.isSetup)
            {
                m_vertexBufferSlices = 1;

                ReserveSubMeshes<IndexedCoreTriangleSubMesh>(1);

                ResizeLattice();

                if (maxSurfaceTriangles == 0 || !useTriangleMesh)
                {
                    if (useTriangleMesh)
                    {
                        maxSurfaceTriangles = (uint)(surfaceTriangleCountPercent * latticeMaxTriangles);
                    }
                    else
                    {
                        maxSurfaceTriangles = latticeMaxTriangles;
                    }
                }

                CurrentSubMesh().Init();
                EnsureVertexBuffer();

                EnsureMaskGenerator();

                return baseResize();
            }
            return false;
        }

        protected virtual void GenerateEdgeMask()
        {
            maskGenerator.EnsureTexture();
            maskGenerator.GenerateMask();
            //set thresholds
            maskGenerator.perspectivesToSlice[0].y = clipThreshold;
            maskGenerator.perspectivesToSlice[0].z = ditherWidth;
        }

        protected override bool OnGenerate()
        {
            if(clip.cppTexture == null) return false;

            GenerateEdgeMask();

            GenerateVertexBuffer();
            GenerateTriangles();

            bool recomputeMaxTriangles = recalculateCurrentSurfaceTriangleCount;
            if (base.OnGenerate())
            {
                // base.OnGenerate will recalculate the current surface triangle count if needed and reset the variable to false.
                if(recomputeMaxTriangles && !recalculateCurrentSurfaceTriangleCount)
                {
                    // update surfaceTriangleCountPercent
                    surfaceTriangleCountPercent = (float)maxSurfaceTriangles / (float)m_latticeMaxTriangles ;
                }
                return true;
            }
            else
                return false;
        }

        #endregion

        #region IPropertyTransfer

        public override void SetProperties(ref ComputeShader compute, int kernel)
        {
            compute.SetBuffer(kernel, CoreShaderIds._VertexBuffer, vertexBuffer);
            maskGenerator?.SetProperties(ref compute, kernel);
            base.SetProperties(ref compute, kernel);
            compute.SetFloat(CoreShaderIds._EdgeChoke, edgeCompressionNoiseThreshold);
        }

        public override void SetProperties(ref Material material)
        {
            material.SetBuffer(CoreShaderIds._VertexBuffer, vertexBuffer);
            maskGenerator?.SetProperties(ref material);
            base.SetProperties(ref material);
            material.SetFloat(CoreShaderIds._EdgeChoke, edgeCompressionNoiseThreshold);
            Util.EnsureKeyword(ref material, CoreShaderIds._DitherEdgeKW, ditherEdge);
        }

        public override void SetProperties(ref Material material, ref MaterialPropertyBlock block)
        {
            block.SetBuffer(CoreShaderIds._VertexBuffer, vertexBuffer);
            maskGenerator?.SetProperties(ref material, ref block);
            base.SetProperties(ref material, ref block);
            block.SetFloat(CoreShaderIds._EdgeChoke, edgeCompressionNoiseThreshold);
            Util.EnsureKeyword(ref material, CoreShaderIds._DitherEdgeKW, ditherEdge);
        }
        #endregion

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
            }
            maskGenerator.Setup();
        }

        #endregion
    }
}