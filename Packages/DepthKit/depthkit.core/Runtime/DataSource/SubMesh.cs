using System.Collections;
using UnityEngine;
using UnityEngine.XR;
using System.Runtime.InteropServices;

namespace Depthkit
{
    [System.Serializable]
    public abstract class SubMesh : IPropertyTransfer
    {
        public uint maxTriangles;

        #region TriangleData

        public static class TriangleDataShaderIds
        {
            public static readonly int
                _TriangleBuffer = Shader.PropertyToID("_TriangleBuffer"),
                _TrianglesDispatchIndirectArgs = Shader.PropertyToID("_TrianglesDispatchIndirectArgs"),
                _TrianglesCount = Shader.PropertyToID("_TrianglesCount"),
                _TrianglesDrawIndirectArgs = Shader.PropertyToID("_TrianglesDrawIndirectArgs");
        }

        public abstract int GetDataTypeSizeInBytes();

        protected ComputeBuffer m_triangleBuffer;
        protected ComputeBuffer m_trianglesCount = null;
        protected ComputeBuffer m_dispatchIndirectArgs = null;
        protected ComputeBuffer m_drawIndirectArgs = null;

        //returns appendable buffer that contains packed valid triangle data
        public ComputeBuffer triangleBuffer { get { return m_triangleBuffer; } }

        //returns a buffer that contains only the count of appended triangles
        public ComputeBuffer trianglesCount { get { return m_trianglesCount; } }

        //returns buffer with triangle count in the x dispatch for DispatchIndirect
        public ComputeBuffer dispatchIndirectArgs { get { return m_dispatchIndirectArgs; } }

        //returns buffer with drawable vertex count and instanced stereo count for DrawIndirect
        public ComputeBuffer drawIndirectArgs { get { return m_drawIndirectArgs; } }

        #endregion

        #region BufferManagement
        public virtual void Init(int subMeshIndex = -1)
        {
            if (maxTriangles == 0)
            {
                maxTriangles = 200000;
            }

            EnsureBuffers(subMeshIndex);
            triangleBuffer.SetCounterValue(0);

            if (useTriangleMesh)
            {
                triangleMesh.EnsureTriangleMesh((int)maxTriangles);
            }
        }

        public void CopyTriangleCount()
        {
            ComputeBuffer.CopyCount(triangleBuffer, trianglesCount, 0);
        }

        public uint calculateMaxTrianglesNeeded() {
            int[] bufferDataTris = new int[1];
            trianglesCount.GetData(bufferDataTris);
            return (uint)bufferDataTris[0] + (uint)((float)bufferDataTris[0] * 0.2f);   // set 20% higher to account for frame variations in triangle count
        }

        public void PrepareDrawArgs(bool forceStereo)
        {
            Util.ArgsBufferPrep.PrepareDrawArgs(trianglesCount, drawIndirectArgs, forceStereo);
        }

        public void PrepareDispatchArgs(int groupSize, int dispatchY = 1, int dispatchZ = 1)
        {
            Util.ArgsBufferPrep.PrepareDispatchArgs(trianglesCount, dispatchIndirectArgs, groupSize, dispatchY, dispatchZ);
        }

        #endregion

        #region LifeCycle
        public virtual void EnsureBuffers(int submeshIndex = -1)
        {
            if (maxTriangles > 0)
            {
                if (Util.EnsureComputeBuffer(ComputeBufferType.Append, ref m_triangleBuffer, (int)maxTriangles, GetDataTypeSizeInBytes()))
                {
                    triangleBuffer.name = "Depthkit Mesh Source Triangles Buffer " + (submeshIndex == -1 ? "" : submeshIndex.ToString());
                }
                if (Util.EnsureComputeBuffer(ComputeBufferType.IndirectArguments, ref m_trianglesCount, 1, sizeof(int)))
                {
                    trianglesCount.name = "Depthkit Mesh Source Triangles Count Buffer " + (submeshIndex == -1 ? "" : submeshIndex.ToString());
                    trianglesCount.SetData(new int[] { 0 });
                }
                if (Util.EnsureComputeBuffer(ComputeBufferType.IndirectArguments, ref m_dispatchIndirectArgs, 3, sizeof(int)))
                {
                    dispatchIndirectArgs.name = "Depthkit Mesh Source Triangles Dispatch Args Buffer " + (submeshIndex == -1 ? "" : submeshIndex.ToString());
                    dispatchIndirectArgs.SetData(new int[] { 0, 1, 1 });
                }
                if (Util.EnsureComputeBuffer(ComputeBufferType.IndirectArguments, ref m_drawIndirectArgs, 4, sizeof(int)))
                {
                    drawIndirectArgs.name = "Depthkit Mesh Source Triangles Indirect Draw Args Buffer " + (submeshIndex == -1 ? "" : submeshIndex.ToString());
                    drawIndirectArgs.SetData(new int[] { 0, (XRSettings.stereoRenderingMode == XRSettings.StereoRenderingMode.SinglePassInstanced || XRSettings.stereoRenderingMode == XRSettings.StereoRenderingMode.SinglePassMultiview) ? 2 : 1, 0, 0 });
                }
            }

            if (useTriangleMesh) triangleMesh.EnsureTriangleMesh((int)maxTriangles);
        }

        public virtual void Release()
        {
            Util.ReleaseComputeBuffer(ref m_triangleBuffer);
            Util.ReleaseComputeBuffer(ref m_trianglesCount);
            Util.ReleaseComputeBuffer(ref m_dispatchIndirectArgs);
            Util.ReleaseComputeBuffer(ref m_drawIndirectArgs);
            if (useTriangleMesh && m_triangleMesh != null) m_triangleMesh.ReleaseMesh();
        }
        #endregion

        #region TriangleMesh

        [SerializeField]
        private MeshSource m_source = null;

        public MeshSource source
        {
            set { m_source = value; }
        }

        public bool useTriangleMesh = false;

        private Depthkit.TriangleMesh m_triangleMesh;
        public Depthkit.TriangleMesh triangleMesh
        {
            get
            {
                if (m_triangleMesh == null)
                {
                    if (useTriangleMesh)
                    {
                        m_triangleMesh = new Depthkit.TriangleMesh();
                        m_triangleMesh.source = m_source;
                    }
                }
                return m_triangleMesh;
            }
        }
        #endregion

        #region IPropertyTransfer
        public virtual void SetProperties(ref ComputeShader compute, int kernel)
        {
            compute.SetBuffer(kernel, TriangleDataShaderIds._TriangleBuffer, triangleBuffer);
            compute.SetBuffer(kernel, TriangleDataShaderIds._TrianglesCount, trianglesCount);
        }

        public virtual void SetProperties(ref Material material)
        {
            material.SetBuffer(TriangleDataShaderIds._TriangleBuffer, triangleBuffer);
            material.SetBuffer(TriangleDataShaderIds._TrianglesCount, trianglesCount);
        }

        public virtual void SetProperties(ref Material material, ref MaterialPropertyBlock block)
        {
            block.SetBuffer(TriangleDataShaderIds._TriangleBuffer, triangleBuffer);
            block.SetBuffer(TriangleDataShaderIds._TrianglesCount, trianglesCount);
        }
        #endregion
    }

    [System.Serializable]
    public class SubMesh<TriangleType> : SubMesh where TriangleType : struct
    {
        public override int GetDataTypeSizeInBytes() { return Marshal.SizeOf(typeof(TriangleType)); }
    }
}