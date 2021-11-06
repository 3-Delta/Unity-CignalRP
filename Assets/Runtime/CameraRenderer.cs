using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace CignalRP {
    // 负责单个相机的渲染
    public partial class CameraRenderer {
        public string ProfileName { get; protected set; } = "CRP|CameraRender";
        public CommandBuffer cmdBuffer { get; protected set; } = new CommandBuffer();

        public Camera camera { get; protected set; } = null;
        private ScriptableRenderContext context;
        private CullingResults cullingResults;

        private Lighting lighting = new Lighting();

        public static readonly int FramebufferId = Shader.PropertyToID("_CameraFrameBuffer");
        private PostProcessStack postProcessStack = new PostProcessStack();
        private PostProcessSettings postProcessSettings;
        private bool allowHDR;

        // https://www.pianshen.com/article/7860291589/
        // Shader中不写 LightMode 时默认ShaderTagId值为“SRPDefaultUnlit”
        // https://www.xuanyusong.com/archives/4759
        // URP以后并不是所有Pass都会执行，因为它预制了两个Pass所以，优先执行”UniversalForward”在执行”SrpDefaultUnlit”的Pass
        // URP保留来了对于UnlitShader的支持
        private static readonly ShaderTagId UnlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
        private static readonly ShaderTagId LitShaderTagId = new ShaderTagId("CRPLit");

        public void Render(ref ScriptableRenderContext context, Camera camera, bool useDynamicBatching, bool useGPUInstancing,
            ShadowSettings shadowSettings, PostProcessSettings postProcessSettings, bool useHDR) {
            this.camera = camera;
            this.context = context;
            this.postProcessSettings = postProcessSettings;

            if (camera.TryGetComponent(out CameraRendererIni cameraIni)) {
                if (cameraIni.rendererFrequency <= 0) {
                    cameraIni.rendererFrequency = -1;
                }

                if (cameraIni.rendererFrequency != -1) {
                    if (Time.frameCount % cameraIni.rendererFrequency == 0) {
                        return;
                    }
                }
            }

#if UNITY_EDITOR
            this.Prepare();
#endif

            if (!this.TryCull(out this.cullingResults, shadowSettings)) {
                return;
            }

            if (camera.cameraType == CameraType.Game) {
                this.allowHDR = useHDR && camera.allowHDR;
            }
#if UNITY_EDITOR
            else if (camera.cameraType == CameraType.SceneView) {
                this.allowHDR = useHDR && SceneView.currentDrawingSceneView.sceneViewState.showImageEffects;
            }
            else {
                this.allowHDR = false;
            }
#endif
            
            ProfileSample(ref context, cmdBuffer, true, this.ProfileName);

            #region 绘制Shadow
            this.PreDraw(shadowSettings);
            #endregion

            #region 绘制Camera常规内容
            ProfileSample(ref context, cmdBuffer, true, this.ProfileName);
            this.Draw(useDynamicBatching, useGPUInstancing);
            ProfileSample(ref context, cmdBuffer, false, this.ProfileName);
            #endregion

            #region 绘制 后处理
            this.PostDraw();
            #endregion

            ProfileSample(ref context, cmdBuffer, false, this.ProfileName);

            // submit之后才会开始绘制本桢
            this.context.Submit();
        }

        #region Cull
        // 是否有任意物体进入该camera的视野，得到剔除结果
        private bool TryCull(out CullingResults cullResults, ShadowSettings shadowSettings) {
            cullResults = default;
            // 以物体为基准，剔除视野之外的物体，应该没有执行遮挡剔除
            // layer裁减等操作
            if (this.camera.TryGetCullingParameters(out ScriptableCullingParameters parameters)) {
                parameters.shadowDistance = Mathf.Min(shadowSettings.maxShadowVSDistance, this.camera.farClipPlane);
                cullResults = this.context.Cull(ref parameters);
                return true;
            }

            return false;
        }
        #endregion

        #region Pre/Post Draw
        private void PreDraw(ShadowSettings shadowSettings) {
            // 设置光源,阴影信息, 内含shadowmap的渲染， 所以需要在正式的相机参数等之前先渲染， 否则放在函数最尾巴，则渲染为一片黑色
            this.lighting.Setup(ref this.context, ref this.cullingResults, shadowSettings);

            // 设置vp矩阵给shader的unity_MatrixVP属性，在Framedebugger中选中某个dc可看
            // vp由CPU构造
            this.context.SetupCameraProperties(this.camera);

            // 有时候rendertarget是rt,那么怎么控制这个	ClearRenderTarget是对于camera生效，还是对于rt生效呢？
            // 猜测应该是向上查找最近的一个rendertarget，也就是setrendertarget, 因为这里没有明显的设置过rendertarget，所以就当是framebuffer

            //    public enum CameraClearFlags {
            //    Skybox = 1,
            //    Color = 2,
            //    SolidColor = 2,
            //    Depth = 3,
            //    Nothing = 4
            // }
            CameraClearFlags flags = this.camera.clearFlags;
            if (this.postProcessStack.IsActive) {
                // 后效开启时,在渲染每个camera的时候,都强制cleardepth,clearcolor
                if (flags > CameraClearFlags.Color) {
                    flags = CameraClearFlags.Color;
                }

                RenderTextureFormat format = allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
                this.cmdBuffer.GetTemporaryRT(FramebufferId, this.camera.pixelHeight, this.camera.pixelHeight, 32,
                    FilterMode.Bilinear, format);
                this.cmdBuffer.SetRenderTarget(FramebufferId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            }

            bool clearDepth = flags <= CameraClearFlags.Depth;
            bool clearColor = flags == CameraClearFlags.Color;
            Color bgColor = clearColor ? this.camera.backgroundColor.linear : Color.clear;
            // 为了Profiler以及Framedebugger中捕获
            // ClearRendererTarget会自动收缩在CmdBufferName下面
            this.cmdBuffer.ClearRenderTarget(clearDepth, clearColor, bgColor);
        }

        private void PostDraw() {
#if UNITY_EDITOR
            this.DrawGizmosBeforeFX();
#endif
            this.postProcessStack.Setup(ref this.context, this.camera, this.postProcessSettings, allowHDR);
            if (this.postProcessStack.IsActive) {
                this.postProcessStack.Render(FramebufferId);
            }
#if UNITY_EDITOR
            this.DrawGizmosAfterFX();
#endif

            this.lighting.Clean();

            if (this.postProcessStack.IsActive) {
                this.cmdBuffer.ReleaseTemporaryRT(FramebufferId);
            }
        }
        #endregion

        #region Draw
        // 内联优化
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ProfileSample(ref ScriptableRenderContext context, CommandBuffer cmdBuffer, bool begin, string sampleName) {
            if (begin) {
                cmdBuffer.BeginSample(sampleName);
            }
            else {
                cmdBuffer.EndSample(sampleName);
            }

            ExecuteCmdBuffer(ref context, cmdBuffer);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void ExecuteCmdBuffer(ref ScriptableRenderContext context, CommandBuffer cmdBuffer) {
            context.ExecuteCommandBuffer(cmdBuffer);
            cmdBuffer.Clear();
        }

        private void Draw(bool useDynamicBatching, bool useGPUInstancing) {
            // step1: 绘制不透明物体
            var sortingSettings = new SortingSettings() {
                // todo 设置lightmode的pass以及物体排序规则， 是否可以利用GPU的hsr规避这里的排序？？？
                criteria = SortingCriteria.CommonOpaque
            };
            var drawingSettings = new DrawingSettings(UnlitShaderTagId, sortingSettings) {
                enableDynamicBatching = useDynamicBatching,
                enableInstancing = useGPUInstancing
            };
            // 渲染CRP光照的pass
            drawingSettings.SetShaderPassName(1, LitShaderTagId);

            var filteringSetttings = new FilteringSettings(RenderQueueRange.opaque);
            this.context.DrawRenderers(this.cullingResults, ref drawingSettings, ref filteringSetttings);

            // step2: 绘制天空盒
            // skybox和opaque进行ztest
            this.context.DrawSkybox(this.camera);

            // step3: 绘制半透明物体
            sortingSettings.criteria = SortingCriteria.CommonTransparent;
            drawingSettings.sortingSettings = sortingSettings;
            filteringSetttings.renderQueueRange = RenderQueueRange.transparent;
            this.context.DrawRenderers(this.cullingResults, ref drawingSettings, ref filteringSetttings);

#if UNITY_EDITOR
            this.DrawUnsupported();
#endif
        }
        #endregion
    }
}
