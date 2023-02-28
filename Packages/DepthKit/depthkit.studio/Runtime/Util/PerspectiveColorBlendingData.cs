using UnityEngine;
using System;

namespace Depthkit
{
    [Serializable]
    public struct PerspectiveColorBlending
    {
#pragma warning disable CS0414
        public static PerspectiveColorBlending[] Create(int count)
        {
            PerspectiveColorBlending[] data = new PerspectiveColorBlending[count];
            for (int i = 0; i < count; ++i)
            {
                data[i].enabled = 1;
                data[i].edgeMaskEnabled = 1;
                data[i].edgeMaskBlendEdgeMax = 1.0f;
                data[i].edgeMaskBlendEdgeMin = 0.0f;
                data[i].edgeMaskStrength = 1.0f;
                data[i].viewWeightPowerContribution = 1.0f;
                data[i].pad0 = 0;
                data[i].pad1 = 0;
            }
            return data;
        }

        public int enabled;
        public float edgeMaskEnabled;
        public float edgeMaskBlendEdgeMin;
        public float edgeMaskBlendEdgeMax;
        public float edgeMaskStrength;
        public float viewWeightPowerContribution;
        float pad0;
        float pad1;
    };

#pragma warning restore CS0414

    [Serializable]
    public class PerspectiveColorBlendingData : SyncedStructuredBuffer<PerspectiveColorBlending>
    {
        public PerspectiveColorBlendingData(string name, int count) : base(name, count, PerspectiveColorBlending.Create(count))
        { }

        public float GetViewDependentColorBlendContribution(int perspective)
        {
            return m_data[perspective].viewWeightPowerContribution;
        }

        public void SetViewDependentColorBlendContribution(int perspective, float contribution)
        {
            contribution = Mathf.Clamp01(contribution);
            if (!Mathf.Approximately(contribution, m_data[perspective].viewWeightPowerContribution))
            {
                m_data[perspective].viewWeightPowerContribution = contribution;
                MarkDirty();
            }
        }

        public float GetEdgeMaskBlendEdgeMin(int perspective)
        {
            return m_data[perspective].edgeMaskBlendEdgeMin;
        }

        public void SetEdgeMaskBlendEdgeMin(int perspective, float min)
        {
            min = Mathf.Clamp01(min);
            if (!Mathf.Approximately(min, m_data[perspective].edgeMaskBlendEdgeMin))
            {
                m_data[perspective].edgeMaskBlendEdgeMin = min;
                MarkDirty();
            }
        }

        public float GetEdgeMaskStrength(int perspective)
        {
            return m_data[perspective].edgeMaskStrength;
        }

        public void SetEdgeMaskStrength(int perspective, float strength)
        {
            strength = Mathf.Clamp(strength, 1, 10);
            if (!Mathf.Approximately(strength, m_data[perspective].edgeMaskStrength))
            {
                m_data[perspective].edgeMaskStrength = strength;
                MarkDirty();
            }
        }

        public float GetEdgeMaskBlendEdgeMax(int perspective)
        {
            return m_data[perspective].edgeMaskBlendEdgeMax;
        }

        public void SetEdgeMaskBlendEdgeMax(int perspective, float max)
        {
            max = Mathf.Clamp01(max);
            if (!Mathf.Approximately(max, m_data[perspective].edgeMaskBlendEdgeMax))
            {
                m_data[perspective].edgeMaskBlendEdgeMax = max;
                MarkDirty();
            }
        }

        public bool GetEdgeMaskEnabled(int perspective)
        {
            return m_data[perspective].edgeMaskEnabled == 1;
        }

        public void SetEdgeMaskEnabled(int perspective, bool enabled)
        {
            if ((m_data[perspective].edgeMaskEnabled == 1) != enabled)
            {
                m_data[perspective].edgeMaskEnabled = enabled ? 1 : 0;
                MarkDirty();
            }
        }

        public bool GetPerspectiveEnabled(int perspective)
        {
            return m_data[perspective].enabled == 1;
        }

        public void SetPerspectiveEnabled(int perspective, bool enabled)
        {
            if ((m_data[perspective].enabled == 1) != enabled)
            {
                m_data[perspective].enabled = enabled ? 1 : 0;
                MarkDirty();
            }
        }
    };
}