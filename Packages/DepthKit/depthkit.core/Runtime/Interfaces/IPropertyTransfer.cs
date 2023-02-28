using System.Collections;
using UnityEngine;

namespace Depthkit
{
    //Transfer properties from a source to a target compute, material or material prop block
    public interface IPropertyTransfer
    {
        void SetProperties(ref ComputeShader compute, int kernel);
        void SetProperties(ref Material material);
        void SetProperties(ref Material material, ref MaterialPropertyBlock block);
    }
}