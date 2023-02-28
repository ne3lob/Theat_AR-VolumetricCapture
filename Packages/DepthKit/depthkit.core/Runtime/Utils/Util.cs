using UnityEngine;
using UnityEngine.Rendering;
using System.Collections.Generic;
using System.Text;
using UnityEngine.XR;
#if UNITY_EDITOR
using UnityEditor;
using System.Reflection;
using System.Linq;
using System.IO;
#endif

namespace Depthkit {

    public enum CoordinateRangeType
    {
        NDC,
        Normalized,
        Pixels
    };

    public enum ImageFormat
    {
        JPG,
        PNG
    };

    public static class Util
    {
        public static int NextMultipleOfX(int input, int x)
        {
            return input % x == 0 ? input : ((input / x) + 1) * x;
        }

        public static string GetScaled2DKernelName(string baseName)
        {
            //todo is 16 kernel size more performant than 8?
            if (SystemInfo.maxComputeWorkGroupSize >= 256 && SystemInfo.maxComputeWorkGroupSizeX >= 16 && SystemInfo.maxComputeWorkGroupSizeY >= 16)
            {
                return baseName + "16x16";
            }

            if (SystemInfo.maxComputeWorkGroupSize >= 64 && SystemInfo.maxComputeWorkGroupSizeX >= 8 && SystemInfo.maxComputeWorkGroupSizeY >= 8)
            {
                return baseName + "8x8";
            }

            return baseName + "4x4";
        }

        public static string GetScaled3DKernelName(string baseName)
        {
            if (SystemInfo.maxComputeWorkGroupSize >= 512 && SystemInfo.maxComputeWorkGroupSizeX >= 8 && SystemInfo.maxComputeWorkGroupSizeY >= 8 && SystemInfo.maxComputeWorkGroupSizeZ >= 8)
            {
                return baseName + "8x8x8";
            }

            return baseName + "4x4x4";
        }

        public static void DispatchGroups(ComputeShader compute, int kernel, int threadsX, int threadsY, int threadsZ)
        {
            uint groupSizeX, groupSizeY, groupSizeZ;

            compute.GetKernelThreadGroupSizes(kernel, out groupSizeX, out groupSizeY, out groupSizeZ);

            // Max(1) since we may have less than THREAD_GROUP_SIZE verts wide or long.
            int groupsX = Mathf.Max((int)Mathf.Ceil(threadsX / (float)groupSizeX), 1);
            int groupsY = Mathf.Max((int)Mathf.Ceil(threadsY / (float)groupSizeY), 1);
            int groupsZ = Mathf.Max((int)Mathf.Ceil(threadsZ / (float)groupSizeZ), 1);

            compute.Dispatch(kernel, groupsX, groupsY, groupsZ);
        }
        
        public static void ClearRenderTexture(RenderTexture renderTexture, Color color)
        {
            if(!renderTexture) return;
            RenderTexture cache = RenderTexture.active;
            RenderTexture.active = renderTexture;
            GL.Clear(false, true, color);
            RenderTexture.active = cache;
        }

        public static void ClearAppendBuffer(this ComputeBuffer appendBuffer)
        {
            // This resets the append buffer buffer to 0
            var dummy1 = RenderTexture.GetTemporary(8, 8, 24, RenderTextureFormat.ARGB32);
            var dummy2 = RenderTexture.GetTemporary(8, 8, 24, RenderTextureFormat.ARGB32);
            var active = RenderTexture.active;

            Graphics.SetRandomWriteTarget(1, appendBuffer);
            Graphics.Blit(dummy1, dummy2);
            Graphics.ClearRandomWriteTargets();

            RenderTexture.active = active;

            dummy1.Release();
            dummy2.Release();
        }

        public static void ReleaseComputeBuffer(ref ComputeBuffer buffer)
        {
            if (buffer != null)
            {
                buffer.Release();
                buffer = null;
            }
        }

        public static void ReleaseRenderTexture(ref RenderTexture tex)
        {
            if (tex != null)
            {
                if(tex.IsCreated())
                {
                    tex.Release();
                }
                tex = null;
            }
        }
        public static RenderTexture CopyFromRenderTextureSettings(RenderTexture tex, Vector2 scale)
        {
            RenderTexture newTex = new RenderTexture((int)((float)tex.width * scale.x), (int)((float)tex.height * scale.y), tex.depth, tex.format);
            newTex.dimension = tex.dimension;
            newTex.volumeDepth = tex.volumeDepth;
            newTex.filterMode = tex.filterMode;
            newTex.name = tex.name + " copy";
            newTex.enableRandomWrite = true;
            newTex.autoGenerateMips = tex.autoGenerateMips;
            newTex.Create();
            return newTex;
        }

