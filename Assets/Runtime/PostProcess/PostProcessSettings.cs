using System;

using UnityEngine;

namespace CignalRP {
    [CreateAssetMenu(menuName = "CRP/PostProcessSettingsAsset")]
    public class PostProcessSettings : ScriptableObject {
        [SerializeField] private Shader _shader;
        [SerializeField] private Material _material;

        [Serializable]
        public struct BloomSettings {
            [Range(0f, PostProcessStack.MAX_BLOOM_PYRAMID_COUNT)] public int maxIterationCount;
            [Min(1f)] public int downscaleLimit;
            public bool bloomBicubicUpsampling;
            [Min(0f)] public float intensity;
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

        public PostProcessSettings() {
            this._bloomSettings.maxIterationCount = 16;
            this._bloomSettings.downscaleLimit = 2;
            this._bloomSettings.bloomBicubicUpsampling = true;
            this._bloomSettings.intensity = 1f;
        }
    }
}
