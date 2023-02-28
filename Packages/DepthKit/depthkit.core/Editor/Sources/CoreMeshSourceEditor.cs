using UnityEngine;
using UnityEditor;

namespace Depthkit
{
    [CustomEditor(typeof(Depthkit.CoreMeshSource))]
    public class CoreMeshSourceEditor : Editor
    {
        private bool m_showAdvanced = false;
        private bool m_showRelighting = false;
        private static MeshDensity s_maskScale;

        SerializedProperty adjustableNormalSlope;
        SerializedProperty normalGenerationTechnique;
        SerializedProperty pauseDataGenerationWhenInvisible;
        SerializedProperty pausePlayerWhenInvisible;
        SerializedProperty radialBias;

        public static readonly GUIContent s_meshDensityLabel = new GUIContent("Mesh Density", "Adjust the density of the Depthkit mesh. Higher values capture more detail and have cleaner edges at the cost of performance.");
        public static readonly GUIContent s_maxSurfaceTrianglesLabel = new GUIContent("Surface Buffer Capacity", "This value determines the maximum number of triangles that can be displayed for each frame of this Clip. Each frame will have a variable size mesh based on the shape of its contents. A higher number ensures that no part of the surface is every excluded and allocates a larger memory footprint.");
        public static readonly GUIContent s_normalGenTechniqueLabel = new GUIContent("Normal Generation Technique", "This is the algorithm used to generate normals. Flat is the fastest.");
        public static readonly GUIContent s_normalSlopeLabel = new GUIContent("Normal Slope", "Adjust the slope of the normal to artificially flatten or sharpen surface normals.");
        public static readonly GUIContent s_setSurfaceBufferCapLabel = new GUIContent("Set Surface Buffer Capacity", "Use this button to sample the current frame to set a reasonable Surface Buffer Capacity tailored to your content.");
        public static readonly GUIContent s_setSurfaceBufferCapSliderLabel = new GUIContent("Surface Buffer Capacity (% of max)", "Use this slider to adjust Surface Buffer Capacity as a percentage of the maximum triangles needed for your content.");
        public static readonly GUIContent s_radialBiasLabel = new GUIContent("Depth Bias Compensation", "Time of Flight cameras measure surfaces farther away than they are in reality. The amount of bias depends greatly on the material of the surface being measured. Skin in particular has a large bias. The Depth Bias Compensation is a correction for this error by pulling the surface back towards their true depth. It most useful for recovering high quality faces and hands on otherwise well-calibrated captures. The larger the value, the larger the compensation. 0 means no depth bias compensation is applied.");

        public static readonly GUIContent s_ditherEdgeLabel = new GUIContent("Dither Edge", "");

        MaskGeneratorGUI m_maskGeneratorGui;

        void OnEnable()
        {
            pauseDataGenerationWhenInvisible = serializedObject.FindProperty("pauseDataGenerationWhenInvisible");
            pausePlayerWhenInvisible = serializedObject.FindProperty("pausePlayerWhenInvisible");
            radialBias = serializedObject.FindProperty("radialBias");
            normalGenerationTechnique = serializedObject.FindProperty("normalGenerationTechnique");
            adjustableNormalSlope = serializedObject.FindProperty("adjustableNormalSlope");

            if(m_maskGeneratorGui == null)
            {
                m_maskGeneratorGui = new MaskGeneratorGUI();
            }
        }

        private void OnDisable()
        {
            m_maskGeneratorGui?.Release();
        }

        public static bool Check(CoreMeshSource source)
        {
            if (GUI.changed)
            {
                EditorUtility.SetDirty(source);
                GUI.changed = false;
                return true;
            }
            return false;
        }

        public static bool Check(CoreMeshSource source, ref bool resize)
        {
            if (Check(source))
            {
                resize = true;
                return true;
            }
            return false;
        }

        public static bool Check(CoreMeshSource source, ref bool resize, ref bool generate)
        {
            if (Check(source, ref resize))
            {
                generate = true;
                return true;
            }
            return false;
        }

        public static bool ValidateMeshDensity(System.Enum md)
        {
            return s_maskScale <= (MeshDensity)md;
        }

        public static void MeshSettingsGUI(ref CoreMeshSource meshSource, ref bool doResize, ref bool doGenerate)
        {
            // save mask scale to static so it can be used in the validator function wrt downscale
            s_maskScale = (MeshDensity)meshSource.maskGenerator.scale;
            meshSource.meshDensity = (MeshDensity)EditorGUILayout.EnumPopup(s_meshDensityLabel, meshSource.meshDensity, ValidateMeshDensity, false);
            Check(meshSource, ref doResize, ref doGenerate);
        }

