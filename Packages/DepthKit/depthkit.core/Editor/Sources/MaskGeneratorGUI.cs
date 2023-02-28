using UnityEditor;
using UnityEngine;

namespace Depthkit
{
    public class MaskGeneratorGUI
    {
        RenderTexture[] m_arrayTextures = null;
        RenderTexture[] m_arrayDownscaledTextures = null;
        bool m_filled = false;
        float m_scale = 0.5f;
        private static MeshDensity s_meshDensity;

        private bool m_showTextures = false;

        public static readonly GUIContent s_maskScaleLabel = new GUIContent("Down Scale", "Adjust the scale of the mask. Higher values capture more detail and have cleaner edges at the cost of performance.");
        public static readonly GUIContent s_edgeMaskBlurRadiusLabel = new GUIContent("Radius", "Control the width of the edge smoothing mask's edge feathering.");
        public static readonly GUIContent s_edgeMaskSobelStrengthLabel = new GUIContent("Strength", "Control the falloff of the edge smoothing mask's edge feathering.  The higher the strength, the steeper the falloff curve.");
        public static readonly GUIContent s_previewMaskOnGeoLabel = new GUIContent("Debug Edge Mask", "Debug view of the mask generated per perspective for precise edge masking adjustments.");

        public static bool ValidateMeshDensity(System.Enum md)
        {
            return s_meshDensity >= (MeshDensity)md;
        }

        public void MaskGui(ref MaskGenerator maskGenerator, MeshDensity meshDensity, ref bool doGenerate, bool drawBox=false)
        {
            if (drawBox)
            {
                EditorGUILayout.BeginVertical("Box");
            }
            else
            {
                EditorGUILayout.Space();
                EditorUtil.DrawUILine(Color.gray, 1);
            }

            EditorGUI.BeginChangeCheck();

            bool previewMaskOnGeo = maskGenerator.enableMaskDebug;
            previewMaskOnGeo = EditorGUILayout.Toggle(MaskGeneratorGUI.s_previewMaskOnGeoLabel, previewMaskOnGeo);
            if (previewMaskOnGeo != maskGenerator.enableMaskDebug)
            {
                maskGenerator.enableMaskDebug = previewMaskOnGeo;
            }

            s_meshDensity = meshDensity;
            MeshDensity d = (MeshDensity)EditorGUILayout.EnumPopup(s_maskScaleLabel, (MeshDensity)maskGenerator.scale, ValidateMeshDensity, false);
            if (maskGenerator.scale != (int)d)
            {
                maskGenerator.scale = (int)d;
            }

            maskGenerator.invalidateEdgeWidth = (float)EditorGUILayout.IntSlider("Invalid Edge Width", (int)maskGenerator.invalidateEdgeWidth, 0, 10);
            maskGenerator.invalidateStrength = EditorGUILayout.Slider("Invalid Strength", maskGenerator.invalidateStrength, 1.0f, 10.0f);

            EditorGUILayout.BeginVertical("Box");
            EditorGUILayout.LabelField("Sobel Filter", EditorStyles.boldLabel);

            maskGenerator.sobelMultiplier = EditorGUILayout.Slider("Multiplier", maskGenerator.sobelMultiplier, 0.0f, 25.0f);
            EditorGUILayout.EndVertical();

            float edgeSmoothRadius = maskGenerator.blurRadius;

            EditorGUILayout.BeginVertical("Box");
            EditorGUILayout.LabelField("Blur Filter", EditorStyles.boldLabel);
            bool enableBlur = maskGenerator.enableBlur;
            enableBlur = EditorGUILayout.Toggle("Enable", enableBlur);
            if (enableBlur != maskGenerator.enableBlur)
            {
                maskGenerator.enableBlur = enableBlur;
            }
            GUI.enabled = enableBlur;
            maskGenerator.blurRadius = EditorGUILayout.Slider(MaskGeneratorGUI.s_edgeMaskBlurRadiusLabel, edgeSmoothRadius, 0.1f, 16.0f);
            GUI.enabled = true;

            EditorGUILayout.EndVertical();
            if (EditorGUI.EndChangeCheck())
            {
                doGenerate = true;
            }

            PreviewGUI(ref m_showTextures, ref maskGenerator, false);

            if (drawBox)
            {
                EditorGUILayout.EndVertical();
            }
            else
            {
                EditorUtil.DrawUILine(Color.gray, 1);
                EditorGUILayout.Space();
            }
        }

