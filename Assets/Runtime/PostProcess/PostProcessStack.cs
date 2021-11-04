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
        private int lutResolution;

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

        public void Setup(ref ScriptableRenderContext context, Camera camera, PostProcessSettings postProcessSettings, bool allowHDR, int lutResolution) {
            this.context = context;
            this.camera = camera;
            this.lutResolution = lutResolution;
            
            this.postProcessSettings = allowHDR ? postProcessSettings : null;
        }

        private void Draw(RenderTargetIdentifier from, RenderTargetIdentifier to, EPostProcessPass pass) {
            // 传递给shader
            // 这里其实只是 重新设置 gpu的texture: _PostProcessSource
            this.cmdBuffer.SetGlobalTexture(postProcessSourceRTId1, from);

            this.cmdBuffer.SetRenderTarget(to, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            this.cmdBuffer.DrawProcedural(Matrix4x4.identity, this.postProcessSettings.material, (int)pass, MeshTopology.Triangles, 3);
        }

        public void Render(int sourceId) {
            this.allowHDR = allowHDR;

            if (this.DoBloom(sourceId)) {
                this.DoColorGradeAndToneMap(bloomResultId);
                this.cmdBuffer.ReleaseTemporaryRT(bloomResultId);
            }
            else {
                this.DoColorGradeAndToneMap(sourceId);
            }

            CameraRenderer.ExecuteCmdBuffer(ref this.context, this.cmdBuffer);
        }
    }

    public partial class PostProcessStack {
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
            this.ConfigColorAdjust();
            this.ConfigWhiteBalance();
            this.ConfigSplitTone();
            this.ConfigChannelMixer();
            this.ConfigSMH();

            int lutHeight = lutResolution;
            int lutWidth = lutHeight * lutHeight;
            RenderTextureFormat format = allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
            // 文章固定使用DefaultHDR
            cmdBuffer.GetTemporaryRT(colorGradeLUTId, lutWidth, lutHeight, 0, FilterMode.Bilinear, format);
            cmdBuffer.SetGlobalVector(colorGradeLUTParamsId, new Vector4(lutHeight, 0.5f / lutWidth, 0.5f / lutHeight,
                lutHeight / (lutHeight - 1f)));

            ToneMapSettings toneMapSettings = this.postProcessSettings.toneMapSettings;
            EPostProcessPass pass = EPostProcessPass.ColorGradeNone + (int)toneMapSettings.mode;
            cmdBuffer.SetGlobalFloat(colorGradeLUTInLogCId, allowHDR && pass != EPostProcessPass.ColorGradeNone ? 1f : 0f);
            Draw(sourceId, colorGradeLUTId, pass);

            cmdBuffer.SetGlobalVector(colorGradeLUTParamsId, new Vector4(1f / lutHeight, 1f / lutHeight, lutHeight - 1f));
            this.Draw(sourceId, BuiltinRenderTextureType.CameraTarget, EPostProcessPass.ColorGradeFinal);
            cmdBuffer.ReleaseTemporaryRT(colorGradeLUTId);
        }
    }
}
