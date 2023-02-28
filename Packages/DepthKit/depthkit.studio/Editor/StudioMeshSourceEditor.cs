using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;

namespace Depthkit
{
    public class StudioMeshSourceGizmoDrawer
    {
        [DrawGizmo(GizmoType.Active)]
        static void DrawGizmosFor(StudioMeshSource meshSource, GizmoType gizmoType)
        {
            for (int persp = 0; persp < meshSource.perspectivesCount; ++persp)
            {
                if (meshSource.showPerspectiveGizmo[persp] && meshSource.clip.metadata.perspectives.Length == meshSource.showPerspectiveGizmo.Length)
                {
                    var perspective = meshSource.clip.metadata.perspectives[persp];
                    Color camColor = Util.ColorForCamera(persp);
                    Util.RenderPerspectiveGizmo(perspective, meshSource.transform, camColor, "Perspective " + persp);
                }
            }
        }
    }

    [CustomEditor(typeof(StudioMeshSource))]
    public class StudioMeshSourceEditor : Editor
    {
        BoxBoundsHandle m_sdfBoundsControl = new BoxBoundsHandle();
        SerializedProperty showVolumePreview;
        SerializedProperty automaticLevelOfDetail;
        SerializedProperty levelOfDetailDistance;
        SerializedProperty showLevelOfDetailGizmo;
        SerializedProperty recalculateCurrentSurfaceTriangleCount;
        SerializedProperty volumeDensity;
        SerializedProperty surfaceSensitivity;
        SerializedProperty surfaceSmoothingRadius;
        SerializedProperty enableSurfaceSmoothing;
        SerializedProperty volumePreviewAlpha;
        SerializedProperty volumePreviewPointSize;

        SerializedProperty globalViewDependentColorBlendWeight;
        SerializedProperty globalViewDependentGeometryBlendWeight;

        SerializedProperty surfaceNormalColorBlendingPower;
        SerializedProperty perViewDisparityThreshold;
        SerializedProperty perViewDisparityBlendWidth;
        SerializedProperty disparityMin;
        SerializedProperty untexturedFragmentSetting;
        SerializedProperty generationMethod;
        SerializedProperty normalWeightResolutionReduction;

        SerializedProperty enableViewDependentGeometry;

        SerializedProperty volumeViewpoint;

        SerializedProperty weightUnknown;
        SerializedProperty weightUnseenMax;
        SerializedProperty weightUnseenMin;
        SerializedProperty weightUnseenFalloffPower;
        SerializedProperty weightInFrontMax;
        SerializedProperty weightInFrontMin;

        SerializedProperty m_bIsSetup;

        SerializedProperty m_totalVoxelCount;
        SerializedProperty m_voxelGridDimensions;

        SerializedProperty radialBias;
        SerializedProperty pauseDataGenerationWhenInvisible;
        SerializedProperty pausePlayerWhenInvisible;

        MaskGeneratorGUI m_maskGUI;

        bool m_showExperimentalVolumeSettings = false;
        bool m_showViewDependentControls = false;
        bool m_showAdvancedPerPerspectiveSettings = false;
        bool m_showAdvancedSurfaceSettings = false;

        bool[] m_showViewDependentControlsPerPerspective = null;

