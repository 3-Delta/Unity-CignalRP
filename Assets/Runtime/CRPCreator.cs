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
}
