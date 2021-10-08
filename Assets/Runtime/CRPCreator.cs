﻿using UnityEngine;
using UnityEngine.Rendering;

namespace CignalRP {
    [CreateAssetMenu(menuName = "CRP/CreateCRP")]
    public partial class CRPCreator : RenderPipelineAsset {
        // srbbatcher >= staticbatching > gpuinstancing > dynamicBatching
        [SerializeField] private bool useDynamicBatching = true;
        [SerializeField] private bool useGPUInstancing = true;
        [SerializeField] private bool useSRPBatcher = true;

        [SerializeField] private ShadowSettings shadowSettings = default;

        protected override RenderPipeline CreatePipeline() {
            return new CRP(useDynamicBatching, useGPUInstancing, useSRPBatcher, shadowSettings);
        }
    }
}