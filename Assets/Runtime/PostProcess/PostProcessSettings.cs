using System;

using UnityEngine;

namespace CignalRP {
    [CreateAssetMenu(menuName = "CRP/PostProcessSettingsAsset")]
    public class PostProcessSettings : ScriptableObject {
        [SerializeField] private Shader _shader;
        private Material _material;

        [Serializable]
        public struct BloomSettings {
            [Range(0f, PostProcessStack.MAX_BLOOM_PYRAMID_COUNT)] public int maxIterationCount;
            [Min(1f)] public int downscaleLimit;
            public bool bloomBicubicUpsampling;
            [Min(0f)] public float intensity;
        }

        [Serializable]
        public struct ToneMapSettings {
            public enum EMode { 
                None,

                ACES,
                Neutral,
                Reinhard, // c/(1+c)
            }

            public EMode mode;
        }

        [Serializable]
        public struct ColorAdjustSettings {
            // 曝光度
            public float postExposure;

            // 对比度, 最亮与最暗的比率
            [Range(-100f, 100f)] public float contrast;

            // 滤镜
            [ColorUsage(false, true)] public Color colorFilter;

            // 色调偏移
            [Range(-180f, 180f)] public float hueShift;

            // 饱和度
            [Range(-100f, 100f)] public float saturation;
        }

        public Material material {
            get {
                if (this._material == null && this._shader != null) {
                    this._material = new Material(this._shader);
                    this._material.hideFlags = HideFlags.HideAndDontSave;
                }

                return this._material;
            }
        }

        [SerializeField]
        private BloomSettings _bloomSettings;
        public BloomSettings bloomSettings => this._bloomSettings;

        [SerializeField]
        private ToneMapSettings _toneMapSettings;
        public ToneMapSettings toneMapSettings => this._toneMapSettings;

        [SerializeField]
        private ColorAdjustSettings _colorAdjustSettings = new ColorAdjustSettings {
            colorFilter = Color.white,
        };
        public ColorAdjustSettings colorAdjustSettings => this._colorAdjustSettings;

        public PostProcessSettings() {
            this._bloomSettings.maxIterationCount = 16;
            this._bloomSettings.downscaleLimit = 2;
            this._bloomSettings.bloomBicubicUpsampling = true;
            this._bloomSettings.intensity = 1f;

            this._toneMapSettings.mode = ToneMapSettings.EMode.Reinhard;
        }
    }
}