        public static Matrix4x4 ComposeExtrinsicsMatrix(Matrix4x4 transformMatrix, Matrix4x4 extrinsics, Bounds bounds)
        {
            // We translate all geometry by boundsCenter here so that 0,0,0 is the middle of the clip bounds.
            var boundsOffset = Matrix4x4.TRS(-bounds.center, Quaternion.identity, Vector3.one);
            return transformMatrix * boundsOffset * extrinsics;
        }

        public static Bounds TransformBounds(Transform _transform, Bounds _localBounds)
        {
            var center = _transform.TransformPoint(_localBounds.center);

            // transform the local extents' axes
            var extents = _localBounds.extents;
            var axisX = _transform.TransformVector(extents.x, 0, 0);
            var axisY = _transform.TransformVector(0, extents.y, 0);
            var axisZ = _transform.TransformVector(0, 0, extents.z);

            // sum their absolute value to get the world extents
            extents.x = Mathf.Abs(axisX.x) + Mathf.Abs(axisY.x) + Mathf.Abs(axisZ.x);
            extents.y = Mathf.Abs(axisX.y) + Mathf.Abs(axisY.y) + Mathf.Abs(axisZ.y);
            extents.z = Mathf.Abs(axisX.z) + Mathf.Abs(axisY.z) + Mathf.Abs(axisZ.z);

            return new Bounds { center = center, extents = extents };
        }
#if UNITY_EDITOR
        public static string IndirectArgsToString(ComputeBuffer buf)
        {
            if (buf == null)
            {
                return "<null>";
            }

            int[] args = new int[buf.count];
            buf.GetData(args);
            return string.Join(", ", args.Select(i => i.ToString()).ToArray());
        }
#endif
        public static RenderTexture CreateRenderTexture(int width, int height, RenderTextureFormat fmt, bool autoMips = false, RenderTextureReadWrite type = RenderTextureReadWrite.Default, FilterMode mode = FilterMode.Point, bool enableRandomWrite = true, int depthBits = 0, TextureDimension dimension = TextureDimension.Tex2D, int volumeDepth = 1)
        {
            RenderTexture tex = new RenderTexture(width, height, depthBits, fmt, type);
            tex.enableRandomWrite = enableRandomWrite;
            tex.filterMode = mode;
            tex.useMipMap = autoMips;
            tex.autoGenerateMips = autoMips;
            tex.dimension = dimension;
            tex.volumeDepth = volumeDepth;
            tex.Create();
            return tex;
        }
        public static bool EnsureRenderTexture(ref RenderTexture tex, int width, int height, RenderTextureFormat fmt, RenderTextureReadWrite type = RenderTextureReadWrite.Default, bool autoMips = false, FilterMode mode = FilterMode.Point, bool enableRandomWrite = true, RenderTextureFormat fallback = RenderTextureFormat.Default, TextureDimension dimension = TextureDimension.Tex2D, int volumeDepth = 1)
        {
            bool didCreate = false;
            if (width == 0 || height == 0) return didCreate;
            if (tex == null || tex.width != width || tex.height != height || tex.format != fmt || tex.dimension != dimension || tex.volumeDepth != volumeDepth)
            {
                if (tex != null && tex.IsCreated())
                {
                    tex.Release();
                }

                if (!SystemInfo.SupportsRenderTextureFormat(fmt))
                {
                    fmt = fallback;
                }

                tex = CreateRenderTexture(width, height, fmt, autoMips, type, mode, enableRandomWrite, 0, dimension, volumeDepth);
                didCreate = tex.IsCreated();
                if (!didCreate)
                {
                    Debug.LogWarning("Failed to create Render Texture with format: " + fmt.ToString() + " falling back to: " + fallback.ToString() + " ignore previous error.");
                    tex = CreateRenderTexture(width, height, fallback, autoMips, type, mode, enableRandomWrite);
                    didCreate = tex.IsCreated();
                }
            }
            return didCreate;
        }