        public void MarkDirty()
        {
            m_filled = false;
        }

        void ReleaseTexture(ref RenderTexture[] textures)
        {
            if (textures != null)
            {
                foreach (var tex in textures)
                {
                    tex.Release();
                    Object.DestroyImmediate(tex);
                }
            }
            textures = null;
        }

        public void Release()
        {
            ReleaseTexture(ref m_arrayTextures);
            ReleaseTexture(ref m_arrayDownscaledTextures);
        }

        public void PreviewGUI(ref bool show, ref MaskGenerator maskGenerator, bool showDownscaled)
        {
            show = EditorGUILayout.Foldout(show, "Edge Mask Preview");
            if (show)
            {
                if (maskGenerator.maskTexture != null)
                {
                    m_scale = EditorGUILayout.Slider("Preview Scale", m_scale, 0.0001f, 1.0f);
                    GUI.enabled = false;
                    EditorGUILayout.Vector2Field("Mask Dimensions", new Vector2(maskGenerator.maskTexture.width, maskGenerator.maskTexture.height));
                    GUI.enabled = true;
                    switch (maskGenerator.maskTexture.dimension)
                    {
                        case UnityEngine.Rendering.TextureDimension.Tex2D:
                            GUILayout.Label(maskGenerator.maskTexture, GUILayout.Height(maskGenerator.maskTexture.height * m_scale));
                            break;
                        case UnityEngine.Rendering.TextureDimension.Tex2DArray:
                            {
                                if (!m_filled)
                                {
                                    // Mask
                                    if (m_arrayTextures == null || m_arrayTextures.Length != maskGenerator.maskTexture.volumeDepth)
                                    {
                                        if (m_arrayTextures != null)
                                        {
                                            foreach (var tex in m_arrayTextures)
                                            {
                                                tex.Release();
                                                Object.DestroyImmediate(tex);
                                            }
                                        }
                                        m_arrayTextures = new RenderTexture[maskGenerator.maskTexture.volumeDepth];
                                    }
                                    for (int i = 0; i < maskGenerator.maskTexture.volumeDepth; ++i)
                                    {
                                        RenderTexture tempTexture = new RenderTexture(maskGenerator.maskTexture.width, maskGenerator.maskTexture.height, 0, maskGenerator.maskTexture.format, 0);
                                        tempTexture.Create();
                                        Graphics.CopyTexture(maskGenerator.maskTexture, i, tempTexture, 0);
                                        m_arrayTextures[i] = tempTexture;
                                    }
                                    if (showDownscaled)
                                    {
                                        // Downscaled Mask
                                        if (m_arrayDownscaledTextures == null || m_arrayDownscaledTextures.Length != maskGenerator.downScaledMaskTexture.volumeDepth)
                                        {
                                            if (m_arrayDownscaledTextures != null)
                                            {
                                                foreach (var tex in m_arrayDownscaledTextures)
                                                {
                                                    tex.Release();
                                                    Object.DestroyImmediate(tex);
                                                }
                                            }
                                            m_arrayDownscaledTextures = new RenderTexture[maskGenerator.downScaledMaskTexture.volumeDepth];
                                        }
                                        for (int i = 0; i < maskGenerator.downScaledMaskTexture.volumeDepth; ++i)
                                        {
                                            RenderTexture tempTexture = new RenderTexture(maskGenerator.downScaledMaskTexture.width, maskGenerator.downScaledMaskTexture.height, 0, maskGenerator.downScaledMaskTexture.format, 0);
                                            tempTexture.Create();
                                            Graphics.CopyTexture(maskGenerator.downScaledMaskTexture, i, tempTexture, 0);
                                            m_arrayDownscaledTextures[i] = tempTexture;
                                        }
                                    }

                                    m_filled = true;
                                }
                                for (int i = 0; i < m_arrayTextures.Length; ++i)
                                {
                                    GUILayout.Label(m_arrayTextures[i], GUILayout.Height(maskGenerator.maskTexture.height * m_scale));
                                    if (showDownscaled) GUILayout.Label(m_arrayDownscaledTextures[i], GUILayout.Height(maskGenerator.downScaledMaskTexture.height * m_scale));
                                }
                            }
                            break;
                        default: break;
                    }
                }
            }
            else
            {
                m_filled = false;
            }
        }
    }
}