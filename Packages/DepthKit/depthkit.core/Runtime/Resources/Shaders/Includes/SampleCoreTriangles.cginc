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

#ifndef _DEPTHKIT_SAMPLETRIANGLES_CGINC
#define _DEPTHKIT_SAMPLETRIANGLES_CGINC

#include "Packages/nyc.scatter.depthkit.core/Runtime/Resources/Shaders/Includes/CoreVertex.cginc"
#include "Packages/nyc.scatter.depthkit.core/Runtime/Resources/Shaders/Includes/CoreTriangle.cginc"
#include "Packages/nyc.scatter.depthkit.core/Runtime/Resources/Shaders/Includes/Utils.cginc"

StructuredBuffer<Triangle> _TriangleBuffer;
StructuredBuffer<uint> _TrianglesCount;

#ifdef DK_CORE_PACKED_TRIANGLE

Vertex dkSampleTriangleBuffer(uint triangleId, uint vertexId)
{
    if (triangleId >= _TrianglesCount[0])
    {
        return newVertex();
    }
    else 
    {
        Triangle t = _TriangleBuffer[triangleId];
        Vertex v = t.vertex[vertexId];
        v.normal = clamp3(v.normal, float3(-1.0, -1.0, -1.0), float3(1.0, 1.0, 1.0));
        return v;
    }
}

#else

StructuredBuffer<Vertex> _VertexBuffer;

Vertex dkSampleTriangleBuffer(uint triangleId, uint vertexId)
{
    if (triangleId >= _TrianglesCount[0])
    {
        return newVertex();
    }
    else
    {
        Triangle t = _TriangleBuffer[triangleId];
        Vertex v = _VertexBuffer[t.vertex[vertexId]];
        v.normal = clamp3(v.normal, float3(-1.0, -1.0, -1.0), float3(1.0, 1.0, 1.0));
        return v;
    }
}

#endif

#endif