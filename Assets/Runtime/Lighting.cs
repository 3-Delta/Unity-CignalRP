using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace CignalRP {
    public class Lighting {
        public const string ProfileName = "Lighting";

        private CommandBuffer cmdBuffer = new CommandBuffer() {
            name = ProfileName
        };

        public static readonly int dirLightCountId = Shader.PropertyToID("_DirectionalLightCount");
        public static readonly int dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors");
        public static readonly int dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightWSDirections");

        public const int MAX_DIR_LIGHT_COUNT = 4;
        public static readonly Vector4[] dirLightColors = new Vector4[MAX_DIR_LIGHT_COUNT];
        public static readonly Vector4[] dirLightWSDirections = new Vector4[MAX_DIR_LIGHT_COUNT];

        private Shadow shadow = new Shadow();

        public void Setup(ref ScriptableRenderContext context, ref CullingResults cullingResults, ShadowSettings shadowSettings) {
            cmdBuffer.BeginSample(ProfileName);

            shadow.Setup(ref context, ref cullingResults, shadowSettings);
            SetLights(ref context, ref cullingResults);
            shadow.Render(ref context, ref cullingResults, shadowSettings);
            
            cmdBuffer.EndSample(ProfileName);

            CameraRenderer.ExecuteCmdBuffer(ref context, cmdBuffer);
        }

        public void Clean(ref ScriptableRenderContext context) {
            shadow.Clean(ref context);
        }

        private void SetLights(ref ScriptableRenderContext context, ref CullingResults cullingResults) {
            NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
            int dirLightCount = 0;
            for (int i = 0; i < visibleLights.Length; ++i) {
                VisibleLight curVisibleLight = visibleLights[i];
                if (curVisibleLight.lightType == LightType.Directional) {
                    SetupDirectionalLights(ref context, ref cullingResults, dirLightCount++, ref curVisibleLight);
                    if (dirLightCount >= MAX_DIR_LIGHT_COUNT) {
                        break;
                    }
                }
            }

            cmdBuffer.SetGlobalInt(dirLightCountId, dirLightCount);
            cmdBuffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
            cmdBuffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightWSDirections);
        }

        private void SetupDirectionalLights(ref ScriptableRenderContext context, ref CullingResults cullingResults, int index, ref VisibleLight visibleLight) {
            // finalColor = light.color.linear * light.intensity;
            dirLightColors[index] = visibleLight.finalColor;
            // https://www.zhihu.com/people/kmac-3/answers
            // 右乘，矩阵每一列都是转换后坐标的基向量，所以这里矩阵第3列就是forward, 世界坐标的forward
            // https://zhuanlan.zhihu.com/p/163360207
            // https://www.zhihu.com/question/452040005/answer/1810783856
            // 最后一列代表的是模型中心点的世界坐标
            // localspace的光源的forward方向
            // 方向：指向光源的方向，所以是负数
            dirLightWSDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);

            // 保留Light阴影设置数据，得到可投射shadow的light数据
            // index是dirLightWSDirections的下标
            shadow.ReserveDirectionalShadows(ref context, ref cullingResults, visibleLight.light, index);
            // Light light = RenderSettings.sun;
            // cmdBuffer.SetGlobalVector(dirLightColorId, light.color.linear * light.intensity);
            // cmdBuffer.SetGlobalVector(dirLightDirectionId, -light.transform.forward);
        }
    }
}
