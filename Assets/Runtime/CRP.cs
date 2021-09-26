using UnityEngine;
using UnityEngine.Rendering;

namespace CignalRP {
    public class CRP : RenderPipeline {
        private CameraRenderer cameraRenderer = new CameraRenderer();

        protected override void Render(ScriptableRenderContext context, Camera[] cameras) {
            for (int i = 0, length = cameras.Length; i < length; ++i) {
                cameraRenderer.Render(context, cameras[i]);
            }
        }
    }
}
