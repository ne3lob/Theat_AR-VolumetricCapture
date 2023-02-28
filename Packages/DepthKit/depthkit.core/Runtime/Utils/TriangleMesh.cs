/************************************************************************************

Depthkit Unity SDK License v1
Copyright 2016-2020 Scatter All Rights reserved.  

Licensed under the Scatter Software Development Kit License Agreement (the "License"); 
you may not use this SDK except in compliance with the License, 
which is provided at the time of installation or download, 
or which otherwise accompanies this software in either electronic or hard copy form.  

You may obtain a copy of the License at http://www.depthkit.tv/license-agreement-v1

Unless required by applicable law or agreed to in writing, 
the SDK distributed under the License is distributed on an "AS IS" BASIS, 
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
See the License for the specific language governing permissions and limitations under the License. 

************************************************************************************/

using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System;
using System.Runtime.InteropServices;

namespace Depthkit
{
    public class TriangleMesh
    {
        [HideInInspector]
        public Depthkit.MeshSource source;

        [System.NonSerialized]
        private Mesh m_mesh;

        [HideInInspector, SerializeField]
        private int m_triangleCount = 0;

        public int TriangleCount
        {
            get { return m_triangleCount; }
            set
            {
                EnsureTriangleMesh(value);
            }
        }
        public Mesh mesh
        {
            get
            {
                if (m_mesh == null)
                {
                    m_mesh = CreateMesh();
                }
                return m_mesh;
            }
        }

        public void EnsureTriangleMesh()
        {
            if (source != null && source.clip != null && source.clip.metadata != null && m_triangleCount > 0)
            {
                if (m_mesh == null)
                {
                    m_mesh = CreateMesh();
                    Bounds bounds = source.GetLocalBounds();
                    m_mesh.bounds = bounds;
                    CreateCubeMesh(m_mesh, bounds.center, bounds.size, m_triangleCount);
                }
            }
        }

        public void EnsureTriangleMesh(Vector2 size)
        {
            if (source != null && source.clip != null && source.clip.metadata != null && size.x > 0 && size.y > 0)
            {
                if (m_mesh == null)
                {
                    m_mesh = CreateMesh();
                }
                int count = ((int)size.x - 1) * ((int)size.y - 1) * 2;
                Bounds bounds = source.GetLocalBounds();
                m_mesh.bounds = bounds;
                if (m_mesh.vertexCount / 3 == count && m_mesh.name == "Depthkit Mesh") return; //no need to update
                m_triangleCount = count;
                CreateCubeMesh(m_mesh, bounds.center, bounds.size, m_triangleCount);
            }
        }

        public void EnsureTriangleMesh(int triangles)
        {
            if (source != null && source.clip != null && source.clip.metadata != null && triangles > 0)
            {
                if (m_mesh == null)
                {
                    m_mesh = CreateMesh();
                }
                Bounds bounds = source.GetLocalBounds();
                m_mesh.bounds = bounds;
                if (m_mesh.vertexCount / 3 == triangles && m_mesh.name == "Depthkit Mesh") return; //no need to update
                m_triangleCount = triangles;
                CreateCubeMesh(m_mesh, bounds.center, bounds.size, m_triangleCount);
            }
        }

        static private Mesh CreateMesh()
        {
            Mesh mesh = new Mesh();
            mesh.name = "Depthkit Mesh";
            mesh.hideFlags = HideFlags.DontSave;
            return mesh;
        }

        public void ReleaseMesh()
        {
            if (m_mesh != null)
            {
                if (Application.isEditor && !Application.isPlaying)
                {
                    UnityEngine.Object.DestroyImmediate(m_mesh);
                }
                else
                {
                    UnityEngine.Object.Destroy(m_mesh);
                }
                m_mesh = null;
            }
        }

        static Vector3[] s_cubeVerts = null;
        static int[] s_cubeTriangles = null;

        static Vector3[] GetCubeVerts()
        {
            if (s_cubeVerts == null)
            {
                s_cubeVerts = new Vector3[] {
                    new Vector3 (-0.5f, -0.5f, -0.5f),
                    new Vector3 (0.5f, -0.5f, -0.5f),
                    new Vector3 (0.5f, 0.5f, -0.5f),
                    new Vector3 (-0.5f, 0.5f, -0.5f),
                    new Vector3 (-0.5f, 0.5f, 0.5f),
                    new Vector3 (0.5f, 0.5f, 0.5f),
                    new Vector3 (0.5f, -0.5f, 0.5f),
                    new Vector3 (-0.5f, -0.5f, 0.5f),
                };
            }
            return s_cubeVerts;
        }

