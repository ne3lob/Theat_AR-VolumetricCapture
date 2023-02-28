using System.Collections;
using UnityEngine;

namespace Depthkit
{
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public abstract class ShaderGraphLook : Look
    {
        protected MeshRenderer m_meshRenderer = null;
        protected MeshFilter m_meshFilter = null;

        protected abstract Material GetDefaultShaderGraph();

        protected override void SetDefaults()
        {
            m_meshFilter = GetComponent<MeshFilter>();
            m_meshFilter.hideFlags = HideFlags.NotEditable;
            m_meshRenderer = GetComponent<MeshRenderer>();

            if (m_meshRenderer.sharedMaterial == null)
            {
                m_meshRenderer.material = GetDefaultShaderGraph();
            }

            m_meshRenderer.SetPropertyBlock(materialPropertyBlock);

            base.SetDefaults();
        }

        protected override bool UsesMaterial() { return true; }
        protected override Material GetMaterial()
        {
            return m_meshRenderer.sharedMaterial;
        }

        protected override void OnUpdate()
        {
            if (!meshSource.useTriangleMesh) meshSource.useTriangleMesh = true;
            if(meshSource.triangleMesh != null && meshSource.triangleMesh.mesh != m_meshFilter.sharedMesh)
                m_meshFilter.sharedMesh = meshSource.triangleMesh.mesh;
            m_meshRenderer.SetPropertyBlock(GetMaterialPropertyBlock());
            base.OnUpdate();
        }

        private void OnDestroy()
        {
            if (m_meshFilter != null)
            {
                m_meshFilter.hideFlags = HideFlags.None;
            }
        }
    }
}