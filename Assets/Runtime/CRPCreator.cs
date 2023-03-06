using UnityEngine;
using UnityEngine.Rendering;

namespace CignalRP {
    [CreateAssetMenu(menuName = "CRP/CRPAsset")]
    public partial class CRPCreator : RenderPipelineAsset {
        // srbbatcher >= staticbatching > gpuinstancing > dynamicBatching
        [Header("Dc")]
        [SerializeField] private bool useDynamicBatching = true;
        [SerializeField] private bool useGPUInstancing = true;
        [SerializeField] private bool useSRPBatcher = true;

        [Header("光影")]
        [SerializeField] private bool usePerObjectLights = true;
        [SerializeField] private ShadowSettings shadowSettings = default;

        [Header("后处理")] [SerializeField] private CameraBufferSettings cameraBufferSettings = new CameraBufferSettings() {
            allowHDR = true,
            renderScale = 1f,
            targetMSAA = MSAASamples.None,
        };
        [SerializeField] private PostProcessSettings postProcessSettings = default;

        [Header("Camera")]
        [SerializeField] private Shader cameraRenderShader = null;

        protected override RenderPipeline CreatePipeline() {
            return new CRP(useDynamicBatching, useGPUInstancing, useSRPBatcher, shadowSettings, postProcessSettings, cameraBufferSettings, usePerObjectLights, cameraRenderShader);
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