        public static void ResizeTriangleMeshGUI(ref CoreMeshSource meshSource, uint submeshIndex, ref bool doResize, ref bool doGenerate)
        {
            if (meshSource.useTriangleMesh)
            {
                if (GUILayout.Button(s_setSurfaceBufferCapLabel))
                {
                    meshSource.recalculateCurrentSurfaceTriangleCount = true;
                }

                GUI.enabled = !meshSource.recalculateCurrentSurfaceTriangleCount;
                uint originalIndex = meshSource.currentSubmeshIndex;
                meshSource.currentSubmeshIndex = submeshIndex;
                float percent = (float)EditorGUILayout.Slider(s_setSurfaceBufferCapSliderLabel, meshSource.surfaceTriangleCountPercent*100, 0.0f, 100.0f)/100.0f;
                if( percent != meshSource.surfaceTriangleCountPercent)
                {
                    meshSource.maxSurfaceTriangles = (uint)( percent * (float)meshSource.latticeMaxTriangles);
                    meshSource.surfaceTriangleCountPercent = percent;
                }
                GUI.enabled = false;
                EditorGUILayout.IntField(s_maxSurfaceTrianglesLabel, (int)meshSource.maxSurfaceTriangles);
                GUI.enabled = true;
                Check(meshSource, ref doResize, ref doGenerate);
                meshSource.currentSubmeshIndex = originalIndex;
            }
        }

        public static void RelightingGUI(ref bool show, ref SerializedProperty technique, ref SerializedProperty slope, ref bool doResize, ref bool doGenerate)
        {
            show = EditorGUILayout.Foldout(show, "Normal Generation Settings");
            if (show)
            {
                EditorGUILayout.PropertyField(technique, s_normalGenTechniqueLabel);
                if (GUI.changed)
                {
                    doResize = true;
                    doGenerate = true;
                    GUI.changed = false;
                }

                NormalGenerationTechnique normalTechnique = (NormalGenerationTechnique)technique.enumValueIndex;

                if (normalTechnique == NormalGenerationTechnique.Adjustable ||
                    normalTechnique == NormalGenerationTechnique.AdjustableSmoother)
                {
                    EditorGUILayout.PropertyField(slope, s_normalSlopeLabel);
                    if (GUI.changed)
                    {
                        doGenerate = true;
                        GUI.changed = false;
                    }
                }
            }
        }

        public override void OnInspectorGUI()
        {
            Depthkit.CoreMeshSource meshSource = target as Depthkit.CoreMeshSource;
            bool doGenerate = false;
            bool doResize = false;

            serializedObject.Update();
            MeshSettingsGUI(ref meshSource, ref doResize, ref doGenerate);
            ResizeTriangleMeshGUI(ref meshSource, 0, ref doResize, ref doGenerate);
            RelightingGUI(ref m_showRelighting, ref normalGenerationTechnique, ref adjustableNormalSlope, ref doResize, ref doGenerate);

            EditorGUI.BeginChangeCheck();

            meshSource.clipThreshold = EditorGUILayout.Slider("Clipping Threshold", meshSource.clipThreshold, 0.0f, 1.0f);

            bool ditherEdge = meshSource.ditherEdge;
            ditherEdge = EditorGUILayout.Toggle(s_ditherEdgeLabel, ditherEdge);

            if (ditherEdge != meshSource.ditherEdge)
            {
                meshSource.ditherEdge = ditherEdge;
            }
            if (meshSource.ditherEdge)
            {
                meshSource.ditherWidth = EditorGUILayout.Slider("Dithering Width", meshSource.ditherWidth, 0.0f, 0.2f);
            }
            if (EditorGUI.EndChangeCheck())
            {
                doGenerate = true;
            }

            m_maskGeneratorGui.MaskGui(ref meshSource.maskGenerator, meshSource.meshDensity, ref doGenerate);

            m_showAdvanced = EditorGUILayout.Foldout(m_showAdvanced, "Advanced Settings");
            if (m_showAdvanced)
            {
                EditorGUI.BeginChangeCheck();

                EditorGUILayout.PropertyField(radialBias, s_radialBiasLabel);
                meshSource.edgeCompressionNoiseThreshold = EditorGUILayout.Slider("Edge Compression Noise Threshold", meshSource.edgeCompressionNoiseThreshold, 0.0f, 1.0f);
                EditorGUILayout.PropertyField(pausePlayerWhenInvisible);
                EditorGUILayout.PropertyField(pauseDataGenerationWhenInvisible);

                if (EditorGUI.EndChangeCheck())
                {
                    doGenerate = true;
                }
            }

            serializedObject.ApplyModifiedProperties();

            if (doResize)
            {
                meshSource.Resize();
            }
            if (doGenerate)
            {
                m_maskGeneratorGui.MarkDirty();
                meshSource.Generate();
            }
        }
    }
}