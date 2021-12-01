﻿using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace CignalRP {
    [Serializable]
    public class CameraSettings {
        public static readonly CameraSettings Default = new CameraSettings();

        [Serializable]
        public struct FinalBlendMode {
            public BlendMode src, dest;
        }

        [Header("相机渲染帧率")] public int rendererFrequency = -1;

        [Header("后处理")] public bool enablePostProcess = true;
        public bool overridePostProcess = false;
        public PostProcessSettings postProcessSettings;

        [Header("多相机blend,影响后处理rt的blend")] public FinalBlendMode finalBlendMode = new FinalBlendMode() {
            src = BlendMode.One,
            dest = BlendMode.Zero
        };

        [Header("相机layermask是否影响光源")] public bool toMaskLights = false;
        [RenderingLayerMaskField] public int cameraLayerMask = -1; // 默认everything
    }
}
