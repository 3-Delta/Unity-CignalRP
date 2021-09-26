using UnityEngine;

namespace CignalRP {
    public class CameraRendererIni : MonoBehaviour {
        // 相机渲染帧率
        [Range(1, 120)]
        public int rendererFrequency = 30;
    }
}

