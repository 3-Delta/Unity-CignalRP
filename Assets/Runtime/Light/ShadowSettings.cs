using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace CignalRP {
    [Serializable]
    [CreateAssetMenu(menuName = "CRP/ShadowSettings")]
    public class ShadowSettings : ScriptableObject {
        // 针对相机，不是光源，而且不是到相机位置的距离， 而是cameraview的depth，简单理解就是到camera的nearplane的距离
        [Min(0.01f)] public float maxShadowVSDistance = 100f;
        [FormerlySerializedAs("distanceFace")] [Range(0.001f, 1f)] public float distanceFade = 0.1f;
        public bool useShadowMask = true;

        public enum EShadowMapSize {
            _256 = 256,
            _512 = 512,
            _1024 = 1024,
            _2048 = 2048,
            _4096 = 4096,
            _8192 = 8192,
        }

        public enum EFilterMode {
            PCF2x2 = 0,
            PCF3x3,
            PCF5x5,
            PCF7x7,
        }

        public enum EShadow {
            Clip, Dither, Off
        }

        [Serializable]
        public struct DirectionalShadow {
            public EFilterMode filterMode;
            public EShadowMapSize shadowMapAtlasSize;
            [Range(1, 4)] public int cascadeCount;
            [Range(0f, 1f)] public float cascadeRatio1, cascadeRatio2, cascadeRatio3;
            [Range(0.001f, 1f)] public float cascadeFade;

            public Vector3 cascadeRatios {
                get {
                    return new Vector3(cascadeRatio1, cascadeRatio2, cascadeRatio3);
                }
            }
        }
        
        [Serializable]
        public struct OtherShadow {
            public EFilterMode filterMode;
            public EShadowMapSize shadowMapAtlasSize;
        }
        
        public DirectionalShadow directionalShadow = new DirectionalShadow() {
            filterMode = EFilterMode.PCF2x2,
            shadowMapAtlasSize = EShadowMapSize._1024,

            cascadeCount = 4,
            cascadeRatio1 = 0.1f,
            cascadeRatio2 = 0.25f,
            cascadeRatio3 = 0.5f,
            cascadeFade = 0.1f
        };

        public OtherShadow otherShadow = new OtherShadow() {
            filterMode = EFilterMode.PCF2x2,
            shadowMapAtlasSize = EShadowMapSize._1024,
        };
    }
}
