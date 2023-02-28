using UnityEngine;
using System.Collections.Generic;

namespace Depthkit
{
    [AddComponentMenu("Depthkit/Depthkit Platform Validator")]
    public class PlatformValidator : MonoBehaviour
    {
        void Start()
        {
            List<string> reasons = new List<string>();
            if(!Info.IsPlatformValid(ref reasons))
            {
                foreach(string reason in reasons)
                {
                    Debug.LogError(reason);
                }
            }
        }
    }
}