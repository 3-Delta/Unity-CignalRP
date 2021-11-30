using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace CignalRP {
    [Serializable]
    public class CameraSettings {
        public static readonly CameraSettings Default = new CameraSettings();

        // 相机渲染帧率
        public int rendererFrequency = -1;

        [Serializable]
        public struct FinalBlendMode {
            public BlendMode src, dest;
        }

        [SerializeField] public FinalBlendMode finalBlendMode = new FinalBlendMode() {
            src = BlendMode.One,
            dest = BlendMode.Zero
        };

        public bool enablePostProcess = true;
        public bool overridePostProcess = false;

        public bool maskLights = false;
        [RenderingLayerMaskField] public int renderingLayerMask = -1; // 默认everything
        
        public PostProcessSettings postProcessSettings;
    }
}
