using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using static CignalRP.PostProcessSettings;

namespace CignalRP {
    public enum EPostProcessPass {
        // bloom
        BloomCombine,
        BloomHorizontal,
        BloomVertical,

        // tone map 其实就是亮度,也就是rgb都变小
        // https://www.cnblogs.com/crazylights/p/3957566.html
        ColorGradeNone,
        ColorGradeACES,
        ColorGradeNeutral,
        ColorGradeReinhard,
        ColorGradeFinal,

        // blit
        Copy,
        FinalScale,
    }

    public partial class PostProcessStack {
        public const string ProfileName = "CRP|PostProcess";

        public CommandBuffer cmdBuffer { get; protected set; } = new CommandBuffer() {
            name = ProfileName
        };

        private ScriptableRenderContext context;
        private Camera camera;
        private CameraRendererIni cameraIni;
        private PostProcessSettings postProcessSettings;
        private bool allowHDR;

        private Vector2Int renderSize;
        private CameraBufferSettings.EBicubicRescaleMode bicubicRescaleMode;

        public const int MAX_BLOOM_PYRAMID_COUNT = 5;

        private static readonly int postProcessSourceRTId1 = Shader.PropertyToID("_PostProcessSource1");
        private static readonly int postProcessSourceRTId2 = Shader.PropertyToID("_PostProcessSource2");

        private static readonly int bloomBicubicUpsamplingId = Shader.PropertyToID("_BloomBicubicUpsampling");
        private static readonly int bloomIntensityId = Shader.PropertyToID("_BloomIntensity");
        private static readonly int bloomResultId = Shader.PropertyToID("_BloomResult");

        private static readonly int colorAdjustId = Shader.PropertyToID("_ColorAdjust");
        private static readonly int colorFilterId = Shader.PropertyToID("_ColorFilter");

        private static readonly int whiteBalanceId = Shader.PropertyToID("_WhiteBalance");

        private static readonly int splitToneShadowId = Shader.PropertyToID("_SplitToneShadow");
        private static readonly int splitToneSpecularId = Shader.PropertyToID("_SplitToneSpecular");

        private static readonly int channelMixerRedId = Shader.PropertyToID("_ChannelMixerRed");
        private static readonly int channelMixerGreenId = Shader.PropertyToID("_ChannelMixerGreen");
        private static readonly int channelMixerBlueId = Shader.PropertyToID("_ChannelMixerBlue");

        private static readonly int smhShadowId = Shader.PropertyToID("_SMHShadow");
        private static readonly int smhMidtoneId = Shader.PropertyToID("_SMHMidtone");
        private static readonly int smhSpecularId = Shader.PropertyToID("_SMHSpecular");
        private static readonly int smhRangeId = Shader.PropertyToID("_SMHRange");

        private static readonly int colorGradeLUTId = Shader.PropertyToID("_ColorGradeLUT");
        private static readonly int colorGradeLUTParamsId = Shader.PropertyToID("_ColorGradeLUTParams");
        private static readonly int colorGradeLUTInLogCId = Shader.PropertyToID("_ColorGradeLUTInLogC");

        private static readonly int finalSrcBlendId = Shader.PropertyToID("_FinalSrcBlend");
        private static readonly int finalDestBlendId = Shader.PropertyToID("_FinalDestBlend");
        private static readonly int finalScaleId = Shader.PropertyToID("_FinalResultId");
        private static readonly int useBicubicRescaleId = Shader.PropertyToID("_UseBicubicRescale");
        
        private int bloomPyramidId;

        public bool IsActive {
            get { return this.postProcessSettings != null && this.postProcessSettings.enable && cameraEnablePostProcess; }
        }

        public bool cameraEnablePostProcess {
            get { return cameraSettings.enablePostProcess; }
        }

        public CameraSettings cameraSettings {
            get {
                if (cameraIni != null) {
                    return cameraIni.cameraSettings;
                }

                return CameraSettings.Default;
            }
        }

        public PostProcessStack() {
            this.bloomPyramidId = Shader.PropertyToID("_BloomPyramid0");
            for (int i = 1; i < MAX_BLOOM_PYRAMID_COUNT * 2; i++) {
                Shader.PropertyToID("_BloomPyramid" + i);
            }
        }

        public void Setup(ref ScriptableRenderContext context, Camera camera, Vector2Int renderSize, PostProcessSettings postProcessSettings, bool allowHDR, 
            CameraBufferSettings.EBicubicRescaleMode bicubicRescaleMode) {
            this.context = context;
            this.camera = camera;
            this.cameraIni = camera.GetComponent<CameraRendererIni>();
            this.allowHDR = allowHDR;
            this.renderSize = renderSize;
            this.bicubicRescaleMode =  bicubicRescaleMode;

            this.postProcessSettings = allowHDR ? postProcessSettings : null;
            if (cameraSettings.overridePostProcess) {
                this.postProcessSettings = cameraSettings.postProcessSettings;
            }
        }