        public static bool EnsureComputeBuffer(ComputeBufferType computeBufferType, ref ComputeBuffer buf, int count, int stride, System.Array defaultValues = null)
        {
            bool wasNew = false;

            if (buf != null && (buf.count != count || buf.stride != stride))
            {
                buf.Release();
                buf = null;
            }

            if (buf == null)
            {
                buf = new ComputeBuffer(count, stride, computeBufferType);
                wasNew = true;
            }

            if (defaultValues != null)
            {
                buf.SetData(defaultValues);
            }

            return wasNew;
        }

        public static Color ColorForCamera(int index)
        {
            switch(index)
            {
                case 0: return new Color(1, 0, 0, 1);
                case 1: return new Color(0, 1, 0, 1);
                case 2: return new Color(0, 0, 1, 1);
                case 3: return new Color(1, 1, 0, 1);
                case 4: return new Color(0, 1, 1, 1);
                case 5: return new Color(1, 0, 1, 1);
                case 6: return new Color(0.5f, 1, 0, 1);
                case 7: return new Color(0, 1, 0.5f, 1);
                case 8: return new Color(1, 0.5f, 0, 1);
                case 9: return new Color(1, 0, 0.5f, 1);
                case 10: return new Color(0.5f, 0, 1, 1);
                case 11: return new Color(0.5f, 0.5f, 1, 1);
                case 12: return new Color(1, 0.5f, 0.5f, 1);
                default: return new Color(1, 1, 1, 1);
            }
        }

        public static void RenderPerspectiveGizmo(Depthkit.Metadata.Perspective perspective, Transform transform, Color color, string label)
        {
#if UNITY_EDITOR
            Handles.Label(transform.TransformPoint(perspective.cameraCenter), label);

            Matrix4x4 storedMatrix = Gizmos.matrix;
            Matrix4x4 camMatrix = new Matrix4x4();
            Vector3 nrm = transform.localToWorldMatrix.MultiplyVector(perspective.cameraNormal).normalized;

            Vector3 pos = transform.localToWorldMatrix.MultiplyPoint(perspective.cameraCenter);

            Quaternion q = new Quaternion();
            q.SetLookRotation(nrm);

            camMatrix.SetTRS(pos, q, Vector3.one);

            Gizmos.color = color;
            Gizmos.matrix = camMatrix;

            float fov = Mathf.Rad2Deg * (2.0f * Mathf.Atan((perspective.depthImageSize.y * 0.5f) / perspective.depthFocalLength.y));

            Gizmos.DrawFrustum(Vector3.zero, fov, perspective.farClip, perspective.nearClip, perspective.depthImageSize.x / perspective.depthImageSize.y);
            Gizmos.matrix = storedMatrix;

            Vector3 endPoint = perspective.cameraCenter + perspective.cameraNormal * 2;

            Gizmos.DrawLine(transform.TransformPoint(perspective.cameraCenter), transform.TransformPoint(endPoint));
#endif
        }

        public static void RenderMetadataGizmos(Depthkit.Metadata metadata, Transform transform)
        {
#if UNITY_EDITOR
            for (int i = 0; i < metadata.perspectivesCount; i++)
            {
                var perspective = metadata.perspectives[i];
                Color camColor = ColorForCamera(i);
                RenderPerspectiveGizmo(perspective, transform, camColor, "Perspective " + i);
            }
#endif
        }

#if UNITY_EDITOR
        public static string GetNextFileName(string filename, string ext)
        {
            if(!File.Exists(filename + ext))
            {
                return filename + ext;
            }
            int count = 1;
            string countSegment = " (" + count + ")";
            while(File.Exists(filename + countSegment + ext))
            {
                count++;
                countSegment = " (" + count + ")";
            }
            return filename + countSegment + ext;
        }
#endif

        public static bool IsVisible(Bounds bounds, Camera camera = null)
        {
            if (camera == null)
            {
                camera = Camera.main;
            }
            if (camera == null) return false;
            Plane[] planes = GeometryUtility.CalculateFrustumPlanes(camera);
            return GeometryUtility.TestPlanesAABB(planes, bounds);
        }
        public static float metersToCm(float meters)
        {
            return meters * 100.0f;
        }
        public static float cmToMeters(float cm)
        {
            return cm / 100.0f;
        }

