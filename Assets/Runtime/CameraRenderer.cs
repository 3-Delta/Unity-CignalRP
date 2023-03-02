using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEngine;
using UnityEngine.Assertions.Must;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace CignalRP {
    // 负责单个相机的渲染
    public partial class CameraRenderer {
        public enum ECopyET {
            CopyColor = 0,
            CopyDepth = 1,
        }
        
        public const string ProfileName = "CRP|CameraRender";
        public CommandBuffer cmdBuffer { get; protected set; } = new CommandBuffer();

        public Camera camera { get; protected set; } = null;
        private ScriptableRenderContext context;
        private CullingResults cullingResults;

        public CameraRendererIni cameraIni;

        public CameraSettings cameraSettings {
            get {
                if (cameraIni != null) {
                    return cameraIni.cameraSettings;
                }

                return CameraSettings.Default;
            }
        }

        private Lighting lighting = new Lighting();

        public static readonly int CameraColorAttachmentId = Shader.PropertyToID("_CameraColorAttachment");
        public static readonly int CameraDepthAttachmentId = Shader.PropertyToID("_CameraDepthAttachment");

        private Material material;
        private Texture2D missingRT; // 有时候depthRT不需要，但是流程会使用到，所以给个默认的
        public static readonly int sourceTextureId = Shader.PropertyToID("_SourceRT");

        private static bool copyTextureSupported = SystemInfo.copyTextureSupport > CopyTextureSupport.None;

        private bool useInterBuffer = false;
        private bool useColorRT = false;
        private bool useDepthRT = false;
        public static readonly int CameraColorRTId = Shader.PropertyToID("_CameraColorRT");
        public static readonly int CameraDepthRTId = Shader.PropertyToID("_CameraDepthRT");

        private bool useRenderScale;
        private Vector2Int renderSize;
        public static readonly int renderSizeId = Shader.PropertyToID("_CameraRenderSize");
        
        public static readonly int finalSrcBlendId = Shader.PropertyToID("_FinalSrcBlend");
        public static readonly int finalDestBlendId = Shader.PropertyToID("_FinalDestBlend");

        private bool allowHDR;
        private PostProcessStack postProcessStack = new PostProcessStack();
        private PostProcessSettings postProcessSettings;

        // https://www.pianshen.com/article/7860291589/
        // Shader中不写 LightMode 时默认ShaderTagId值为“SRPDefaultUnlit”
        // https://www.xuanyusong.com/archives/4759
        // URP以后并不是所有Pass都会执行，因为它预制了两个Pass所以，优先执行”UniversalForward”在执行”SrpDefaultUnlit”的Pass
        // URP保留来了对于UnlitShader的支持
        private static readonly ShaderTagId UnlitShaderTagId = new ShaderTagId("SRPDefaultUnlit");
        private static readonly ShaderTagId LitShaderTagId = new ShaderTagId("CRPLit");

        public CameraRenderer(Shader shader) {
            material = CoreUtils.CreateEngineMaterial(shader);

            missingRT = new Texture2D(1, 1) {
                hideFlags = HideFlags.DontSave,
                name = "MissingRT",
            };
            missingRT.SetPixel(0, 0, Color.white * 0.5f);
            missingRT.Apply(true, true);
        }
        
        public void Dispose() {
            CoreUtils.Destroy(material);
            CoreUtils.Destroy(missingRT);
        }

        public void Render(ref ScriptableRenderContext context, Camera camera, bool useDynamicBatching, bool useGPUInstancing,
            ShadowSettings shadowSettings, PostProcessSettings postProcessSettings, CameraBufferSettings cameraBufferSettings, bool usePerObjectLights) {
            this.camera = camera;
            this.context = context;
            this.postProcessSettings = postProcessSettings;

            bool cameraRenderShadow = true;
            if (camera.TryGetComponent(out cameraIni)) {
                cameraRenderShadow = cameraIni.cameraSettings.renderShadow;
                if ((int)cameraIni.cameraSettings.rendererFrequency <= 0) {
                    cameraIni.cameraSettings.rendererFrequency = CameraSettings.ECameraFrameFrequency.Very;
                }

                //if (cameraIni.cameraSettings.rendererFrequency != CameraSettings.ECameraFrameFrequency.Very) {
                //    if ((Time.frameCount % (int)cameraIni.cameraSettings.rendererFrequency) == 0) {
                //        return;
                //    }
                //}
            }

            if (camera.cameraType == CameraType.Reflection) {
                useColorRT = cameraBufferSettings.copyColorReflection;
                useDepthRT = cameraBufferSettings.copyDepthReflection;
            }
            else {
                useColorRT = cameraBufferSettings.copyColor && cameraSettings.copyColor;
                useDepthRT = cameraBufferSettings.copyDepth && cameraSettings.copyDepth;
            }

            float renderScale = cameraSettings.GetRenderScale(cameraBufferSettings.renderScale);
            useRenderScale = renderScale <= 0.99f || renderScale > 1.01f;
#if UNITY_EDITOR
            this.Prepare();
#endif

            if (!this.TryCull(out this.cullingResults, shadowSettings, cameraRenderShadow)) {
                return;
            }

            if (camera.cameraType == CameraType.Game) {
                this.allowHDR = cameraBufferSettings.allowHDR && camera.allowHDR;
            }
#if UNITY_EDITOR
            else if (camera.cameraType == CameraType.SceneView) {
                this.allowHDR = cameraBufferSettings.allowHDR && SceneView.currentDrawingSceneView.sceneViewState.showImageEffects;
            }
#endif
            else
            {
                this.allowHDR = false;
            }
            if (useRenderScale) {
                renderScale = Mathf.Clamp(renderScale, 0.1f, 2f);
                renderSize.x = (int)(camera.pixelWidth * renderScale);
                renderSize.y = (int)(camera.pixelHeight * renderScale);
            }
            else {
                renderSize.x = camera.pixelWidth;
                renderSize.y = camera.pixelHeight;
            }
            
            cmdBuffer.SetGlobalVector(renderSizeId, new Vector4(1f / renderSize.x, 1f / renderSize.y, renderSize.x, renderSize.y));
            CmdBufferExt.Execute(ref context, cmdBuffer);

            string cameraProfileName = "CRP|" + this.camera.name;
#if UNITY_EDITOR
            Profiler.BeginSample("Editor Only");
            this.cmdBuffer.name = cameraProfileName;
            Profiler.EndSample();
#endif
            CmdBufferExt.ProfileSample(ref context, cmdBuffer, EProfileStep.Begin, cameraProfileName);

            #region 绘制Shadow
            this.PreDraw(shadowSettings, usePerObjectLights, cameraBufferSettings);
            #endregion
            
            #region framebuffer设置
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
            useInterBuffer = useColorRT || useDepthRT || useRenderScale || postProcessStack.IsActive;
            if (useInterBuffer) {
                // 后效开启时,在渲染每个camera的时候,都强制cleardepth,clearcolor, 否则多个camera的内容会叠加
                if (flags > CameraClearFlags.Color) {
                    flags = CameraClearFlags.Color;
                }

                RenderTextureFormat format = allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
                // color无深度信息，所以0
                this.cmdBuffer.GetTemporaryRT(CameraColorAttachmentId, renderSize.x, renderSize.y, 0, FilterMode.Bilinear, format);
                // depth无颜色，所以RenderTextureFormat.Depth
                this.cmdBuffer.GetTemporaryRT(CameraDepthAttachmentId, renderSize.x, renderSize.y, 32, FilterMode.Point, RenderTextureFormat.Depth);

                // 设置之后，zwite on的ztest pass物体就会写入CameraDepthAttachmentId
                this.cmdBuffer.SetRenderTarget(CameraColorAttachmentId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                    CameraDepthAttachmentId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            }
            else {
                // 否则直接写入framebuffer
                // 即 BuiltinRenderTextureType.CameraTarget
            }

            bool clearDepth = flags <= CameraClearFlags.Depth;
            bool clearColor = flags == CameraClearFlags.Color; 
            // 为什么不是flags <= CameraClearFlags.Color, skybox应该也要clear color啊，
            // uwa说是本来skybox就会覆盖，所以这里不clearcolor也可以
            Color bgColor = clearColor ? this.camera.backgroundColor.linear : Color.clear;

#if UNITY_EDITOR // 特殊处理ClearRenderTarget
            Profiler.BeginSample("Editor Only");
            this.cmdBuffer.name = ProfileName;
            Profiler.EndSample();
#endif
            // 为了Profiler以及Framedebugger中捕获
            // ClearRendererTarget会自动收缩在CmdBufferName下面,所以要给设置一个cmdBuffer.name
            this.cmdBuffer.ClearRenderTarget(clearDepth, clearColor, bgColor);

            // 设置默认的color/depth RT
            cmdBuffer.SetGlobalTexture(CameraColorRTId, missingRT);
            cmdBuffer.SetGlobalTexture(CameraDepthRTId, missingRT);
            CmdBufferExt.Execute(ref context, cmdBuffer);
            #endregion

            #region 绘制Camera常规内容
            CmdBufferExt.ProfileSample(ref context, cmdBuffer, EProfileStep.Begin, ProfileName);
            this.Draw(useDynamicBatching, useGPUInstancing, usePerObjectLights, cameraSettings.cameraLayerMask);
            CmdBufferExt.ProfileSample(ref context, cmdBuffer, EProfileStep.End, ProfileName);
            #endregion
            
            #region 绘制 后处理
            this.PostDraw();
            #endregion

            CmdBufferExt.ProfileSample(ref context, cmdBuffer, EProfileStep.End, cameraProfileName);

            // submit之后才会开始绘制本桢
            this.context.Submit();
        }

        #region Cull
        // 是否有任意物体进入该camera的视野，得到剔除结果
        private bool TryCull(out CullingResults cullResults, ShadowSettings shadowSettings, bool cameraRenderShadow) {
            cullResults = default;
            // 以物体为基准，剔除视野之外的物体，应该没有执行遮挡剔除
            // layer裁减等操作
            if (this.camera.TryGetCullingParameters(out ScriptableCullingParameters parameters)) {
                // camera上不勾选cameraRenderShadow，那么在该camera上就会将阴影距离设置为0，这就会剔除所有的光源
                bool canRenderShadow = cameraRenderShadow && SystemInfo.supportsShadows; // 其实还得看light是否投射阴影
                var finalShadowDistance = canRenderShadow ? shadowSettings.maxShadowVSDistance : 0f;
                parameters.shadowDistance = Mathf.Min(finalShadowDistance, this.camera.farClipPlane);

                if (!canRenderShadow) {
                    parameters.cullingOptions &= ~(CullingOptions.ShadowCasters);
                }
                cullResults = this.context.Cull(ref parameters);
                return true;
            }

            return false;
        }
        #endregion

        #region Pre/Post/Draw
        private void PreDraw(ShadowSettings shadowSettings, bool usePerObjectLights, CameraBufferSettings cameraBufferSettings) {
            // 设置光源,阴影信息, 内含shadowmap的渲染， 所以需要在正式的相机参数等之前先渲染， 否则放在函数最尾巴，则渲染为一片黑色
            this.lighting.Setup(ref this.context, ref this.cullingResults, shadowSettings, usePerObjectLights,
                cameraSettings.toMaskLights ? cameraSettings.cameraLayerMask : CameraSettings.ALL_RENDER_LAYER);
            this.postProcessStack.Setup(ref this.context, this.camera, renderSize, this.postProcessSettings, allowHDR, cameraBufferSettings.bicubicRescaleMode);

            // 设置vp矩阵给shader的unity_MatrixVP属性，在Framedebugger中选中某个dc可看
            // 把当前摄像机的信息告诉上下文，这样shader中就可以获取到当前帧下摄像机的信息，比如VP矩阵等， 同时之前因为rendertarget是shadowmap, 所以这里默认还会将屏幕设置为RenderTarget
            // vp由CPU构造，传递给gpu
            this.context.SetupCameraProperties(this.camera);
        }

        private void PostDraw() {
#if UNITY_EDITOR
            this.DrawGizmosBeforeFX();
#endif
            // 此时渲染目标是：CameraColorAttachmentId 和 CameraDepthAttachmentId

            // 后处理激活的情况下
            if (this.postProcessStack.IsActive) {
                this.postProcessStack.Render(CameraColorAttachmentId);
            }
            else if (useInterBuffer) {
                CmdBufferExt.ProfileSample(ref context, cmdBuffer, EProfileStep.Begin, "CRP|Blit", false);
                // blend在后处理不启用的时候，也生效
                // 目的是：多个相机的内容混合
                DrawBlendFinal(cameraSettings.finalBlendMode);
                CmdBufferExt.ProfileSample(ref context, cmdBuffer, EProfileStep.End, "CRP|Blit");
            }
#if UNITY_EDITOR
            this.DrawGizmosAfterFX();
#endif

            this.lighting.Clean();

            if (useInterBuffer) {
                this.cmdBuffer.ReleaseTemporaryRT(CameraColorAttachmentId);
                this.cmdBuffer.ReleaseTemporaryRT(CameraDepthAttachmentId);
                
                // CopyAttachments有可能申请
                if (useColorRT) {
                    this.cmdBuffer.ReleaseTemporaryRT(CameraColorRTId);
                }

                if (useDepthRT) {
                    this.cmdBuffer.ReleaseTemporaryRT(CameraDepthRTId);
                }
            }
        }
        
        private void Draw(bool useDynamicBatching, bool useGPUInstancing, bool usePerObjectLights,
            int cameraRenderingLayerMask) {
            PerObjectData lightPerObjectFlags = PerObjectData.None;
            if (usePerObjectLights) {
                // 每个对象收到几个哪几个光源的影响?
                lightPerObjectFlags = PerObjectData.LightData | PerObjectData.LightIndices;
            }

            // step1: 绘制不透明物体
            // CommonOpaque = SortingLayer | RenderQueue | OptimizeStateChanges | CanvasOrder | QuantizedFrontToBack
            SortingCriteria yesFrontToBackOpaqueFlags = SortingCriteria.CommonOpaque;
            // 少个了QuantizedFrontToBack， 也就是从前到后的cpu物体排序， 最终按照hsr处理
            SortingCriteria noFrontToBackOpaqueFlags = SortingCriteria.SortingLayer | SortingCriteria.RenderQueue | SortingCriteria.OptimizeStateChanges | SortingCriteria.CanvasOrder;

            bool hasHSRGPU = SystemInfo.hasHiddenSurfaceRemovalOnGPU;
            bool canSkipFrontToBackSorting = (camera.opaqueSortMode == OpaqueSortMode.Default && hasHSRGPU) || camera.opaqueSortMode == OpaqueSortMode.NoDistanceSort;
            // hsr影响最终的opaque物体的排序方式
            var sortingCriteria = canSkipFrontToBackSorting ? noFrontToBackOpaqueFlags : yesFrontToBackOpaqueFlags;

            var sortingSettings = new SortingSettings() {
                criteria = sortingCriteria
            };
            var drawingSettings = new DrawingSettings(UnlitShaderTagId, sortingSettings) {
                enableDynamicBatching = useDynamicBatching,
                enableInstancing = useGPUInstancing,
                // 传递obj在lightmap中的uv
                perObjectData = PerObjectData.Lightmaps |
                                PerObjectData.LightProbe |
                                PerObjectData.LightProbeProxyVolume | // lppv
                                
                                PerObjectData.ShadowMask |
                                PerObjectData.OcclusionProbe |
                                PerObjectData.OcclusionProbeProxyVolume |
                                
                                PerObjectData.ReflectionProbes |
                                
                                lightPerObjectFlags
            };
            // 渲染CRP光照的pass
            // 下标的意义就是lightmode的渲染的优先级，0最大 https://zhuanlan.zhihu.com/p/600876271
            // 这里第二个参数好像应该是lightmode的tag，不能使其他tag
            drawingSettings.SetShaderPassName(1, LitShaderTagId);

            var filteringSetttings = new FilteringSettings(RenderQueueRange.opaque, camera.cullingMask, (uint) cameraRenderingLayerMask);
            this.context.DrawRenderers(this.cullingResults, ref drawingSettings, ref filteringSetttings);

            // step2: 绘制天空盒
            // skybox和opaque进行ztest
            this.context.DrawSkybox(this.camera);

            // 绘制完不透明以及天空盒(也是不透明方式绘制)之后，缓存color以及depth
            if (useColorRT || useDepthRT) {
                CopyAttachments();
            }

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

        private void CopyAttachments() {
            if (useColorRT) {
                // 重新配置CameraColorRTId的宽高等属性
                cmdBuffer.GetTemporaryRT(CameraColorRTId, renderSize.x, renderSize.y, 0, FilterMode.Bilinear, allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default);
                if (copyTextureSupported) {
                    cmdBuffer.CopyTexture(CameraColorAttachmentId, CameraColorRTId);
                }
                else {
                    // webgl有的不支持CopyTexture，所以只能低效的DrawCopy，类似blit
                    DrawCopy(CameraColorAttachmentId, CameraColorRTId, ECopyET.CopyColor);
                }
            }

            if (useDepthRT) {
                cmdBuffer.GetTemporaryRT(CameraDepthRTId, renderSize.x, renderSize.y, 32, FilterMode.Point, RenderTextureFormat.Depth);
                if (copyTextureSupported) {
                    cmdBuffer.CopyTexture(CameraDepthAttachmentId, CameraDepthRTId);
                }
                else {
                    DrawCopy(CameraDepthAttachmentId, CameraDepthRTId, ECopyET.CopyDepth);
                }
            }

            if (!copyTextureSupported) {
                // DrawCopy 会修改 rendertarget, 所以这里 设置为原来的
                this.cmdBuffer.SetRenderTarget(CameraColorAttachmentId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store,
                    CameraDepthAttachmentId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            }

            CmdBufferExt.Execute(ref context, cmdBuffer);
        }

        private void DrawCopy(RenderTargetIdentifier from, RenderTargetIdentifier to, ECopyET copyMode) {
            cmdBuffer.SetGlobalTexture(sourceTextureId, from);
            cmdBuffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            int passIndex = (int)copyMode;
            cmdBuffer.DrawProcedural(Matrix4x4.identity, material, passIndex, MeshTopology.Triangles, 3);
        }
        
        private void DrawBlendFinal (CameraSettings.FinalBlendMode finalBlendMode) {
            cmdBuffer.SetGlobalFloat(finalSrcBlendId, (float)finalBlendMode.src);
            cmdBuffer.SetGlobalFloat(finalDestBlendId, (float)finalBlendMode.dest);

            // 将当前camera的CameraColorAttachmentId的颜色传递给屏幕BuiltinRenderTextureType.CameraTarget
            // 目的是为了多相机的颜色混合
            cmdBuffer.SetGlobalTexture(sourceTextureId, CameraColorAttachmentId);
            cmdBuffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget, finalBlendMode.dest == BlendMode.Zero ? RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load, RenderBufferStoreAction.Store);

            // 这里为什么不是 camera.pixelRect * renderScale呢?
            // 因为renderscale只需要影响RT的size就行，最终rt还是需要显示在camera的Viewport上
            cmdBuffer.SetViewport(camera.pixelRect);
            cmdBuffer.DrawProcedural(Matrix4x4.identity, material, 0, MeshTopology.Triangles, 3);
            
            // 还原 混合因子
            cmdBuffer.SetGlobalFloat(finalSrcBlendId, 1f);
            cmdBuffer.SetGlobalFloat(finalDestBlendId, 0f);
        }
    }
}