        void OnEnable()
        {
            showVolumePreview = serializedObject.FindProperty("showVolumePreview");
            automaticLevelOfDetail = serializedObject.FindProperty("automaticLevelOfDetail");
            levelOfDetailDistance = serializedObject.FindProperty("levelOfDetailDistance");
            recalculateCurrentSurfaceTriangleCount = serializedObject.FindProperty("recalculateCurrentSurfaceTriangleCount");
            volumeDensity = serializedObject.FindProperty("m_volumeDensity");
            surfaceSensitivity = serializedObject.FindProperty("surfaceSensitivity");
            surfaceSmoothingRadius = serializedObject.FindProperty("surfaceSmoothingRadius");
            volumePreviewAlpha = serializedObject.FindProperty("volumePreviewAlpha");
            volumePreviewPointSize = serializedObject.FindProperty("volumePreviewPointSize");
            enableSurfaceSmoothing = serializedObject.FindProperty("enableSurfaceSmoothing");
            m_bIsSetup = serializedObject.FindProperty("m_bIsSetup");
            showLevelOfDetailGizmo = serializedObject.FindProperty("showLevelOfDetailGizmo");
            surfaceNormalColorBlendingPower = serializedObject.FindProperty("surfaceNormalColorBlendingPower");
            weightUnknown = serializedObject.FindProperty("weightUnknown");
            weightUnseenMax = serializedObject.FindProperty("weightUnseenMax");
            weightUnseenMin = serializedObject.FindProperty("weightUnseenMin");
            weightUnseenFalloffPower = serializedObject.FindProperty("weightUnseenFalloffPower");
            weightInFrontMax = serializedObject.FindProperty("weightInFrontMax");
            weightInFrontMin = serializedObject.FindProperty("weightInFrontMin");
            perViewDisparityThreshold = serializedObject.FindProperty("perViewDisparityThreshold");
            globalViewDependentColorBlendWeight = serializedObject.FindProperty("globalViewDependentColorBlendWeight");
            globalViewDependentGeometryBlendWeight = serializedObject.FindProperty("globalViewDependentGeometryBlendWeight");
            volumeViewpoint = serializedObject.FindProperty("volumeViewpoint");
            enableViewDependentGeometry = serializedObject.FindProperty("enableViewDependentGeometry");
            m_totalVoxelCount = serializedObject.FindProperty("m_totalVoxelCount");
            m_voxelGridDimensions = serializedObject.FindProperty("m_voxelGridDimensions");
            perViewDisparityBlendWidth = serializedObject.FindProperty("perViewDisparityBlendWidth");
            disparityMin = serializedObject.FindProperty("disparityMin");
            pauseDataGenerationWhenInvisible = serializedObject.FindProperty("pauseDataGenerationWhenInvisible");
            pausePlayerWhenInvisible = serializedObject.FindProperty("pausePlayerWhenInvisible");
            generationMethod = serializedObject.FindProperty("generationMethod");
            normalWeightResolutionReduction = serializedObject.FindProperty("normalWeightResolutionReduction");
            radialBias = serializedObject.FindProperty("radialBias");
            untexturedFragmentSetting = serializedObject.FindProperty("untexturedFragmentSetting");

            m_bIsSetup.boolValue = false;
            if (m_maskGUI == null)
            {
                m_maskGUI = new MaskGeneratorGUI();
            }
        }

        private void OnDisable()
        {
            m_maskGUI?.Release();
        }

        private void OnSceneGUI()
        {
            StudioMeshSource meshSource = target as Depthkit.StudioMeshSource;
            bool didChange = m_sdfBoundsControl.center != meshSource.volumeBounds.center || m_sdfBoundsControl.size != meshSource.volumeBounds.size;
            m_sdfBoundsControl.center = meshSource.volumeBounds.center;
            m_sdfBoundsControl.size = meshSource.volumeBounds.size;
            // draw the handle
            EditorGUI.BeginChangeCheck();
            Matrix4x4 pushPop = Handles.matrix;
            Handles.matrix = meshSource.transform.localToWorldMatrix;
            m_sdfBoundsControl.DrawHandle();
            Handles.matrix = pushPop;
            if (EditorGUI.EndChangeCheck())
            {
                // record the target object before setting new values so changes can be undone/redone
                Undo.RecordObject(meshSource, "Change Bounds");

                // copy the handle's updated data back to the target object
                Bounds newBounds = new Bounds();
                newBounds.center = m_sdfBoundsControl.center;
                newBounds.size = m_sdfBoundsControl.size;
                meshSource.volumeBounds = newBounds;
                EditorUtility.SetDirty(meshSource);
            }
            else if (didChange)
            {
                EditorUtility.SetDirty(meshSource);
            }
        }

