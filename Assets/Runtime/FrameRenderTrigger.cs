using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace CignalRP {
    public class FrameRenderTrigger : MonoBehaviour {
        public enum EventSwitch {
            None = 0,
            Begin = 1,
            End = 2,
            Both = Begin | End
        }

        public EventSwitch eventSwitch = EventSwitch.Both;

        protected virtual void OnEnable() {
            if ((eventSwitch & EventSwitch.Begin) != 0) {
                RenderPipelineManager.beginFrameRendering += OnBeginFrameRender;
            }

            if ((eventSwitch & EventSwitch.End) != 0) {
                RenderPipelineManager.endFrameRendering += OnEndFrameRender;
            }
        }

        protected virtual void OnDisable() {
            RenderPipelineManager.beginFrameRendering -= OnBeginFrameRender;
            RenderPipelineManager.endFrameRendering -= OnEndFrameRender;
        }

        protected virtual void OnBeginFrameRender(ScriptableRenderContext context, Camera[] cameras) { }

        protected virtual void OnEndFrameRender(ScriptableRenderContext context, Camera[] cameras) { }
    }
}
