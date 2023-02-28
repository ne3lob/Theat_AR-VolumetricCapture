using UnityEngine;
using UnityEngine.Events;

namespace Depthkit
{
    [AddComponentMenu("Depthkit/Util/Set Capture Frame Rate")]
    public class SetCaptureFrameRate : MonoBehaviour
    {
        public UnityEvent onFrameBegin;
        public UnityEvent onFrameEnd;

        public int captureFramteRate = 60;

        private void Start()
        {
            Time.captureFramerate = captureFramteRate;
        }

        private void Update()
        {
            if (onFrameBegin != null)
            {
                onFrameBegin.Invoke();
            }
        }

        private void LateUpdate()
        {
            if(onFrameEnd != null)
            {
                onFrameEnd.Invoke();
            }
        }
    }
}