        static int[] GetCubeTriangles()
        {
            if (s_cubeTriangles == null)
            {
                s_cubeTriangles = new int[] {
                    0, 2, 1, //face front
                    0, 3, 2,
                    2, 3, 4, //face top
                    2, 4, 5,
                    1, 2, 5, //face right
                    1, 5, 6,
                    0, 7, 4, //face left
                    0, 4, 3,
                    5, 4, 7, //face back
                    5, 7, 6,
                    0, 6, 7, //face bottom
                    0, 1, 6
                };
            }
            return s_cubeTriangles;
        }

        public void ResetMeshCube(Vector3 center, Vector3 size)
        {
            if (size == Vector3.zero) return;

            Vector3[] positions = mesh.vertices;

            Vector3[] cubeVerts = GetCubeVerts();
            int[] cubeTriangles = GetCubeTriangles();

            for (int i = 0; i < 12; ++i)
            {
                int index = i * 3;
                Vector3 v1 = cubeVerts[cubeTriangles[index]];
                positions[index] = new Vector3(v1.x * size.x + center.x, v1.y * size.y + center.y, v1.z * size.z + center.z);

                ++index;
                Vector3 v2 = cubeVerts[cubeTriangles[index]];
                positions[index] = new Vector3(v2.x * size.x + center.x, v2.y * size.y + center.y, v2.z * size.z + center.z);

                ++index;
                Vector3 v3 = cubeVerts[cubeTriangles[index]];
                positions[index] = new Vector3(v3.x * size.x + center.x, v3.y * size.y + center.y, v3.z * size.z + center.z);
            }

            mesh.vertices = positions;
        }

        //static factory functions
        public static void CreateLattice(Mesh mesh, Vector2 dims, CoordinateRangeType rangeType)
        {

            int w = (int)dims.x;
            int h = (int)dims.y;
            float fw = dims.x;
            float fh = dims.y;
            int sw = w - 1;
            int sh = h - 1;

            Vector3[] positions = new Vector3[w * h];
            Vector2[] texCoords = new Vector2[w * h];
            int indexCount = sw * sh * 6;
            int[] indices = new int[indexCount];

            mesh.Clear();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            int a = 0;
            int b = 0;
            int c = 0;
            int tri = 0;

            for (int y = 0; y < sh; y++)
            {
                for (int x = 0; x < sw; x++)
                {
                    a = x + y * w;
                    b = (x + 1) + y * w;
                    c = (x + 1) + (y + 1) * w;

                    indices[tri++] = c;
                    indices[tri++] = b;
                    indices[tri++] = a;

                    a = x + y * w;
                    b = (x + 1) + (y + 1) * w;
                    c = x + (y + 1) * w;

                    indices[tri++] = c;
                    indices[tri++] = b;
                    indices[tri++] = a;
                }
            }

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {

                    int ind = x + y * w;
                    switch (rangeType)
                    {
                        case CoordinateRangeType.NDC:
                            positions[ind] = new Vector3(((float)x / fw) * 2.0f - 1.0f, ((float)y / fh) * 2.0f - 1.0f, 0); // NDC space units (-1 .. 1)
                            break;
                        case CoordinateRangeType.Normalized:
                            positions[ind] = new Vector3(((float)x / fw), ((float)y / fh), 0); // normalized space units (0 .. 1)
                            break;
                        case CoordinateRangeType.Pixels:
                            positions[ind] = new Vector3(x, y, 0); // world space units (1 unit / meter per pixel)
                            break;
                    }
                    texCoords[ind] = new Vector2((float)x / fw, (float)y / fh);
                }
            }

            mesh.vertices = positions;
            mesh.uv = texCoords;
            mesh.triangles = indices;
            mesh.RecalculateNormals();
        }

