using UnityEngine;
using UnityEngine.Rendering;

namespace CignalRP {
    public class CameraRenderTrigger : MonoBehaviour {
        public FrameRenderTrigger.EventSwitch eventSwitch = FrameRenderTrigger.EventSwitch.Both;

        protected virtual void OnEnable() {
            if ((eventSwitch & FrameRenderTrigger.EventSwitch.Begin) != 0) {
                RenderPipelineManager.beginCameraRendering += OnBeginCameraRender;
            }

            if ((eventSwitch & FrameRenderTrigger.EventSwitch.End) != 0) {
                RenderPipelineManager.endCameraRendering += OnEndCameraRender;
            }
        }

        protected virtual void OnDisable() {
            RenderPipelineManager.beginCameraRendering -= OnBeginCameraRender;
            RenderPipelineManager.endCameraRendering -= OnEndCameraRender;
        }

        protected virtual void OnBeginCameraRender(ScriptableRenderContext context, Camera camera) { }

        protected virtual void OnEndCameraRender(ScriptableRenderContext context, Camera camera) { }
    }
}
