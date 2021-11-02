
using UnityEditor;

using UnityEngine;
using UnityEngine.Rendering;

namespace CignalRP {
    public enum EPostProcessPass {
        // bloom
        BloomCombine,
        BloomHorizontal,
        BloomVertical,

        // tone map 其实就是亮度,也就是rgb都变小
        // https://www.cnblogs.com/crazylights/p/3957566.html
        ToneMapACES,
        ToneMapNeutral,
        ToneMapReinhard,

        // blit
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
        private bool allowHDR;

        public const int MAX_BLOOM_PYRAMID_COUNT = 5;

        private static readonly int postProcessSourceRTId1 = Shader.PropertyToID("_PostProcessSource1");
        private static readonly int postProcessSourceRTId2 = Shader.PropertyToID("_PostProcessSource2");

        private static readonly int bloomBicubicUpsamplingId = Shader.PropertyToID("_BloomBicubicUpsampling");
        private static readonly int bloomIntensityId = Shader.PropertyToID("_BloomIntensity");
        private static readonly int bloomResultId = Shader.PropertyToID("_BloomResult");

        private int bloomPyramidId;

        public bool IsActive {
            get { return this.postProcessSettings != null; }
        }

        public PostProcessStack() {
            this.bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
            for (int i = 1; i < MAX_BLOOM_PYRAMID_COUNT * 2; i++) {
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
            this.cmdBuffer.SetGlobalTexture(postProcessSourceRTId1, from);

            this.cmdBuffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            this.cmdBuffer.DrawProcedural(Matrix4x4.identity, this.postProcessSettings.material, (int)pass, MeshTopology.Triangles, 3);
        }

        public void Render(int sourceId, bool allowHDR) {
            this.allowHDR = allowHDR;

            if (this.DoBloom(sourceId)) {
                this.DoToneMap(bloomResultId);
                this.cmdBuffer.ReleaseTemporaryRT(bloomResultId);
            }
            else {
                this.DoToneMap(sourceId);
            }

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

        private bool DoBloom(int sourceId) {
            PostProcessSettings.BloomSettings bloomSettings = this.postProcessSettings.bloomSettings;
            int width = this.camera.pixelWidth / 2;
            int height = this.camera.pixelHeight / 2;
            if (bloomSettings.maxIterationCount <= 0 || bloomSettings.intensity <= 0f || width < bloomSettings.downscaleLimit || height < bloomSettings.downscaleLimit) {
                return false;
            }

            this.cmdBuffer.BeginSample("CRP|Bloom");

            int fromId = sourceId;
            int toId = this.bloomPyramidId + 1;
            int i = 0;
            RenderTextureFormat format = this.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
            for (; i < bloomSettings.maxIterationCount; i++) {
                if (height < bloomSettings.downscaleLimit || width < bloomSettings.downscaleLimit) {
                    break;
                }

                int midId = toId - 1;
                this.cmdBuffer.GetTemporaryRT(midId, width, height, 0, FilterMode.Bilinear, format);
                this.cmdBuffer.GetTemporaryRT(toId, width, height, 0, FilterMode.Bilinear, format);
                // 先hor,同时下采样(因为这里分辨率小了)
                this.Draw(fromId, midId, EPostProcessPass.BloomHorizontal);
                // 后ver,不下采样
                this.Draw(midId, toId, EPostProcessPass.BloomVertical);

                fromId = toId;
                toId += 2;

                width /= 2;
                height /= 2;
            }

            this.cmdBuffer.SetGlobalFloat(bloomBicubicUpsamplingId, bloomSettings.bloomBicubicUpsampling ? 1f : 0f);
            this.cmdBuffer.SetGlobalFloat(bloomIntensityId, 1f);
            if (i > 1) {
                this.cmdBuffer.ReleaseTemporaryRT(fromId - 1);
                // todo: 因为-2,和fromid一样指向最后一个rt, -5指向的是倒数第二组的第一个rt, 两个一组
                toId = toId - 2 - 2 - 1;
                for (i -= 1; i > 0; i--) {
                    this.cmdBuffer.SetGlobalTexture(postProcessSourceRTId2, toId + 1);
                    // 每一级都blend, 因为每个pass都是blend one zero 
                    this.Draw(fromId, toId, EPostProcessPass.BloomCombine);

                    this.cmdBuffer.ReleaseTemporaryRT(fromId);
                    this.cmdBuffer.ReleaseTemporaryRT(toId + 1);

                    fromId = toId;
                    toId -= 2;
                }
            }
            else {
                this.cmdBuffer.ReleaseTemporaryRT(this.bloomPyramidId);
            }


            this.cmdBuffer.SetGlobalFloat(bloomIntensityId, bloomSettings.intensity);
            this.cmdBuffer.SetGlobalTexture(postProcessSourceRTId2, sourceId);

            this.cmdBuffer.GetTemporaryRT(bloomResultId, this.camera.pixelWidth, this.camera.pixelHeight, 0,
                FilterMode.Bilinear, format);
            this.Draw(fromId, bloomResultId, EPostProcessPass.BloomCombine);

            this.cmdBuffer.ReleaseTemporaryRT(fromId);

            this.cmdBuffer.EndSample("CRP|Bloom");
            return true;
        }

        private void DoToneMap(int sourceId) {
            PostProcessSettings.ToneMapSettings toneMapSettings= this.postProcessSettings.toneMapSettings;
            EPostProcessPass pass = toneMapSettings.mode < 0 ? EPostProcessPass.Copy : EPostProcessPass.ToneMapACES + (int)toneMapSettings.mode;
            this.Draw(sourceId, BuiltinRenderTextureType.CameraTarget, pass);
        }
    }
}
