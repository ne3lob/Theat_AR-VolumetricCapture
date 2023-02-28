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

#ifndef _DEPTHKIT_STUDIOMESHSOURCECOMMON_CGINC
#define _DEPTHKIT_STUDIOMESHSOURCECOMMON_CGINC

#include "Packages/nyc.scatter.depthkit.core/Runtime/Resources/Shaders/Includes/Utils.cginc"

int _VoxelGridX;
int _VoxelGridY;
int _VoxelGridZ;
float3 _VoxelGridf;
float3 _BoundsSize;
float3 _BoundsCenter;
float _SdfSensitivity = 0.015f; // tweakable sdf range

float _IsoLODScalar;

static uint3 _VoxelGrid = uint3(_VoxelGridX, _VoxelGridY, _VoxelGridZ);
static uint3 _VoxelGridLOD = uint3(_VoxelGridf / _IsoLODScalar);

#ifdef SDF_READ_ONLY
StructuredBuffer<float> _SdfBuffer;
#else
RWStructuredBuffer<float> _SdfBuffer;
#endif

static const float Invalid_Sdf = 3.14159265e+30F; // invalid sdf value
static const float Invalid_Sdf_compare = 3.14159265e+25F; // comparison value to avoid precision issues

uint linearIndex(int3 id)
{
    return toIndex3DClamp(id, _VoxelGridLOD);
}

// get the scaled positions using the geometry LOD level
float3 scaledPosition(uint3 id)
{
    float3 fid = float3(id) * _IsoLODScalar; // multiply position up by LOD level * 2 to stretch the position to the whole voxel grid space
    float3 voxNDC = (fid / _VoxelGridf) - .5; //apply this to the voxel space bounds to get into normalized voxel space
    float3 pos3d = _BoundsCenter + _BoundsSize * voxNDC;
    return pos3d;
}

float3 scaledPositionf(float3 id)
{
    float3 fid = id * _IsoLODScalar; // multiply position up by LOD level * 2 to stretch the position to the whole voxel grid space
    float3 voxNDC = (fid / _VoxelGridf) - .5; //apply this to the voxel space bounds to get into normalized voxel space
    float3 pos3d = _BoundsCenter + _BoundsSize * voxNDC;
    return pos3d;
}

uint3 quantizePosition(float3 wsPosition)
{
    float3 voxNDC = (wsPosition - _BoundsCenter) / _BoundsSize;
    float3 fid = (voxNDC + 0.5) * _VoxelGridf;
    return uint3(fid / _IsoLODScalar);
}

#endif // _DEPTHKIT_STUDIOMESHSOURCECOMMON_CGINC
