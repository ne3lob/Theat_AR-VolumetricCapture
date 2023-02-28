using UnityEngine;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using System.Linq;
using System.Collections.Generic;
using System;

namespace Depthkit
{
    public class StudioMeshSourceGizmosDrawer
    {
        [DrawGizmo(GizmoType.Selected | GizmoType.Active)]
        static void DrawGizmosFor(Depthkit.StudioMeshSource meshSource, GizmoType gizmoType)
        {
            if(meshSource.showLevelOfDetailGizmo)
            {
                Camera cam = Camera.main;
                Vector3 origin = meshSource.transform.position + meshSource.volumeBounds.center;
                Vector3 dir = (cam.transform.position - origin).normalized;
                float segmentLength = (cam.farClipPlane / meshSource.levelOfDetailDistance) / meshSource.numLevelOfDetailLevels;
                for(int i = 0; i < meshSource.numLevelOfDetailLevels; ++i)
                {
                    Color c = Color.HSVToRGB(1.0f / (float)i, 1.0f, 1.0f);
                    Vector3 start = dir * segmentLength * i + origin;
                    Vector3 end = start + dir * segmentLength;
                    Gizmos.color = c;
                    Gizmos.DrawLine(start, end);
                    Matrix4x4 storedMatrix = Gizmos.matrix;
                    Gizmos.matrix = Matrix4x4.Translate(end - cam.transform.position);
                    Gizmos.matrix *= Matrix4x4.LookAt(cam.transform.position, origin, cam.transform.up);
                    Gizmos.DrawWireCube(Vector3.zero, new Vector3(1, 1, 0.001f));
                    Gizmos.matrix = storedMatrix;
                }
            }
        }
    }
}