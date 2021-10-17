using System;
using UnityEngine;

namespace CignalRP {
    [Serializable]
    public class ShadowSettings {
        // 针对相机，不是光源，而且不是到相机位置的距离， 而是cameraview的depth，简单理解就是到camera的nearplane的距离
        [Min(0.01f)] public float maxShadowDistance = 100f;

        public enum EShadowMapSize {
            _256 = 256,
            _512 = 512,
            _1024 = 1024,
            _2048 = 2048,
            _4096 = 4096,
            _8192 = 8192,
        }

        [Serializable]
        public struct DirectionalShadow {
            public EShadowMapSize shadowMapAtlasSize;
            [Range(1, 4)] public int cascadeCount;
            [Range(0f, 1f)] public float cascadeRatio1, cascadeRatio2, cascadeRatio3;
            
            public Vector3 cascadeRatios {
                get {
                    return new Vector3(cascadeRatio1, cascadeRatio2, cascadeRatio3);
                }
            }
        }
        
        public DirectionalShadow directionalShadow = new DirectionalShadow() {
            shadowMapAtlasSize = EShadowMapSize._1024,
            cascadeCount = 4,
            cascadeRatio1 = 0.1f,
            cascadeRatio2 = 0.25f,
            cascadeRatio3 = 0.5f
        };
    }
}