        public override void OnInspectorGUI()
        {
            bool doResize = false;
            bool doGenerate = false;
            float val;
            StudioMeshSource meshSource = target as StudioMeshSource;
            if (meshSource.clip == null) return;

            serializedObject.Update();

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Volume Settings", EditorStyles.boldLabel);

            EditorGUILayout.PropertyField(volumeDensity, new GUIContent("Volume Density", "Adjust the density of the Depthkit volume in voxels per meter. Higher values capture more detail at the cost of performance and memory usage."));

            GUI.enabled = false;
            EditorGUILayout.PropertyField(m_voxelGridDimensions);
            EditorGUILayout.PropertyField(m_totalVoxelCount);
            GUI.enabled = true;
            if (GUILayout.Button("Reset Volume Bounds"))
            {
                meshSource.ResetVolumeBounds();
                EditorUtility.SetDirty(meshSource);
            }
            if (EditorGUI.EndChangeCheck())
            {
                doResize = true;
                doGenerate = true;
            }

            m_showExperimentalVolumeSettings = EditorGUILayout.Foldout(m_showExperimentalVolumeSettings, "Experimental Volume Settings");
            if (m_showExperimentalVolumeSettings)
            {
                EditorGUI.BeginChangeCheck();

                EditorGUILayout.PropertyField(surfaceSensitivity, new GUIContent("Adjust Surface Sensitivity", "Tailor this value to what looks best for each specific clip. Surface Sensitivity controls how likely a Surface is to be determined from a point in the volume based on the various depth perspectives. A higher sensitivity is more likely to generate surfaces, which may recover some geometry and also introduce artifacts."));
                if (GUILayout.Button("Reset Surface Sensitivity"))
                {
                    meshSource.ResetSurfaceSensitivity();
                    EditorUtility.SetDirty(meshSource);
                    doGenerate = true;
                }
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(radialBias, new GUIContent("Depth Bias Compensation", "Time of Flight cameras measure surfaces farther away than they are in reality. The amount of bias depends greatly on the material of the surface being measured. Skin in particular has a large bias. The Depth Bias Compensation is a correction for this error by pulling the surface back towards their true depth. It most useful for recovering high quality faces and hands on otherwise well-calibrated captures. The larger the value, the larger the compensation. 0 means no depth bias compensation is applied."));
                
                EditorGUILayout.Space();
                EditorGUILayout.PropertyField(weightUnknown);
                EditorGUILayout.PropertyField(weightUnseenMax);
                EditorGUILayout.PropertyField(weightUnseenMin);
                EditorGUILayout.PropertyField(weightUnseenFalloffPower);
                EditorGUILayout.PropertyField(weightInFrontMax);
                EditorGUILayout.PropertyField(weightInFrontMin);
                if (GUILayout.Button("Load Front Biased Defaults"))
                {
                    meshSource.LoadFrontBiasedDefaults();
                    EditorUtility.SetDirty(meshSource);
                    doGenerate = true;
                }

                EditorGUILayout.PropertyField(showVolumePreview);
                if (showVolumePreview.boolValue)
                {
                    EditorGUILayout.PropertyField(volumePreviewAlpha);
                    EditorGUILayout.PropertyField(volumePreviewPointSize);
                }

                m_showAdvancedSurfaceSettings = EditorGUILayout.Foldout(m_showAdvancedSurfaceSettings, "Advanced Optimization Settings");
                if (m_showAdvancedSurfaceSettings)
                {
                    EditorGUILayout.PropertyField(normalWeightResolutionReduction);
                    EditorGUILayout.PropertyField(pausePlayerWhenInvisible);
                    EditorGUILayout.PropertyField(pauseDataGenerationWhenInvisible);
                }

                if (EditorGUI.EndChangeCheck())
                {
                    doGenerate = true;
                }
            }

            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.LabelField("Surface Settings", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(enableSurfaceSmoothing, new GUIContent("Enable Surface Smoothing", "Smooth the output surface to reduce artifacts at the cost of some loss of detail."));
            if (enableSurfaceSmoothing.boolValue)
            {
                EditorGUILayout.PropertyField(surfaceSmoothingRadius);
                if (GUILayout.Button("Reset Surface Smoothing"))
                {
                    surfaceSmoothingRadius.floatValue = 0.3f;
                }
            }

            if (GUILayout.Button(new GUIContent("Set Surface Buffer Capacity", "Use this button to sample the current frame to set a reasonable Surface Buffer Capacity tailored to your content.")))
            {
                recalculateCurrentSurfaceTriangleCount.boolValue = true;
            }

            GUI.enabled = !recalculateCurrentSurfaceTriangleCount.boolValue;
            uint curMaxTriangles = meshSource.maxSurfaceTriangles;
            uint newMaxTriangles = (uint)EditorGUILayout.IntField(new GUIContent("Surface Buffer Capacity", "This value determines the maximum number of triangles that can be displayed for each frame of this Clip. Each frame will have a variable size mesh based on the shape of its contents. A higher number ensures that no part of the surface is ever excluded and allocates a larger memory footprint."),  (int)curMaxTriangles);
            if (newMaxTriangles != curMaxTriangles)
            {
                meshSource.maxSurfaceTriangles = newMaxTriangles;
                doResize = true;
            }
            GUI.enabled = true;

            if (EditorGUI.EndChangeCheck())
            {
                doGenerate = true;
            }

            if (meshSource.clip.isSetup == true && meshSource.perspectivesCount == meshSource.clip?.metadata.perspectivesCount)
            {
                m_showViewDependentControls = EditorGUILayout.Foldout(m_showViewDependentControls, "Experimental Texture Settings");
                if (m_showViewDependentControls)
                {
                    EditorGUI.BeginChangeCheck();

                    EditorGUILayout.BeginVertical("Box");
                    EditorGUILayout.LabelField("Color Blending Settings");
                    EditorGUILayout.PropertyField(globalViewDependentColorBlendWeight, new GUIContent("View Dependent Color Blend Weight", "Remove texture bleeding artifacts by adjusting the maximum contribution of each view’s color texture in the output. Default blend weight is 1."));
                    EditorGUILayout.PropertyField(surfaceNormalColorBlendingPower, new GUIContent("Surface Normal Color Blend Weight", "Adjust the surface normal contribution of color blending. Default blend weight is 1."));
                    EditorGUILayout.PropertyField(perViewDisparityThreshold, new GUIContent("Surface Disparity Color Threshold", "Adjust the threshold that determines the color for one perspective is occluding another perspective. Default blend weight is 0.025."));
                    EditorGUILayout.PropertyField(perViewDisparityBlendWidth, new GUIContent("Disparity Blend Width","The width of the blend between disparate surfaces."));
                    EditorGUILayout.PropertyField(untexturedFragmentSetting, new GUIContent("Untextured Geometry Settings"));
                    if ((StudioMeshSource.UntexturedGeometrySettings)untexturedFragmentSetting.intValue == StudioMeshSource.UntexturedGeometrySettings.Colorize)
                    {
                        meshSource.untexturedColor = EditorGUILayout.ColorField(new GUIContent("Untextured Fragment Color"), meshSource.untexturedColor, true, false, false);
                    }

                    if (EditorGUI.EndChangeCheck())
                    {
                        doGenerate = true;
                    }

                    bool enableEdgeMask = meshSource.enableEdgeMask;
                    enableEdgeMask = EditorGUILayout.Toggle("Enable Edge Mask", enableEdgeMask);
                    if (enableEdgeMask != meshSource.enableEdgeMask)
                    {
                        meshSource.enableEdgeMask = enableEdgeMask;
                        doGenerate = true;
                        doResize = true;
                    }
                    if (enableEdgeMask)
                    {
                        m_maskGUI.MaskGui(ref meshSource.maskGenerator, MeshDensity.Low, ref doGenerate, true);
                    }

                    EditorGUILayout.EndVertical();

                    if (m_showViewDependentControlsPerPerspective == null || m_showViewDependentControlsPerPerspective.Length != meshSource.clip.metadata.perspectivesCount)
                    {
                        m_showViewDependentControlsPerPerspective = new bool[meshSource.clip.metadata.perspectivesCount];
                        for (int p = 0; p < m_showViewDependentControlsPerPerspective.Length; ++p)
                        {
                            m_showViewDependentControlsPerPerspective[p] = false;
                        }
                    }

                    m_showAdvancedPerPerspectiveSettings = EditorGUILayout.Foldout(m_showAdvancedPerPerspectiveSettings, "Show Advanced Per Perspective Settings (Experimental)");
                    if (m_showAdvancedPerPerspectiveSettings)
                    {
                        EditorGUI.BeginChangeCheck();
                        for (int persp = 0; persp < meshSource.perspectivesCount; ++persp)
                        {
                            m_showViewDependentControlsPerPerspective[persp] = EditorGUILayout.Foldout(m_showViewDependentControlsPerPerspective[persp], "Perspective " + persp + " Settings");
                            if (m_showViewDependentControlsPerPerspective[persp])
                            {
                                EditorGUILayout.BeginVertical("Box");

                                EditorGUILayout.LabelField("Color Settings");

                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField("Perspective Color Enabled", GUILayout.Width(250));
                                bool perspColorEnabled = meshSource.perspectiveColorBlendingData.GetPerspectiveEnabled(persp);
                                perspColorEnabled = EditorGUILayout.Toggle(perspColorEnabled);
                                meshSource.perspectiveColorBlendingData.SetPerspectiveEnabled(persp, perspColorEnabled);
                                EditorGUILayout.EndHorizontal();

                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField("Enable Perspective Geometry", GUILayout.Width(250));
                                bool perspGeomEnabled = meshSource.perspectiveGeometryData.EnableGeometry(persp);
                                perspGeomEnabled = EditorGUILayout.Toggle(perspGeomEnabled);
                                meshSource.perspectiveGeometryData.EnableGeometry(persp, perspGeomEnabled);
                                EditorGUILayout.EndHorizontal();

                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField("Show Camera Frustum", GUILayout.Width(250));
                                bool show = meshSource.showPerspectiveGizmo[persp];
                                show = EditorGUILayout.Toggle(show);
                                meshSource.showPerspectiveGizmo[persp] = show;
                                EditorGUILayout.EndHorizontal();

                                EditorGUILayout.LabelField("Color Weight Contribution");
                                val = meshSource.perspectiveColorBlendingData.GetViewDependentColorBlendContribution(persp);
                                val = EditorGUILayout.Slider(val, 0.0f, 1.0f);
                                meshSource.perspectiveColorBlendingData.SetViewDependentColorBlendContribution(persp, val);

                                GUI.enabled = perspGeomEnabled;

                                EditorGUILayout.LabelField("Geometry Settings");

                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField("Override Radial Bias", GUILayout.Width(250));
                                bool overrideRadialBias = meshSource.overrideRadialBias[persp];
                                overrideRadialBias = EditorGUILayout.Toggle(overrideRadialBias);
                                if (overrideRadialBias != meshSource.overrideRadialBias[persp])
                                {
                                    meshSource.overrideRadialBias[persp] = overrideRadialBias;

                                }
                                EditorGUILayout.EndHorizontal();

                                GUI.enabled = perspGeomEnabled && overrideRadialBias;

                                EditorGUILayout.LabelField(new GUIContent("Depth Bias Compensation", "Time of Flight cameras measure surfaces farther away than they are in reality. The amount of bias depends greatly on the material of the surface being measured. Skin in particular has a large bias. The Depth Bias Compensation is a correction for this error by pulling the surface back towards their true depth. It most useful for recovering high quality faces and hands on otherwise well-calibrated captures. The larger the value, the larger the compensation. 0 means no depth bias compensation is applied."));
                                val = meshSource.radialBiasPersp[persp];
                                val = EditorGUILayout.Slider(val, StudioMeshSource.radialBiasMin, StudioMeshSource.radialBiasMax);
                                meshSource.radialBiasPersp[persp] = val;

                                GUI.enabled = perspGeomEnabled;

                                EditorGUILayout.BeginHorizontal();
                                EditorGUILayout.LabelField("Override Weight Unknown", GUILayout.Width(250));
                                bool overrideWeight = meshSource.perspectiveGeometryData.GetOverrideWeightUnknown(persp);
                                overrideWeight = EditorGUILayout.Toggle(overrideWeight);
                                meshSource.perspectiveGeometryData.SetOverrideWeightUnknown(persp, overrideWeight);
                                EditorGUILayout.EndHorizontal();

                                GUI.enabled = overrideWeight && perspGeomEnabled;

                                EditorGUILayout.LabelField("Weight Unknown");
                                val = meshSource.perspectiveGeometryData.GetWeightUnknown(persp);
                                val = EditorGUILayout.Slider(val, 0.0001f, 0.05f);
                                meshSource.perspectiveGeometryData.SetWeightUnknown(persp, val);

                                EditorGUILayout.EndVertical();
                            }
                        }

                        if (EditorGUI.EndChangeCheck())
                        {
                            doGenerate = true;
                            EditorUtility.SetDirty(meshSource);
                        }
                    }
                }
            }

            serializedObject.ApplyModifiedProperties();

            if (doResize)
            {
                meshSource.Resize();
            }
            if (doGenerate)
            {
                m_maskGUI.MarkDirty();
                meshSource.Generate();
            }
        }
    }
}