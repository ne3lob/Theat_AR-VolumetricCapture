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

#if UNITY_EDITOR
using UnityEditor;
#endif
using System;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
public static class Reflector
{
    static Reflector() { }

    public static Dictionary<string, string> GetDerivedTypeSet<T>() where T : class
    {
        Dictionary<string, string> derivedClasses = new Dictionary<string, string>();

        Assembly[] asms = AppDomain.CurrentDomain.GetAssemblies();

        foreach(Assembly asm in asms)
        {
            Type[] types;
            try
            {
                types = asm.GetTypes();
            }
            catch(System.Reflection.ReflectionTypeLoadException)
            {
                continue;
            }
            foreach (Type type in types.Where(myType => myType.IsClass && !myType.IsAbstract && myType.IsSubclassOf(typeof(T))))
            {
                derivedClasses.Add(type.Name, type.AssemblyQualifiedName);
            }
        }
        return derivedClasses;
    }
}

namespace Depthkit
{
    public enum GammaCorrection
    {
        None = 0,
        LinearToGammaSpace = 1,
        GammaToLinearSpace = 2,
        //Unity 2017.1 - 2018.2 has a video player bug where Linear->Gamma needs to be applied twice before texture look up in depth
        LinearToGammaSpace2x = 3

    }

    /// <summary>
    /// A version struct to contain a verison number in major.minor.patch format 
    /// </summary>
    /// <remarks>
    /// Version objects are equitable, compareable and implicitly convertable to string
    /// </remarks>
    public struct Version : System.IEquatable<Version>
    {
        // Read/write auto-implemented properties.
        public byte major { get; private set; }
        public byte minor { get; private set; }
        public byte patch { get; private set; }

        public Version(byte major, byte minor = 0, byte patch = 0)
            : this()
        {
            this.major = major;
            this.minor = minor;
            this.patch = patch;
        }

        public override string ToString()
        {
            return major.ToString() + "." + minor.ToString() + "." + patch.ToString();
        }

        public static implicit operator string(Version v)
        {
            return v.ToString();
        }

        public override bool Equals(object obj)
        {
            if (obj is Version)
            {
                return this.Equals((Version)obj);
            }
            return false;
        }

        public bool Equals(Version other)
        {
            return (major == other.major) && (minor == other.minor) && (patch == other.patch);
        }

        public override int GetHashCode()
        {
            return (int)(major ^ minor ^ patch);
        }

        public static bool operator ==(Version lhs, Version rhs)
        {
            return lhs.Equals(rhs);
        }

        public static bool operator !=(Version lhs, Version rhs)
        {
            return !(lhs.Equals(rhs));
        }

        public static bool operator <(Version lhs, Version rhs)
        {
            if (lhs.Equals(rhs))return false;
            if (lhs.major < rhs.major) { return true; }
            else if (lhs.major == rhs.major)
            {
                if (lhs.minor < rhs.minor) { return true; }
                else if (lhs.minor == rhs.minor)
                {
                    if (lhs.patch < rhs.patch) { return true; }
                }
            }
            return false;
        }

        public static bool operator >(Version lhs, Version rhs)
        { 
            if (lhs.Equals(rhs))return false;
            if (lhs.major > rhs.major) { return true; }
            else if (lhs.major == rhs.major)
            {
                if (lhs.minor > rhs.minor) { return true; }
                else if (lhs.minor == rhs.minor)
                {
                    if (lhs.patch > rhs.patch) { return true; }
                }
            }
            return false;
        }

        public static bool operator <=(Version lhs, Version rhs)
        {
            if (lhs == rhs) return true;
            return lhs < rhs;
        }

        public static bool operator >=(Version lhs, Version rhs)
        {
            if (lhs == rhs) return true;
            return lhs > rhs;
        }
    }

    public class Info {
        public static Version Version = new Version(0, 4, 1);
        public static bool IsPlatformValid(ref List<string> whynot)
        {
            bool valid = true;
            if (!SystemInfo.supportsComputeShaders)
            {
                valid = false;
                whynot.Add("Depthkit requires Compute Shader support. This platform does not support compute shaders.");
            }
            if (SystemInfo.maxComputeBufferInputsVertex < 1)
            {
                valid = false;
                whynot.Add("Depthkit requires binding at least 1 compute buffers in vertex a shader, this platform's binding max: " + SystemInfo.maxComputeBufferInputsVertex);
            }
            if (SystemInfo.maxComputeBufferInputsCompute < 4)
            {
                valid = false;
                whynot.Add("Depthkit requires binding at least 4 compute buffers in a compute shader, this platform's binding max: " + SystemInfo.maxComputeBufferInputsCompute);
            }
            return valid;
        }
    }
}
