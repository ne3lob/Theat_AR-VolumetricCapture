namespace Depthkit
{
     public delegate void DataSourceEventHandler();

    /// <summary>
    /// Class that contains events a given player could potentially emit for listening. </summary>
    [System.Serializable]
    public class DataSourceEvents 
    {
        private event DataSourceEventHandler m_dataGenerated;
        public event DataSourceEventHandler dataGenerated
        {
            add
            {
                if (m_dataGenerated != null)
                {
                    foreach(DataSourceEventHandler existingHandler in m_dataGenerated.GetInvocationList())
                    {
                        if (existingHandler == value)
                        {
                            return;
                        }
                    }
                }
                m_dataGenerated += value;
            }
            remove
            {
                if (m_dataGenerated != null)
                {
                    foreach(DataSourceEventHandler existingHandler in m_dataGenerated.GetInvocationList())
                    {
                        if (existingHandler == value)
                        {
                            m_dataGenerated -= value;
                        }
                    }
                }
            }
        }
        private event DataSourceEventHandler m_dataResized;
        public event DataSourceEventHandler dataResized
        {
            add
            {
                if (m_dataResized != null)
                {
                    foreach(DataSourceEventHandler existingHandler in m_dataResized.GetInvocationList())
                    {
                        if (existingHandler == value)
                        {
                            return;
                        }
                    }
                }
                m_dataResized += value;
            }
            remove
            {
                if (m_dataResized != null)
                {
                    foreach(DataSourceEventHandler existingHandler in m_dataResized.GetInvocationList())
                    {
                        if (existingHandler == value)
                        {
                            m_dataResized -= value;
                        }
                    }
                }
            }
        }

        public virtual void OnDataGenerated()
        {   
            if(m_dataGenerated != null) { m_dataGenerated(); }
        }
        public virtual void OnDataResized()
        {   
            if(m_dataResized != null) { m_dataResized(); }
        }
    }

}