using UnityEngine;
using UnityEngine.Rendering;

namespace CignalRP {
    public class CRP : RenderPipeline {
        private CameraRenderer cameraRenderer = new CameraRenderer();

        private bool useDynamicBatching;
        private bool useGPUInstancing;

        private ShadowSettings shadowSettings;
        
        public CRP(bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher, ShadowSettings shadowSettings) {
            this.useDynamicBatching = useDynamicBatching;
            this.useGPUInstancing = useGPUInstancing;
            this.shadowSettings = shadowSettings;
            
            GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
            GraphicsSettings.lightsUseLinearIntensity = true;
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras) {
            for (int i = 0, length = cameras.Length; i < length; ++i) {
                cameraRenderer.Render(ref context, cameras[i], useDynamicBatching, useGPUInstancing, shadowSettings);
            }
        }
    }
}