        private void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, EPostProcessPass pass) {
            // 传递给shader
            // 这里其实只是 重新设置 gpu的texture: _PostProcessSource1
            this.cmdBuffer.SetGlobalTexture(postProcessSourceRTId1, from);
            this.cmdBuffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            this.cmdBuffer.DrawProcedural(Matrix4x4.identity, this.postProcessSettings.material, (int)pass, MeshTopology.Triangles, 3);
        }

        private void DrawFinal(RenderTargetIdentifier from, EPostProcessPass pass) {
            this.cmdBuffer.SetGlobalFloat(finalSrcBlendId, (float)cameraSettings.finalBlendMode.src);
            this.cmdBuffer.SetGlobalFloat(finalDestBlendId, (float)cameraSettings.finalBlendMode.dest);
            this.cmdBuffer.SetGlobalTexture(postProcessSourceRTId1, from);

            // 如果不设置SetViewport，那么后camera的画面会覆盖前camera的画面，即使后camera设置来正确的rt的size,但是会将这个size的画面铺满整个camera
            // 不是是屏幕映射阶段。所以需要设置正确的viewport
            this.cmdBuffer.SetViewport(camera.pixelRect);
            // blend的时候,需要从framebuffer中加载colorbuffer, 所以Load
            var loadAction = cameraSettings.finalBlendMode.dest == BlendMode.Zero ? RenderBufferLoadAction.DontCare : RenderBufferLoadAction.Load;
            this.cmdBuffer.SetRenderTarget(BuiltinRenderTextureType.CameraTarget, loadAction, RenderBufferStoreAction.Store);
            this.cmdBuffer.DrawProcedural(Matrix4x4.identity, this.postProcessSettings.material, (int)pass, MeshTopology.Triangles, 3);
        }

