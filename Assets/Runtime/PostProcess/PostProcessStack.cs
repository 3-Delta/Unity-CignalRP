using UnityEditor;

using UnityEngine;
using UnityEngine.Rendering;

namespace CignalRP {
    public enum EPostProcessPass {
        BloomHorizontal,
        BloomVertical,
        Copy,
    }

    public partial class PostProcessStack {
        public const string ProfileName = "CRP|PostProcess";

        public CommandBuffer cmdBuffer { get; protected set; } = new CommandBuffer() {
            name = ProfileName
        };

        private ScriptableRenderContext context;
        private Camera camera;
        private CameraRendererIni cameraRendererIni;
        private PostProcessSettings postProcessSettings;

        public const int MAX_BLOOM_Pyramid_COUNT = 16;

        private static readonly int PostProcessSourceRTId1 = Shader.PropertyToID("_PostProcessSource1");
        private static readonly int PostProcessSourceRTId2 = Shader.PropertyToID("_PostProcessSource2");

        private int bloomPyramidId;

        public bool IsActive {
            get { return this.postProcessSettings != null; }
        }

        public PostProcessStack() {
            this.bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
            for (int i = 1; i < MAX_BLOOM_Pyramid_COUNT * 2; i++) {
                Shader.PropertyToID("_BloomPyramid" + i);
            }
        }

        public void Setup(ref ScriptableRenderContext context, Camera camera, PostProcessSettings postProcessSettings) {
            this.context = context;
            this.camera = camera;

            if (camera.TryGetComponent(out this.cameraRendererIni)) {
                this.postProcessSettings = camera.cameraType <= CameraType.SceneView && this.cameraRendererIni.usePostProcess ? postProcessSettings : null;
            }
            else {
                this.postProcessSettings = camera.cameraType <= CameraType.SceneView ? postProcessSettings : null;
            }

#if UNITY_EDITOR
            this.ApplySceneViewState();
#endif
        }

        private void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, EPostProcessPass pass) {
            // 传递给shader
            // 这里其实只是 重新设置 gpu的texture: _PostProcessSource
            this.cmdBuffer.SetGlobalTexture(PostProcessSourceRTId1, from);

            this.cmdBuffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            this.cmdBuffer.DrawProcedural(Matrix4x4.identity, this.postProcessSettings.material, (int)pass, MeshTopology.Triangles, 3);
        }

        public void Render(int sourceId) {
            this.DoBloom(sourceId);

            CameraRenderer.ExecuteCmdBuffer(ref this.context, this.cmdBuffer);
        }
    }

    public partial class PostProcessStack {
#if UNITY_EDITOR
        private void ApplySceneViewState() {
            if (this.camera.cameraType == CameraType.SceneView && !SceneView.currentDrawingSceneView.sceneViewState.showImageEffects) {
                this.postProcessSettings = null;
            }
        }
#endif

        private void DoBloom(int sourceId) {
            this.cmdBuffer.BeginSample("CRP|Bloom");

            int width = this.camera.pixelWidth / 2;
            int height = this.camera.pixelHeight / 2;
            int fromId = sourceId;
            int toId = this.bloomPyramidId + 1;
            PostProcessSettings.BloomSettings bloomSettings = this.postProcessSettings.bloomSettings;

            int i = 0;
            for (; i < bloomSettings.maxIterationCount; i++) {
                if (height < bloomSettings.downscaleLimit || width < bloomSettings.downscaleLimit) {
                    break;
                }

                int midId = toId - 1;
                this.cmdBuffer.GetTemporaryRT(midId, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
                this.cmdBuffer.GetTemporaryRT(toId, width, height, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
                // 先hor,同时下采样(因为这里分辨率小了)
                this.Draw(fromId, midId, EPostProcessPass.BloomHorizontal);
                // 后ver,不下采样
                this.Draw(midId, toId, EPostProcessPass.BloomVertical);

                fromId = toId;
                toId += 2;

                width /= 2;
                height /= 2;
            }

            this.Draw(fromId, BuiltinRenderTextureType.CameraTarget, EPostProcessPass.BloomHorizontal);
            for (i -= 1; i >= 0; i--) {
                this.cmdBuffer.ReleaseTemporaryRT(fromId);
                this.cmdBuffer.ReleaseTemporaryRT(fromId - 1);
                fromId -= 2;
            }

            this.cmdBuffer.EndSample("CRP|Bloom");
        }
    }
}
