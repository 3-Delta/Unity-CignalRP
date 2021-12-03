using System;
using UnityEngine;

namespace CignalRP {
    [CreateAssetMenu(menuName = "CRP/PostProcessSettingsAsset")]
    public class PostProcessSettings : ScriptableObject {
        public bool enable = true;
        [SerializeField] private Shader _shader;
        private Material _material;

        [Serializable]
        public struct BloomSettings {
            [Range(0f, PostProcessStack.MAX_BLOOM_PYRAMID_COUNT)]
            public int maxIterationCount;

            [Min(1f)] public int downscaleLimit;
            public bool bloomBicubicUpsampling;
            [Min(0f)] public float intensity;

            public bool ignoreRenderScale;
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
            // 曝光度, 就是:颜色 * float
            public float postExposure;

            // 对比度, 最亮与最暗的比率
            [Range(-100f, 100f)] public float contrast;

            // 滤镜: color * filterColor
            [ColorUsage(false, true)] public Color colorFilter;

            // 色调偏移
            [Range(-180f, 180f)] public float hueShift;

            // 饱和度, 置灰的程度
            [Range(-100f, 100f)] public float saturation;
        }

        [Serializable]
        public struct WhiteBalanceSettings {
            // 色温
            [Range(-100f, 100f)] public float temperation;
            [Range(-100f, 100f)] public float tint;
        }

        [Serializable]
        public struct SplitToneSettings {
            [ColorUsage(false)] public Color shadow, specular;

            // shadow和specular的平衡控制
            [Range(-100f, 100f)] public float balance;
        }

        [Serializable]
        public struct ChannelMixerSettings {
            public Vector3 red, green, blue;
        }

        [Serializable]
        public struct ShadowMidtoneHighlightSettings {
            [ColorUsage(false, true)] public Color shadow, midtone, specular;

            [Range(0f, 2f)] public float shadowStart, shadowEnd, specularStart, specularEnd;
        }

        public enum ELUTResolution {
            _Off,
            _16 = 16,
            _32 = 32,
            _64 = 64,
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

        [SerializeField] private BloomSettings _bloomSettings;
        public BloomSettings bloomSettings => this._bloomSettings;

        [SerializeField] private ToneMapSettings _toneMapSettings;
        public ToneMapSettings toneMapSettings => this._toneMapSettings;

        [SerializeField] private ColorAdjustSettings _colorAdjustSettings = new ColorAdjustSettings {
            colorFilter = Color.white,
        };

        public ColorAdjustSettings colorAdjustSettings => this._colorAdjustSettings;

        [SerializeField] private WhiteBalanceSettings _whiteBalanceSettings;
        public WhiteBalanceSettings whiteBalanceSettings => this._whiteBalanceSettings;

        [SerializeField] private SplitToneSettings _splitToneSettings = new SplitToneSettings() {
            shadow = Color.gray,
            specular = Color.gray
        };

        public SplitToneSettings splitToneSettings => this._splitToneSettings;

        [SerializeField] // [Header("其实就是通过矩阵相乘，将color的某些chanel进行结合")]
        private ChannelMixerSettings _channelMixerSettings = new ChannelMixerSettings() {
            red = Vector3.right,
            green = Vector3.up,
            blue = Vector3.forward
        };

        public ChannelMixerSettings channelMixerSettings => this._channelMixerSettings;

        [SerializeField] private ShadowMidtoneHighlightSettings _shadowMidtoneHighlightSettings = new ShadowMidtoneHighlightSettings() {
            shadow = Color.white,
            midtone = Color.white,
            specular = Color.white,

            shadowStart = 0f,
            shadowEnd = 0.3f,
            specularStart = 0.55f,
            specularEnd = 1f
        };

        public ShadowMidtoneHighlightSettings shadowMidtoneHighlightSettings => this._shadowMidtoneHighlightSettings;

        [SerializeField] public ELUTResolution lutResolution = ELUTResolution._32;
        
        public PostProcessSettings() {
            this._bloomSettings.maxIterationCount = 16;
            this._bloomSettings.downscaleLimit = 2;
            this._bloomSettings.bloomBicubicUpsampling = true;
            this._bloomSettings.intensity = 1f;

            this._toneMapSettings.mode = ToneMapSettings.EMode.Reinhard;
        }
    }
}
