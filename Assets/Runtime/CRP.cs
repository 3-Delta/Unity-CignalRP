using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;
using UnityEngine.Rendering;
using LightType = UnityEngine.LightType;

namespace CignalRP {
    public partial class CRP : RenderPipeline {
        private CameraRenderer cameraRenderer = new CameraRenderer();

        private bool useDynamicBatching;
        private bool useGPUInstancing;

        private bool usePerObjectLights;

        private ShadowSettings shadowSettings;
        private PostProcessSettings postProcessSettings;
        private bool allowHDR;

        public CRP(bool useDynamicBatching, bool useGPUInstancing, bool useSRPBatcher, ShadowSettings shadowSettings,
            PostProcessSettings postProcessSettings, bool allowHDR, bool usePerObjectLights) {
            this.useDynamicBatching = useDynamicBatching;
            this.useGPUInstancing = useGPUInstancing;
            this.shadowSettings = shadowSettings;
            this.postProcessSettings = postProcessSettings;
            this.allowHDR = allowHDR;
            this.usePerObjectLights = usePerObjectLights;

            GraphicsSettings.useScriptableRenderPipelineBatching = useSRPBatcher;
            GraphicsSettings.lightsUseLinearIntensity = true;

            InitForEditor();
        }

        protected override void Render(ScriptableRenderContext context, Camera[] cameras) {
            for (int i = 0, length = cameras.Length; i < length; ++i) {
                cameraRenderer.Render(ref context, cameras[i], useDynamicBatching,
                    useGPUInstancing, shadowSettings, postProcessSettings, allowHDR, usePerObjectLights);
            }
        }
    }

    //#if UNITY_EDITOR
    public partial class CRP : RenderPipeline {
        partial void InitForEditor();

#if UNITY_EDITOR
		partial void InitForEditor () {
			Lightmapping.SetDelegate(lightsDelegate);
		}

		protected override void Dispose (bool disposing) {
			base.Dispose(disposing);
			Lightmapping.ResetDelegate();
		}

		// 烘培delegate,因为点光源等直接bake会明显过亮，是因为使用了builtin的烘培形式，所以需要手动告知Unity
		// todo 方向光不存在这个问题? 不存在,因为方向光永远不衰减!!!
		static Lightmapping.RequestLightsDelegate lightsDelegate =
			(Light[] lights, NativeArray<LightDataGI> output) => {
				var lightData = new LightDataGI();
				for (int i = 0; i < lights.Length; i++) {
					Light light = lights[i];
					switch (light.type) {
						case LightType.Directional:
							var directionalLight = new DirectionalLight();
							LightmapperUtils.Extract(light, ref directionalLight);
							lightData.Init(ref directionalLight);
							break;
						case LightType.Point:
							var pointLight = new PointLight();
							LightmapperUtils.Extract(light, ref pointLight);
							lightData.Init(ref pointLight);
							break;
						case LightType.Spot:
							var spotLight = new SpotLight();
							LightmapperUtils.Extract(light, ref spotLight);
							lightData.Init(ref spotLight);
							break;
						case LightType.Area:
							var rectangleLight = new RectangleLight();
							LightmapperUtils.Extract(light, ref rectangleLight);
							rectangleLight.mode = LightMode.Baked;
							lightData.Init(ref rectangleLight);
							break;
						default:
							lightData.InitNoBake(light.GetInstanceID());
							break;
					}

					lightData.falloff = FalloffType.InverseSquared;
					output[i] = lightData;
				}
			};

#endif
    }
}
