using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

namespace CignalRP {
    public class Lighting {
        public const string ProfileName = "CRP|Lighting";

        private CommandBuffer cmdBuffer = new CommandBuffer() {
            name = ProfileName
        };

        private ScriptableRenderContext context;
        private CullingResults cullingResults;

        public const int MAX_DIR_LIGHT_COUNT = 4;
        public static readonly int dirLightCountId = Shader.PropertyToID("_DirectionalLightCount");
        
        public static readonly int dirLightColorsId = Shader.PropertyToID("_DirectionalLightColors");
        public static readonly Vector4[] dirLightColors = new Vector4[MAX_DIR_LIGHT_COUNT];
        
        public static readonly int dirLightDirectionsId = Shader.PropertyToID("_DirectionalLightWSDirections");
        public static readonly Vector4[] dirLightWSDirections = new Vector4[MAX_DIR_LIGHT_COUNT];
        
        public static readonly int dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");
        public static readonly Vector4[] dirLightShadowData = new Vector4[MAX_DIR_LIGHT_COUNT];
        
        public const int MAX_OTHER_LIGHT_COUNT = 64;
        public static readonly int otherLightCountId = Shader.PropertyToID("_OtherLightCount");
        
        public static readonly int otherLightColorsId = Shader.PropertyToID("_OtherLightColors");
        public static readonly Vector4[] otherLightColors = new Vector4[MAX_OTHER_LIGHT_COUNT];
        
        public static readonly int otherLightPositionsId = Shader.PropertyToID("_OtherLightWSPositions");
        public static readonly Vector4[] otherLightWSPositions = new Vector4[MAX_OTHER_LIGHT_COUNT];
        
        // 聚光灯
        public static readonly int otherLightDirectionsId = Shader.PropertyToID("_OtherLightWSDirections");
        public static readonly Vector4[] otherLightDirections = new Vector4[MAX_OTHER_LIGHT_COUNT];
        
        public static readonly int otherLightSpotAnglesId = Shader.PropertyToID("_OtherLightSpotAngles");
        public static readonly Vector4[] otherLightSpotAngles = new Vector4[MAX_OTHER_LIGHT_COUNT];

        private Shadow shadow = new Shadow();

        public void Setup(ref ScriptableRenderContext context, ref CullingResults cullingResults, ShadowSettings shadowSettings) {
            this.context = context;
            this.cullingResults = cullingResults;

            cmdBuffer.BeginSample(ProfileName);

            shadow.Setup(ref context, ref cullingResults, shadowSettings);
            SetLights();
            shadow.Render();

            cmdBuffer.EndSample(ProfileName);

            CameraRenderer.ExecuteCmdBuffer(ref context, cmdBuffer);
        }

        public void Clean() {
            shadow.Clean();
        }

        private void SetLights() {
            NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
            int dirLightCount = 0;
            int otherLightCount = 0;
            for (int i = 0; i < visibleLights.Length; ++i) {
                VisibleLight curVisibleLight = visibleLights[i];
                switch (curVisibleLight.lightType) {
                    case LightType.Directional: {
                        if (dirLightCount < MAX_DIR_LIGHT_COUNT) {
                            SetupDirectionalLights(dirLightCount++, ref curVisibleLight);
                        }
                    }
                        break;
                    case LightType.Point: {
                        if (otherLightCount < MAX_OTHER_LIGHT_COUNT) {
                            SetupPointLights(otherLightCount++, ref curVisibleLight);
                        }
                    }
                        break;
                    case LightType.Spot:
                        if (otherLightCount < MAX_OTHER_LIGHT_COUNT) {
                            SetupSpotLights(otherLightCount++, ref curVisibleLight);
                        }
                        break;
                }
            }

            cmdBuffer.SetGlobalInt(dirLightCountId, dirLightCount);
            if (dirLightCount > 0) {
                cmdBuffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
                cmdBuffer.SetGlobalVectorArray(dirLightDirectionsId, dirLightWSDirections);
                cmdBuffer.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData);
            }

            cmdBuffer.SetGlobalInt(otherLightCountId, otherLightCount);
            if (otherLightCount > 0) {
                cmdBuffer.SetGlobalVectorArray(otherLightColorsId, otherLightColors);
                cmdBuffer.SetGlobalVectorArray(otherLightPositionsId, otherLightWSPositions);
                cmdBuffer.SetGlobalVectorArray(otherLightDirectionsId, otherLightDirections);
                cmdBuffer.SetGlobalVectorArray(otherLightSpotAnglesId, otherLightSpotAngles);
            }
        }

        // 点光，无位置，有方向
        private void SetupDirectionalLights(int index, ref VisibleLight visibleLight) {
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
            dirLightShadowData[index] = shadow.ReserveDirectionalShadows(visibleLight.light, index);

            // Light light = RenderSettings.sun;
            // cmdBuffer.SetGlobalVector(dirLightColorId, light.color.linear * light.intensity);
            // cmdBuffer.SetGlobalVector(dirLightDirectionId, -light.transform.forward);
        }

        // 点光源，有位置，无方向
        private void SetupPointLights(int index, ref VisibleLight visibleLight) {
            otherLightColors[index] = visibleLight.finalColor;
            // 第4列是pos
            Vector4 pos = visibleLight.localToWorldMatrix.GetColumn(3);
            // 限制点光范围，同时不在边界突然消失，而是fade
            pos.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
            otherLightWSPositions[index] = pos;
            
            // 避免受到shader中计算spot的衰减受到影响
            otherLightSpotAngles[index] = new Vector4(0f, 1f);
        }

        // 聚光灯 有位置，有方向
        private void SetupSpotLights(int index, ref VisibleLight visibleLight) {
            otherLightColors[index] = visibleLight.finalColor;
            otherLightDirections[index] = -visibleLight.localToWorldMatrix.GetColumn(2);
            
            // 第4列是pos
            Vector4 pos = visibleLight.localToWorldMatrix.GetColumn(3);
            // 限制点光范围，同时不在边界突然消失，而是fade
            pos.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
            otherLightWSPositions[index] = pos;

            // 角度范围
            Light light = visibleLight.light;
            float innerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.innerSpotAngle);
            float outerCos = Mathf.Cos(Mathf.Deg2Rad * 0.5f * light.spotAngle);
            float angleRangeInv = 1f / Mathf.Max(innerCos - outerCos, 0.001f);
            otherLightSpotAngles[index] = new Vector4(angleRangeInv, -outerCos * angleRangeInv);
        }
    }
}
