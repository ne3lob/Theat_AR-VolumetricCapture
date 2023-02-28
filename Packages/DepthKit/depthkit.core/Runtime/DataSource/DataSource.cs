using UnityEngine;
using UnityEditor;
using System;
using System.Collections.Generic;

namespace Depthkit
{
    [ExecuteInEditMode]
    [DefaultExecutionOrder(-15)]
    public abstract class DataSource : MonoBehaviour
    {
        public DataSourceEvents events;
        [SerializeField]
        private bool m_bIsSetup = false;
        protected Depthkit.Clip m_clip;
        public Depthkit.Clip clip
        {
            get { return m_clip; }
        }

        [SerializeField]
        private string m_parent = "root";
        public string dataSourceParent
        {
            get { return m_parent; }
        }

        private bool m_doUpdate = false;
        private bool m_doResize = false;
        public abstract string DataSourceName();

        private List<WeakReference> m_children;
        public T GetChild<T>(bool create = true) where T : DataSource
        {
            if (m_children == null)
            {
                ResetChildren();
            }
            else
            {
                m_children.RemoveAll(x => x.Target == null);
            }
            //check if we already had it cached
            foreach (var generator in m_children)
            {
                if (generator.Target.GetType().Equals(typeof(T)))
                {
                    return generator.Target as T;
                }
            }
            if (!create) return null;
            //create it if its missing
            T gen = gameObject.GetComponent<T>();
            if (gen == null)
            {
                //add it
                gen = gameObject.AddComponent<T>();
                m_doUpdate = true;
            }
            gen.m_parent = DataSourceName();
            m_children.Add(new WeakReference(gen));
            return gen;
        }

        internal void ResetChildren()
        {
            if (m_children == null)
            {
                m_children = new List<WeakReference>();
            }
            else
            {
                m_children.Clear();
            }
            var dataSources = GetComponents<DataSource>();
            foreach (var source in dataSources)
            {
                if (source.dataSourceParent == DataSourceName())
                {
                    m_children.Add(new WeakReference(source));
                }
            }
        }

        protected virtual void AcquireResources() { }
        protected virtual void FreeResources() { }
        protected virtual void OnAwake() { }
        public virtual void OnCleanup() { }

        public abstract bool OnSetup();
        protected abstract bool OnResize();
        protected abstract bool OnGenerate();

        protected virtual void OnUpdate() { }

        protected virtual bool CanGenerate() { return true; }

        void Awake()
        {
            m_children = new List<WeakReference>();
            events = new DataSourceEvents();
            m_clip = GetComponent<Depthkit.Clip>();
            OnAwake();
        }

        void Start()
        {
            ResetChildren();
            m_bIsSetup = false;
        }

        public void ScheduleGenerate()
        {
            m_doUpdate = true;
        }

        public void ScheduleResize()
        {
            m_doResize = true;
        }
        public void UnscheduleGenerate()
        {
            m_doUpdate = false;
        }

        public void UnscheduleResize()
        {
            m_doResize = false;
        }

#if UNITY_EDITOR
        protected virtual void OnAssemblyReload()
        {
            Setup();
            ScheduleResize();
            ScheduleGenerate();
        }
#endif

        void OnEnable()
        {
#if UNITY_EDITOR
            AssemblyReloadEvents.afterAssemblyReload += OnAssemblyReload;
#endif
            ResetChildren();
            foreach (var child in m_children)
            {
                var gen = child.Target as DataSource;
                if (gen != null)
                    gen.hideFlags = HideFlags.None;
            }
            AcquireResources();
        }

        void OnDisable()
        {
#if UNITY_EDITOR
            AssemblyReloadEvents.afterAssemblyReload -= OnAssemblyReload;
#endif
            FreeResources();

            if (m_children == null) return;

            foreach (var child in m_children)
            {
                var gen = child.Target as DataSource;
                if (gen != null)
                    gen.hideFlags = HideFlags.NotEditable;
            }
        }

        void Reset()
        {
            m_bIsSetup = false;
        }

        public void Setup()
        {
            ResetChildren();
            AcquireResources();
            //Debug.Log(DataSourceName() + " - generator setup started...");
            m_bIsSetup = OnSetup();
            if (m_bIsSetup)
            {
                foreach (var child in m_children)
                {
                    var gen = child.Target as DataSource;
                    if (gen != null)
                        gen.Setup();
                }
                //Debug.Log(DataSourceName() + " - setup SUCCESS");
            }
        }

        public void Cleanup()
        {
            OnCleanup();
            foreach (var child in m_children)
            {
                var gen = child.Target as DataSource;
                if (gen != null)
                    gen.Cleanup();
            }
            m_children.RemoveAll(x => x.Target == null);
        }

        public void Resize()
        {
            //Debug.Log(DataSourceName() + " - generator resize started...");
            if (!gameObject.activeInHierarchy || !gameObject.activeSelf)
            {
                return;
            }
            if (!m_bIsSetup)
            {
                //Debug.Log(DataSourceName() + " - setting up insize resize...");
                Setup();
            }
            if (m_bIsSetup)
            {
                if (OnResize())
                {
                    //Debug.Log(DataSourceName() + " - resize SUCCESS");
                    events.OnDataResized();
                }
                //refresh children regardless
                foreach (var child in m_children)
                {
                    var gen = child.Target as DataSource;
                    if (gen != null)
                        gen.Resize();
                }
            }
        }

        public bool IsSetup()
        {
            return m_bIsSetup;
        }

        public void Generate()
        {
            //Debug.Log(DataSourceName() + " - generator generate started...");
            if (!gameObject.activeInHierarchy || !gameObject.activeSelf)
            {
                return;
            }

            if (!m_bIsSetup)
            {
                Setup();
                if (!m_bIsSetup)
                {
                    return; //early out if setup fails
                }
                Resize();
            }

            if (!CanGenerate()) return;

            if (OnGenerate())
            {
                //Debug.Log(DataSourceName() + " - calling on generated event.");
                events.OnDataGenerated();
                //only generate child data if we processed
                foreach (var child in m_children)
                {
                    var gen = child.Target as DataSource;
                    if (gen != null)
                        gen.Generate();
                }
            }
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                //prompt the editor to update everything so the looks can update themselves with the new data
                EditorApplication.delayCall += EditorApplication.QueuePlayerLoopUpdate;
            }
#endif
            //Debug.Log(DataSourceName() + " - generator generate complete");
        }

        void Update()
        {
            if (m_doResize)
            {
                Resize();
                m_doResize = false;
            }

        }

        void LateUpdate()
        {
            if (m_doUpdate)
            {
                Generate();
                m_doUpdate = false;
            }
            OnUpdate();
        }
    }
}