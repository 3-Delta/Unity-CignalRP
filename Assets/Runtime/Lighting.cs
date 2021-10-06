using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace CignalRP {
    public class Lighting {
        public const string ProfileName = "Lighting";
        
        private CommandBuffer cmdBuffer = new CommandBuffer() {
            name = ProfileName
        };

        private CullingResults cullingResults;

        public static readonly int dirLightCountId = Shader.PropertyToID("_DirectionalLightCount");
        public static readonly int dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors");
        public static readonly int dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightWSDirections");
        
        public const int MAX_DIR_LIGHT_COUNT = 4;
        public static readonly Vector4[] dirLightColors = new Vector4[MAX_DIR_LIGHT_COUNT];
        public static readonly Vector4[] dirLightWSDirections = new Vector4[MAX_DIR_LIGHT_COUNT];

        public void Setup(ScriptableRenderContext context, CullingResults cullingResults) {
            this.cullingResults = cullingResults;
            
            cmdBuffer.BeginSample(ProfileName);
            SetLights();
            cmdBuffer.EndSample(ProfileName);
            
            context.ExecuteCommandBuffer(cmdBuffer);
            cmdBuffer.Clear();
        }

        private void SetLights() {
            NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
            int dirLightCount = 0;
            for (int i = 0; i < visibleLights.Length; ++i) {
                VisibleLight curVisibleLight = visibleLights[i];
                if (curVisibleLight.lightType == LightType.Directional) {
                    SetupDirectionalLights(dirLightCount ++, ref curVisibleLight);
                    if (dirLightCount >= MAX_DIR_LIGHT_COUNT) {
                        break;
                    }
                }
            }
            
            cmdBuffer.SetGlobalInt(dirLightCountId, dirLightCount);
            cmdBuffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
            cmdBuffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightWSDirections);
        }

        private void SetupDirectionalLights(int index, ref VisibleLight visibleLight) {
            // finalColor = light.color.linear * light.intensity;
            dirLightColors[index] = visibleLight.finalColor;
            // https://www.zhihu.com/people/kmac-3/answers
            // 右乘，矩阵每一列都是转换后坐标的基向量，所以这里矩阵第3列就是forward, 世界坐标的forward
            dirLightWSDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
            
            // Light light = RenderSettings.sun;
            // cmdBuffer.SetGlobalVector(dirLightColorId, light.color.linear * light.intensity);
            // cmdBuffer.SetGlobalVector(dirLightDirectionId, -light.transform.forward);
        }
    }
}
