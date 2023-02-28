/************************************************************************************

Depthkit Unity SDK License v1
Copyright 2016-2019 Scatter All Rights reserved.  

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
using System.Linq;
using System.Runtime.InteropServices;

namespace Depthkit
{
    [System.Serializable]
    public class Metadata
    {
        public static readonly int MaxPerspectives = 10;

        [System.Serializable]
        public class MetadataVersion
        {
            public int _versionMajor;
            public int _versionMinor;
            public string format;
        }

        //Used for reading the older single perspective JSON format.
        [System.Serializable]
        public class MetadataSinglePerspective
        {
            public int _versionMajor;
            public int _versionMinor;
            public string format;

            public int numAngles;
            public Vector2 depthImageSize;
            public Vector2 depthPrincipalPoint;
            public Vector2 depthFocalLength;
            public float farClip;
            public float nearClip;

            public int textureWidth;
            public int textureHeight;

            public Matrix4x4 extrinsics;
            public Vector3 boundsCenter;
            public Vector3 boundsSize;
            public Vector4 crop;
            public float clipEpsilon;
        }

        [System.Serializable]
        public class Perspective
        {
            public Vector2 depthImageSize;
            public Vector2 depthPrincipalPoint;
            public Vector2 depthFocalLength;
            public float farClip;
            public float nearClip;
            public Matrix4x4 extrinsics;
            public Matrix4x4 extrinsicsInv;
            public Vector4 crop;
            public float clipEpsilon;
            public Vector3 cameraNormal;
            public Vector3 cameraCenter;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct StructuredPerspectiveData
        {
            public const int size = sizeof(float) * 2 +
                sizeof(float) * 2 +
                sizeof(float) * 2 +
                sizeof(float) +
                sizeof(float) +
                sizeof(float) * 16 +
                sizeof(float) * 16 +
                sizeof(float) * 4 +
                sizeof(float) +
                sizeof(float) * 3 +
                sizeof(float) * 3 +
                sizeof(float);

            public Vector2 depthImageSize;
            public Vector2 depthPrincipalPoint;
            public Vector2 depthFocalLength;
            public float farClip;
            public float nearClip;
            public Matrix4x4 extrinsics;
            public Matrix4x4 extrinsicsInverse;
            public Vector4 crop;
            public float clipEpsilon;
            public Vector3 cameraPosition;
            public Vector3 cameraNormal;
            public float padding;

            public StructuredPerspectiveData(Vector2 dImageSize, Vector2 dPrincipalPoint, Vector2 dFocalLength, float fClip, float nClip, Matrix4x4 ex, Vector4 c, float cEpsilon, Vector3 camPos, Vector3 camNorm)
            {
                depthImageSize = dImageSize;
                depthPrincipalPoint = dPrincipalPoint;
                depthFocalLength = dFocalLength;
                farClip = fClip;
                nearClip = nClip;
                extrinsics = ex;
                extrinsicsInverse = ex.inverse;
                crop = c;
                clipEpsilon = cEpsilon;
                cameraPosition = camPos;
                cameraNormal = camNorm;
                padding = 0;
            }
        }

        public readonly static uint MAX_PERSPECTIVES = 12;

        private const float eps = 0.00000001f;

        public int _versionMajor;
        public int _versionMinor;
        public string format;
        public int textureWidth;
        public int textureHeight;
        public Vector3 boundsCenter;
        public Vector3 boundsSize;
        public Perspective[] perspectives;
        public int perspectivesCount;
        public int numRows;
        public int numColumns;
        public int numAngles;

        public bool Valid()
        {
            return _versionMinor != 0 && perspectives != null;
        }

        // Depth and color resolution for each perspective
        public Vector2Int perspectiveCPPResolution
        {
            get
            {
                if (textureHeight == 0 && textureWidth == 0) return new Vector2Int(0, 0);
                return new Vector2Int
                {
                    x = Mathf.Max(textureWidth / Mathf.Max(numColumns, 1), 1),
                    y = Mathf.Max(textureHeight / Mathf.Max(numRows, 1), 1),
                };
            }
        }

        public Vector2Int perspectiveResolution
        {
            get
            {
                Vector2Int res = perspectiveCPPResolution;
                res.y /= 2;
                return res;
            }
        }

        public Vector2Int paddedTextureDimensions
        {
            get
            {
                return new Vector2Int(
                    Util.NextMultipleOfX(perspectiveResolution.x, 8),
                    Util.NextMultipleOfX(perspectiveResolution.y, 8)
                );
            }
        }

        static Depthkit.Metadata FromSinglePerspective(MetadataSinglePerspective md)
        {
            return new Depthkit.Metadata
            {
                _versionMajor = 0,
                _versionMinor = 4,
                format = md.format,
                boundsCenter = md.boundsCenter,
                boundsSize = md.boundsSize,
                textureWidth = md.textureWidth,
                textureHeight = md.textureHeight,
                perspectives = new Perspective[] {
                    new Perspective {
                        depthImageSize = md.depthImageSize,
                        depthPrincipalPoint = md.depthPrincipalPoint,
                        depthFocalLength = md.depthFocalLength,
                        farClip = md.farClip,
                        nearClip = md.nearClip,
                        extrinsics = md.extrinsics,
                        crop = md.crop,
                        clipEpsilon = md.clipEpsilon
                    }
                }
            };
        }

        public static Depthkit.Metadata CreateFromJSON(string jsonString)
        {
            Depthkit.Metadata metadata;
            MetadataVersion mdVersion = JsonUtility.FromJson<MetadataVersion>(jsonString);

            // Read and upgrade old single perspective format.
            if (mdVersion._versionMajor == 0 && mdVersion._versionMinor < 3)
            {
                MetadataSinglePerspective md = JsonUtility.FromJson<MetadataSinglePerspective>(jsonString);

                //for version 1.0 (from Depthkit Visualize) fill in defaults for missing parameters
                if (mdVersion.format == "perpixel" && mdVersion._versionMinor == 1)
                {
                    //set final image dimensions
                    md.textureWidth = (int)(md.depthImageSize.x);
                    md.textureHeight = (int)(md.depthImageSize.y) * 2;

                    //calculate bounds
                    md.boundsCenter = new Vector3(0f, 0f, (md.farClip - md.nearClip) / 2.0f + md.nearClip);
                    md.boundsSize = new Vector3(md.depthImageSize.x * md.farClip / md.depthFocalLength.x,
                                                md.depthImageSize.y * md.farClip / md.depthFocalLength.y,
                                                md.farClip - md.nearClip);

                    md.numAngles = 1;

                    // check if we have a zero'd crop (is this possible?), if so default to full window
                    if (md.crop.x <= eps && md.crop.y <= eps && md.crop.z <= eps && md.crop.w <= eps)
                    {
                        md.crop = new Vector4(0.0f, 0.0f, 1.0f, 1.0f);
                    }

                    md.extrinsics = Matrix4x4.identity;
                }

                if (md.clipEpsilon < eps)
                {
                    md.clipEpsilon = 0.005f; // default depth clipping epsilon, set for older versions of metadata
                }

                metadata = Depthkit.Metadata.FromSinglePerspective(md);
                metadata.numRows = 1;
                metadata.numColumns = 1;
            }
            else
            {
                // Read multiperspective format.
                metadata = JsonUtility.FromJson<Depthkit.Metadata>(jsonString);
                metadata.boundsCenter.z *= -1;
                metadata.boundsCenter.y *= -1;
                if (mdVersion._versionMinor == 3)
                {
                    metadata.numRows = 1;
                    metadata.numColumns = metadata.numAngles;
                }
            }

            // Convert from Depthkit to Unity coordinate space
            for (int i = 0; i < metadata.perspectives.Length; ++i)
            {
                Matrix4x4 mirror = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(1.0f, -1.0f, -1.0f));
                metadata.perspectives[i].extrinsics = mirror * metadata.perspectives[i].extrinsics * mirror;
                metadata.perspectives[i].extrinsicsInv = metadata.perspectives[i].extrinsics.inverse;

                metadata.perspectives[i].cameraCenter = (metadata.perspectives[i].extrinsics * new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
                metadata.perspectives[i].cameraNormal = (metadata.perspectives[i].extrinsics * new Vector4(0.0f, 0.0f, 1.0f, 0.0f)).normalized;
            }

            metadata.perspectivesCount = metadata.perspectives.Length;

            return metadata;
        }

        public static StructuredPerspectiveData[] FillPersistentMetadataFromPerspectives(Perspective[] perspectives)
        {
            StructuredPerspectiveData[] filledData = new StructuredPerspectiveData[perspectives.Length];
            for (int i = 0; i < perspectives.Length; i++)
            {
                filledData[i] = new StructuredPerspectiveData(perspectives[i].depthImageSize,
                    perspectives[i].depthPrincipalPoint,
                    perspectives[i].depthFocalLength,
                    perspectives[i].farClip,
                    perspectives[i].nearClip,
                    perspectives[i].extrinsics,
                    perspectives[i].crop,
                    perspectives[i].clipEpsilon,
                    perspectives[i].cameraCenter,
                    perspectives[i].cameraNormal);
            }

            return filledData;
        }
    }
}
