using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace CignalRP {
    public class PostProcessStack {
        public const string BufferName = "PostProcess";
        public CommandBuffer cmdBuffer { get; protected set; } = new CommandBuffer() {
            name = BufferName
        };
        
        private ScriptableRenderContext context;
        private Camera camera;
        private PostProcessSettings postProcessSettings;

        public bool IsActive {
            get {
                return postProcessSettings != null;
            }
        }

        public void Setup(ref ScriptableRenderContext context, Camera  camera, PostProcessSettings postProcessSettings) {
            this.context = context;
            this.camera = camera;
            this.postProcessSettings = postProcessSettings;
        }

        public void Render(int sourceId) {
            cmdBuffer.Blit(sourceId, BuiltinRenderTextureType.CameraTarget);
            
            CameraRenderer.ExecuteCmdBuffer(ref context, cmdBuffer);
        }
    }
}
