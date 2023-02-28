using UnityEngine;
using System;
using System.Runtime.InteropServices;

namespace Depthkit
{
    [Serializable]
    public class SyncedStructuredBuffer<T>
    {
        public ComputeBuffer buffer;

        [SerializeField]
        protected T[] m_data = null;

        bool m_dirty = true;

        [SerializeField]
        string m_name;

        public SyncedStructuredBuffer(string name, int count, T[] defaultData = null)
        {
            m_name = name;
            if (defaultData != null)
            {
                m_data = defaultData;
            }
            else
            {
                m_data = new T[count];
            }
            MarkDirty();
        }

        public int Length { get { return m_data != null ? m_data.Length : 0; } }

        public void MarkDirty()
        {
            m_dirty = true;
        }

        public bool Sync()
        {
            if ((m_dirty || buffer == null || !buffer.IsValid()) && m_data != null && m_data.Length > 0)
            {
                if (Util.EnsureComputeBuffer(ComputeBufferType.Default, ref buffer, m_data.Length, Marshal.SizeOf(typeof(T)), m_data))
                {
                    if(m_name != string.Empty)
                    {
                        buffer.name = m_name;
                    }
                }
                m_dirty = false;
                return true;
            }
            return false;
        }

        public void Release()
        {
            if (buffer != null && buffer.IsValid())
            {
                buffer.Release();
            }
            buffer = null;
        }
    }
}