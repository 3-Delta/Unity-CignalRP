using UnityEngine;

namespace CignalRP {
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class CameraRendererIni : MonoBehaviour {
        [SerializeField] private CameraSettings _cameraSettings;

        public CameraSettings cameraSettings {
            get {
                if (_cameraSettings == null) {
                    _cameraSettings = new CameraSettings();
                }

                return _cameraSettings;
            }
        }
    }
}
