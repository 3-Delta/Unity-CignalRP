using UnityEngine;
using UnityEngine.Rendering;

namespace CignalRP {
    public class CRP : RenderPipeline {
        private CameraRenderer cameraRenderer = new CameraRenderer();

        private bool useDynamicBatching;
        private bool useGPUInstancing;

        private ShadowSettings shadowSettings;
        private PostProcessSettings postProcessSettings;
        private bool allowHDR;
        private int lutResolution;

        public CRP(bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher, ShadowSettings shadowSettings, 
            PostProcessSettings postProcessSettings, bool allowHDR, int lutResolution) {
            this.useDynamicBatching = useDynamicBatching;
            this.useGPUInstancing = useGPUInstancing;
            this.shadowSettings = shadowSettings;
            this.postProcessSettings = postProcessSettings;
            this.allowHDR = allowHDR;
            this.lutResolution = lutResolution;

            GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
            GraphicsSettings.lightsUseLinearIntensity = true;
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras) {
            for (int i = 0, length = cameras.Length; i < length; ++i) {
                cameraRenderer.Render(ref context, cameras[i], useDynamicBatching, 
                    useGPUInstancing, shadowSettings, postProcessSettings, allowHDR, lutResolution);
            }
        }
    }
}
