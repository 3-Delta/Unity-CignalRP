using UnityEngine;
using UnityEngine.Rendering;

namespace CignalRP {
    [CreateAssetMenu(menuName = "CRP/CRPAsset")]
    public partial class CRPCreator : RenderPipelineAsset {
        // srbbatcher >= staticbatching > gpuinstancing > dynamicBatching
        [SerializeField] private bool useDynamicBatching = true;
        [SerializeField] private bool useGPUInstancing = true;
        [SerializeField] private bool useSRPBatcher = true;

        [SerializeField] private bool usePerObjectLights = true;

        [SerializeField] private ShadowSettings shadowSettings = default;
        [SerializeField] private PostProcessSettings postProcessSettings = default;
        [SerializeField] private bool allowHDR = false;

        protected override RenderPipeline CreatePipeline() {
            return new CRP(useDynamicBatching, useGPUInstancing, useSRPBatcher, shadowSettings, postProcessSettings, allowHDR, usePerObjectLights);
        }
    }

    public partial class CRPCreator : RenderPipelineAsset {
#if UNITY_EDITOR
        private static string[] _renderingLayerNames;

        public override string[] renderingLayerMaskNames {
            get { return _renderingLayerNames; }
        }

        static CRPCreator() {
            _renderingLayerNames = new string[32];
            for (int i = 0; i < _renderingLayerNames.Length; ++i) {
                _renderingLayerNames[i] = "LayerMask" + (i + 1);
            }
        }
#endif
    }
}
