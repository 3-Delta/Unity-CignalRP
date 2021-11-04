using UnityEngine;
using UnityEngine.Rendering;

namespace CignalRP {
    [CreateAssetMenu(menuName = "CRP/CRPAsset")]
    public partial class CRPCreator : RenderPipelineAsset {
        // srbbatcher >= staticbatching > gpuinstancing > dynamicBatching
        [SerializeField] private bool useDynamicBatching = true;
        [SerializeField] private bool useGPUInstancing = true;
        [SerializeField] private bool useSRPBatcher = true;

        [SerializeField] private ShadowSettings shadowSettings = default;
        [SerializeField] private PostProcessSettings postProcessSettings = default;
        [SerializeField] private bool allowHDR = false;
        
        public enum ELUTResolution {
            _16 = 16,
            _32 = 32,
            _64 = 64,
        }

        [SerializeField] private ELUTResolution lutResolution = ELUTResolution._32;

        protected override RenderPipeline CreatePipeline() {
            return new CRP(useDynamicBatching, useGPUInstancing, useSRPBatcher, shadowSettings, postProcessSettings, allowHDR, (int)lutResolution);
        }
    }
}