        public void Render(int sourceId) {
            if (this.DoBloom(sourceId)) {
                this.DoColorGradeAndToneMap(bloomResultId);
                this.cmdBuffer.ReleaseTemporaryRT(bloomResultId);
            }
            else {
                this.DoColorGradeAndToneMap(sourceId);
            }

            CmdBufferExt.Execute(ref this.context, this.cmdBuffer);
        }
    }

    public partial class PostProcessStack {
        private bool DoBloom(int sourceId) {
            BloomSettings bloomSettings = this.postProcessSettings.bloomSettings;
            int width = renderSize.x / 2;
            int height = renderSize.y / 2;
            
            if (bloomSettings.ignoreRenderScale) {
                width = camera.pixelWidth / 2;
                height = camera.pixelHeight / 2;
            }

            if (bloomSettings.maxIterationCount <= 0 || bloomSettings.intensity <= 0f || width < bloomSettings.downscaleLimit || height < bloomSettings.downscaleLimit) {
                return false;
            }

            CmdBufferExt.ProfileSample(ref context, cmdBuffer, EProfileStep.Begin, "CRP|Bloom", false);

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

            this.cmdBuffer.GetTemporaryRT(bloomResultId, renderSize.x, renderSize.y, 0,
                FilterMode.Bilinear, format);
            this.Draw(fromId, bloomResultId, EPostProcessPass.BloomCombine);

            this.cmdBuffer.ReleaseTemporaryRT(fromId);

            CmdBufferExt.ProfileSample(ref context, cmdBuffer, EProfileStep.End, "CRP|Bloom", false);
            return true;
        }

        private void ConfigColorAdjust() {
            ColorAdjustSettings colorAdjustSettings = this.postProcessSettings.colorAdjustSettings;

            cmdBuffer.SetGlobalVector(colorAdjustId,
                new Vector4(
                    Mathf.Pow(2f, colorAdjustSettings.postExposure),
                    colorAdjustSettings.contrast * 0.01f + 1f, // 因为range为[-100, 100],这里其实就是(x+100)/100;
                    colorAdjustSettings.hueShift * (1f / 360f),
                    colorAdjustSettings.saturation * 0.01f + 1f
                )
            );

            // 传入colorFilter.linear, 后处理 shader内部都是按照linear处理的
            cmdBuffer.SetGlobalColor(colorFilterId, colorAdjustSettings.colorFilter.linear);
        }

        private void ConfigWhiteBalance() {
            WhiteBalanceSettings whiteBalanceSettings = postProcessSettings.whiteBalanceSettings;

            cmdBuffer.SetGlobalVector(whiteBalanceId, ColorUtils.ColorBalanceToLMSCoeffs(whiteBalanceSettings.temperation, whiteBalanceSettings.tint));
        }

        private void ConfigSplitTone() {
            SplitToneSettings splitToneSettings = postProcessSettings.splitToneSettings;

            Color splitColor = splitToneSettings.shadow;
            splitColor.a = splitToneSettings.balance * 0.01f;
            cmdBuffer.SetGlobalColor(splitToneShadowId, splitColor);

            cmdBuffer.SetGlobalColor(splitToneSpecularId, splitToneSettings.specular);
        }

        private void ConfigChannelMixer() {
            ChannelMixerSettings channelMixerSettings = postProcessSettings.channelMixerSettings;

            cmdBuffer.SetGlobalVector(channelMixerRedId, channelMixerSettings.red);
            cmdBuffer.SetGlobalVector(channelMixerGreenId, channelMixerSettings.green);
            cmdBuffer.SetGlobalVector(channelMixerBlueId, channelMixerSettings.blue);
        }

        private void ConfigSMH() {
            ShadowMidtoneHighlightSettings smh = postProcessSettings.shadowMidtoneHighlightSettings;

            // .linear表明：编辑器中所有颜色都是被当做gamma颜色看待的
            cmdBuffer.SetGlobalColor(smhShadowId, smh.shadow.linear);
            cmdBuffer.SetGlobalColor(smhMidtoneId, smh.midtone.linear);
            cmdBuffer.SetGlobalColor(smhSpecularId, smh.specular.linear);
            cmdBuffer.SetGlobalVector(smhRangeId, new Vector4(smh.shadowStart, smh.shadowEnd, smh.specularStart, smh.specularEnd));
        }

        // 最终执行tonemap
        private void DoColorGradeAndToneMap(int sourceId) {
            int lutResolution = (int)postProcessSettings.lutResolution;
            /*if (lutResolution <= 0) {
                CmdBufferExt.ProfileSample(ref context, cmdBuffer, EProfileStep.Begin, "PostProcess Final", false);
                ToneMapSettings toneMapSettings = this.postProcessSettings.toneMapSettings;
                EPostProcessPass pass = EPostProcessPass.ColorGradeNone + (int)toneMapSettings.mode;
                this.Draw(sourceId, BuiltinRenderTextureType.CameraTarget, EPostProcessPass.Copy);
                CmdBufferExt.ProfileSample(ref context, cmdBuffer, EProfileStep.End, "PostProcess Final", false);
            }
            else*/ {
                CmdBufferExt.ProfileSample(ref context, cmdBuffer, EProfileStep.Begin, "CRP|ColorGrade", false);

                this.ConfigColorAdjust();
                this.ConfigWhiteBalance();
                this.ConfigSplitTone();
                this.ConfigChannelMixer();
                this.ConfigSMH();

                int lutHeight = lutResolution;
                int lutWidth = lutHeight * lutHeight;
                RenderTextureFormat format = allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
                // 文章固定使用DefaultHDR
                cmdBuffer.GetTemporaryRT(colorGradeLUTId, lutWidth, lutHeight, 0, FilterMode.Bilinear, RenderTextureFormat.DefaultHDR);
                cmdBuffer.SetGlobalVector(colorGradeLUTParamsId, new Vector4(lutHeight, 0.5f / lutWidth, 0.5f / lutHeight,
                    lutHeight / (lutHeight - 1f)));

                ToneMapSettings toneMapSettings = this.postProcessSettings.toneMapSettings;
                EPostProcessPass pass = EPostProcessPass.ColorGradeNone + (int)toneMapSettings.mode;
                cmdBuffer.SetGlobalFloat(colorGradeLUTInLogCId, allowHDR && pass != EPostProcessPass.ColorGradeNone ? 1f : 0f);
                Draw(sourceId, colorGradeLUTId, pass);
                
                cmdBuffer.SetGlobalVector(colorGradeLUTParamsId, new Vector4(1f / lutHeight, 1f / lutHeight, lutHeight - 1f));
                if (renderSize.x == camera.pixelWidth) {
                    DrawFinal(sourceId, EPostProcessPass.ColorGradeFinal);
                }
                else {
                    cmdBuffer.SetGlobalFloat(finalSrcBlendId, 1f);
                    cmdBuffer.SetGlobalFloat(finalDestBlendId, 0f);
                    cmdBuffer.GetTemporaryRT(finalScaleId, renderSize.x, renderSize.y, 0, FilterMode.Bilinear, RenderTextureFormat.Default);
                    Draw(sourceId, finalScaleId, EPostProcessPass.ColorGradeFinal);

                    bool bicubicSampling =
                        bicubicRescaleMode == CameraBufferSettings.EBicubicRescaleMode.UpAndDown ||
                        bicubicRescaleMode == CameraBufferSettings.EBicubicRescaleMode.UpOnly &&
                        renderSize.x < camera.pixelWidth;
                    cmdBuffer.SetGlobalFloat(useBicubicRescaleId, bicubicSampling ? 1f : 0f);
                    DrawFinal(finalScaleId, EPostProcessPass.FinalScale);
                    cmdBuffer.ReleaseTemporaryRT(finalScaleId);
                }
                CmdBufferExt.ProfileSample(ref context, cmdBuffer, EProfileStep.End, "CRP|ColorGrade", false);
            }
        }
    }
}
