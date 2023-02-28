using UnityEngine;
using UnityEngine.Rendering;

namespace Depthkit
{
    public abstract class ProceduralLook : Look
    {
        public ShadowCastingMode shadowCastingMode = ShadowCastingMode.On;
        public bool receiveShadows = true;
        public bool interpolateLightProbes = true;
        public Transform anchorOverride = null;
        public Material lookMaterial = null;

        protected override bool UsesMaterial() { return true; }
        protected override Material GetMaterial() { return lookMaterial; }

        protected override bool UsesMaterialPropertyBlock() { return true; }

        protected override void SetMaterialProperties(ref Material material, ref MaterialPropertyBlock block)
        {
            if (interpolateLightProbes)
            {
                Vector3[] positions = new Vector3[1] { anchorOverride != null ? anchorOverride.position : transform.position };
                SphericalHarmonicsL2[] harmonics = new SphericalHarmonicsL2[1];
                Vector4[] occlusions = new Vector4[1];

                LightProbes.CalculateInterpolatedLightAndOcclusionProbes(positions, harmonics, occlusions);

                block.CopyProbeOcclusionArrayFrom(occlusions, 0, 0, 1);
                block.CopySHCoefficientArraysFrom(harmonics, 0, 0, 1);
            }
        }

        protected override void OnUpdate()
        {
            base.OnUpdate();
            if (meshSource.triangleBufferDrawIndirectArgs == null ||
                !meshSource.triangleBufferDrawIndirectArgs.IsValid() ||
                meshSource.triangleBuffer == null ||
                !meshSource.triangleBuffer.IsValid())
            {
                return;
            }

            Graphics.DrawProceduralIndirect(
                GetMaterial(),
                meshSource.GetWorldBounds(),
                MeshTopology.Triangles,
                meshSource.triangleBufferDrawIndirectArgs, 0,
                null,
                GetMaterialPropertyBlock(),
                shadowCastingMode, //cast shadows
                receiveShadows, //receive shadows
                gameObject.layer);
        }
    }
}