        public static void EnsureKeyword(ref Material material, string keyword, bool enable)
        {
            bool keywordEnabled = material.IsKeywordEnabled(keyword);
            if (!keywordEnabled && enable)
            {
                material.EnableKeyword(keyword);
            }
            else if (keywordEnabled && !enable)
            {
                material.DisableKeyword(keyword);
            }
        }

        public static void EnsureComputeShader(ref ComputeShader compute, string path, string name = "")
        {
            if (compute == null)
            {
                compute = Resources.Load(path, typeof(ComputeShader)) as ComputeShader;
                if (compute == null)
                {
                    Debug.LogError("unable to load compute shader: " + path);
                }
                else
                {
                    if(name != string.Empty) compute.name = name;
                }
            }
        }

        public static class ArgsBufferPrep
        {
            private static string s_prepareArgsComputeName = "Shaders/Util/PrepareArgs";
            private static ComputeShader s_prepareArgsCompute = null;
            private static int s_prepareDrawIndirectArgsKernelId = -1;
            private static int s_prepareDispatchIndirectArgsKernelId = -1;

            public static class ShaderIds
            {
                public static readonly int
                    _GroupSize = Shader.PropertyToID("_GroupSize"),
                    _DispatchY = Shader.PropertyToID("_DispatchY"),
                    _DispatchZ = Shader.PropertyToID("_DispatchZ"),
                   _SinglePassStereo = Shader.PropertyToID("_SinglePassStereo");
            }

            public static void Setup()
            {
                if (s_prepareArgsCompute == null)
                {
                    s_prepareArgsCompute = Resources.Load(s_prepareArgsComputeName, typeof(ComputeShader)) as ComputeShader;
                    if (s_prepareArgsCompute == null)
                    {
                        Debug.LogError("unable to load compute shader: " + s_prepareArgsComputeName);
                    }
                }
                if (s_prepareDrawIndirectArgsKernelId == -1)
                {
                    s_prepareDrawIndirectArgsKernelId = s_prepareArgsCompute.FindKernel("KPrepareDrawIndirectArgs");
                }
                if (s_prepareDispatchIndirectArgsKernelId == -1)
                {
                    s_prepareDispatchIndirectArgsKernelId = s_prepareArgsCompute.FindKernel("KPrepareDispatchIndirectArgs");
                }
            }

            public static void PrepareDispatchArgs(ComputeBuffer count, ComputeBuffer dispatchArgs, int groupSize, int dispatchY = 1, int dispatchZ = 1)
            {
                s_prepareArgsCompute.SetBuffer(s_prepareDispatchIndirectArgsKernelId, SubMesh.TriangleDataShaderIds._TrianglesCount, count);
                s_prepareArgsCompute.SetBuffer(s_prepareDispatchIndirectArgsKernelId, SubMesh.TriangleDataShaderIds._TrianglesDispatchIndirectArgs, dispatchArgs);
                s_prepareArgsCompute.SetInt(ShaderIds._GroupSize, groupSize);
                s_prepareArgsCompute.SetInt(ShaderIds._DispatchY, dispatchY);
                s_prepareArgsCompute.SetInt(ShaderIds._DispatchZ, dispatchZ);
                s_prepareArgsCompute.Dispatch(s_prepareDispatchIndirectArgsKernelId, 1, 1, 1);
            }

            public static void PrepareDrawArgs(ComputeBuffer count, ComputeBuffer drawArgs, bool forceStereo)
            {
                s_prepareArgsCompute.SetBuffer(s_prepareDrawIndirectArgsKernelId, SubMesh.TriangleDataShaderIds._TrianglesCount, count);
                s_prepareArgsCompute.SetBuffer(s_prepareDrawIndirectArgsKernelId, SubMesh.TriangleDataShaderIds._TrianglesDrawIndirectArgs, drawArgs);
                s_prepareArgsCompute.SetBool(ShaderIds._SinglePassStereo, forceStereo ? true : (XRSettings.stereoRenderingMode == XRSettings.StereoRenderingMode.SinglePassInstanced || XRSettings.stereoRenderingMode == XRSettings.StereoRenderingMode.SinglePassMultiview));
                s_prepareArgsCompute.Dispatch(s_prepareDrawIndirectArgsKernelId, 1, 1, 1);
            }
        }

    }
} // namespace Depthkit