        internal static void addVertex(int triangle, int tri_pos, int vert, int x, int y, float fw, float fh, Vector3[] positions, Vector2[] uv, int[] indices, CoordinateRangeType rangeType)
        {
            indices[vert] = vert;
            switch (rangeType)
            {
                case CoordinateRangeType.NDC:
                    positions[vert] = new Vector3(((float)x / fw) * 2.0f - 1.0f, ((float)y / fh) * 2.0f - 1.0f, 0); // NDC space units (-1 .. 1)
                    break;
                case CoordinateRangeType.Normalized:
                    positions[vert] = new Vector3(((float)x / fw), ((float)y / fh), 0); // normalized space units (0 .. 1)
                    break;
                case CoordinateRangeType.Pixels:
                    positions[vert] = new Vector3(x, y, 0); // world space units (1 unit / meter per pixel)
                    break;
            }
            uv[vert] = new Vector2((float)triangle, (float)tri_pos);
        }

        public static void CreateTriangleLattice(Mesh mesh, Vector2 dims, CoordinateRangeType rangeType)
        {
            int w = (int)dims.x;
            int h = (int)dims.y;

            int vertexCount = (w - 1) * (h - 1) * 6;
            Vector3[] positions = new Vector3[vertexCount];
            Vector2[] uv = new Vector2[vertexCount];
            int[] indices = new int[vertexCount];

            mesh.Clear();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

            int vert = 0;
            int tri = 0;

            for (int y = 0; y < h - 1; y++)
            {
                for (int x = 0; x < w - 1; x++)
                {
                    addVertex(tri, 0, vert, x, y, dims.x, dims.y, positions, uv, indices, rangeType);
                    vert++;

                    addVertex(tri, 1, vert, x, y + 1, dims.x, dims.y, positions, uv, indices, rangeType);
                    vert++;

                    addVertex(tri, 2, vert, x + 1, y, dims.x, dims.y, positions, uv, indices, rangeType);
                    vert++;
                    tri++;

                    addVertex(tri, 0, vert, x + 1, y + 1, dims.x, dims.y, positions, uv, indices, rangeType);
                    vert++;

                    addVertex(tri, 1, vert, x + 1, y, dims.x, dims.y, positions, uv, indices, rangeType);
                    vert++;

                    addVertex(tri, 2, vert, x, y + 1, dims.x, dims.y, positions, uv, indices, rangeType);
                    vert++;
                    tri++;
                }
            }

            mesh.vertices = positions;
            mesh.uv = uv;
            mesh.triangles = indices;
        }

        public static void CreateCubeMesh(Mesh mesh, Vector3 center, Vector3 size, int totalTriangles)
        {
            Vector3[] vertices = new Vector3[totalTriangles * 3];
            Vector2[] uvs = new Vector2[totalTriangles * 3];
            int[] triangles = new int[totalTriangles * 3];

            Vector3[] cubeVerts = GetCubeVerts();
            int[] cubeTriangles = GetCubeTriangles();

            int tri = 0;
            int i = 0;
            for (; i < 12; ++i)
            {
                int index = i * 3;
                Vector3 v1 = cubeVerts[cubeTriangles[index]];
                vertices[index] = new Vector3(v1.x * size.x + center.x, v1.y * size.y + center.y, v1.z * size.z + center.z);
                uvs[index] = new Vector2(tri, 0);
                triangles[index] = index;

                ++index;
                Vector3 v2 = cubeVerts[cubeTriangles[index]];
                vertices[index] = new Vector3(v2.x * size.x + center.x, v2.y * size.y + center.y, v2.z * size.z + center.z);
                uvs[index] = new Vector2(tri, 1);
                triangles[index] = index;

                ++index;
                Vector3 v3 = cubeVerts[cubeTriangles[index]];
                vertices[index] = new Vector3(v3.x * size.x + center.x, v3.y * size.y + center.y, v3.z * size.z + center.z);
                uvs[index] = new Vector2(tri, 2);
                triangles[index] = index;

                ++tri;
            }

            for (; i < totalTriangles; ++i)
            {
                int index = i * 3;
                vertices[index] = new Vector3(0, 0, 0);
                uvs[index] = new Vector2(tri, 0);
                triangles[index] = index;

                index++;
                vertices[index] = new Vector3(0, 0, 0);
                uvs[index] = new Vector2(tri, 1);
                triangles[index] = index;

                index++;
                vertices[index] = new Vector3(0, 0, 0);
                uvs[index] = new Vector2(tri, 2);
                triangles[index] = index;

                ++tri;
            }

            mesh.Clear();
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
        }

    }
}