using System;
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

        public static readonly int dirLightDirectionsAndMaskId = Shader.PropertyToID("_DirectionalLightWSDirectionsAndMasks");
        public static readonly Vector4[] dirLightWSDirectionsAndMask = new Vector4[MAX_DIR_LIGHT_COUNT];

        public static readonly int dirLightShadowDataId = Shader.PropertyToID("_DirectionalLightShadowData");
        public static readonly Vector4[] dirLightShadowData = new Vector4[MAX_DIR_LIGHT_COUNT];

        public const int MAX_OTHER_LIGHT_COUNT = 64;
        public static readonly int otherLightCountId = Shader.PropertyToID("_OtherLightCount");

        public static readonly int otherLightColorsId = Shader.PropertyToID("_OtherLightColors");
        public static readonly Vector4[] otherLightColors = new Vector4[MAX_OTHER_LIGHT_COUNT];

        public static readonly int otherLightPositionsId = Shader.PropertyToID("_OtherLightWSPositions");
        public static readonly Vector4[] otherLightWSPositions = new Vector4[MAX_OTHER_LIGHT_COUNT];

        // 聚光灯
        public static readonly int otherLightDirectionsAndMaskId = Shader.PropertyToID("_OtherLightWSDirectionsAndMasks");
        public static readonly Vector4[] otherLightDirectionsAndMask = new Vector4[MAX_OTHER_LIGHT_COUNT];

        public static readonly int otherLightSpotAnglesId = Shader.PropertyToID("_OtherLightSpotAngles");
        public static readonly Vector4[] otherLightSpotAngles = new Vector4[MAX_OTHER_LIGHT_COUNT];

        public static readonly int otherLightShadowDataId = Shader.PropertyToID("_OtherLightShadowData");
        public static readonly Vector4[] otherLightShadowData = new Vector4[MAX_OTHER_LIGHT_COUNT];

        public static readonly string lightsPerObjectKeyword = "_LIGHTS_PER_OBJECT";

        private Shadow shadow = new Shadow();

        public void Setup(ref ScriptableRenderContext context, ref CullingResults cullingResults, ShadowSettings shadowSettings,
            bool usePerObjectLights, int cameraRenderingLayerMask) {
            this.context = context;
            this.cullingResults = cullingResults;

            CmdBufferExt.ProfileSample(ref context, cmdBuffer, EProfileStep.Begin, ProfileName, false);

            shadow.Setup(ref context, ref cullingResults, shadowSettings);
            SetLights(usePerObjectLights, cameraRenderingLayerMask);
            shadow.Render();

            CmdBufferExt.ProfileSample(ref context, cmdBuffer, EProfileStep.End, ProfileName);
        }

        public void Clean() {
            shadow.Clean();
        }

        private void SetLights(bool usePerObjectLights, int cameraRenderingLayerMask) {
            NativeArray<int> indexMap = usePerObjectLights ? cullingResults.GetLightIndexMap(Allocator.Temp) : default;
            NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
            int dirLightCount = 0;
            int otherLightCount = 0;
            int i = 0;
            for (; i < visibleLights.Length; ++i) {
                int newIndex = -1;
                VisibleLight curVisibleLight = visibleLights[i];
                int mask = curVisibleLight.light.renderingLayerMask & cameraRenderingLayerMask;
                if (mask != 0) {
                    switch (curVisibleLight.lightType) {
                        case LightType.Directional: {
                            if (dirLightCount < MAX_DIR_LIGHT_COUNT) {
                                SetupDirectionalLights(dirLightCount++, i, ref curVisibleLight);
                            }
                        }
                            break;
                        case LightType.Point: {
                            if (otherLightCount < MAX_OTHER_LIGHT_COUNT) {
                                newIndex = otherLightCount;
                                SetupPointLights(otherLightCount++, i, ref curVisibleLight);
                            }
                        }
                            break;
                        case LightType.Spot:
                            if (otherLightCount < MAX_OTHER_LIGHT_COUNT) {
                                newIndex = otherLightCount;
                                SetupSpotLights(otherLightCount++, i, ref curVisibleLight);
                            }

                            break;
                    }
                }

                if (usePerObjectLights) {
                    indexMap[i] = newIndex;
                }
            }

            if (usePerObjectLights) {
                // cullingResults.visibleLights只是可见光源,还有不可见光源,这里剔除
                for (; i < indexMap.Length; i++) {
                    indexMap[i] = -1;
                }

                cullingResults.SetLightIndexMap(indexMap);
                Shader.EnableKeyword(lightsPerObjectKeyword);
                indexMap.Dispose();
            }
            else {
                Shader.DisableKeyword(lightsPerObjectKeyword);
            }

            cmdBuffer.SetGlobalInt(dirLightCountId, dirLightCount);
            if (dirLightCount > 0) {
                cmdBuffer.SetGlobalVectorArray(dirLightColorsId, dirLightColors);
                cmdBuffer.SetGlobalVectorArray(dirLightDirectionsAndMaskId, dirLightWSDirectionsAndMask);
                cmdBuffer.SetGlobalVectorArray(dirLightShadowDataId, dirLightShadowData);
            }

            cmdBuffer.SetGlobalInt(otherLightCountId, otherLightCount);
            if (otherLightCount > 0) {
                cmdBuffer.SetGlobalVectorArray(otherLightColorsId, otherLightColors);
                cmdBuffer.SetGlobalVectorArray(otherLightPositionsId, otherLightWSPositions);
                cmdBuffer.SetGlobalVectorArray(otherLightDirectionsAndMaskId, otherLightDirectionsAndMask);
                cmdBuffer.SetGlobalVectorArray(otherLightSpotAnglesId, otherLightSpotAngles);
                cmdBuffer.SetGlobalVectorArray(otherLightShadowDataId, otherLightShadowData);
            }
        }

        // 点光，无位置，有方向
        private void SetupDirectionalLights(int index, int visibleIndex, ref VisibleLight visibleLight) {
            // light.color.linear其实就意味着，inspector中的颜色被看作是gamma颜色 https://zhuanlan.zhihu.com/p/163944531 [sRGB]
            // shader的inspector如果想将颜色看作linear, 需要添加[Gamma],这样unity就会做类似将sRGB纹理转成linear的操作
            // finalColor = light.color.linear * light.intensity;
            dirLightColors[index] = visibleLight.finalColor;
            // https://www.zhihu.com/people/kmac-3/answers
            // 右乘，矩阵每一列都是转换后坐标的基向量，所以这里矩阵第3列就是forward, 世界坐标的forward
            // https://zhuanlan.zhihu.com/p/163360207
            // https://www.zhihu.com/question/452040005/answer/1810783856
            // 最后一列代表的是模型中心点的世界坐标
            // localspace的光源的forward方向
            // 方向：指向光源的方向，所以是负数
            var dirAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
            
            // 因为light的cullmask支持不完善，所以urp使用renderelayermask代替。
            // light的cullmask只会影响shadow, 不像camera那样，会影响到物体的颜色表现和投射,接收阴影
            dirAndMask.w = visibleLight.light.renderingLayerMask.ToFloat();
            dirLightWSDirectionsAndMask[index] = dirAndMask;

            // 保留Light阴影设置数据，得到可投射shadow的light数据
            // index是dirLightWSDirections的下标
            dirLightShadowData[index] = shadow.ReserveDirectionalShadows(visibleLight.light, visibleIndex);

            // Light light = RenderSettings.sun;
            // cmdBuffer.SetGlobalVector(dirLightColorId, light.color.linear * light.intensity);
            // cmdBuffer.SetGlobalVector(dirLightDirectionId, -light.transform.forward);

            // 没有位置， 角度， 衰减， 只有方向
        }

        // 点光源，有位置，无方向
        private void SetupPointLights(int index, int visibleIndex, ref VisibleLight visibleLight) {
            otherLightColors[index] = visibleLight.finalColor;
            // 第4列是pos
            Vector4 pos = visibleLight.localToWorldMatrix.GetColumn(3);
            // 限制点光范围，同时不在边界突然消失，而是fade
            pos.w = 1f / Mathf.Max(visibleLight.range * visibleLight.range, 0.00001f);
            otherLightWSPositions[index] = pos;

            // 避免受到shader中计算spot的衰减受到影响
            otherLightSpotAngles[index] = new Vector4(0f, 1f);

            var dirAndMask = Vector4.zero;
            dirAndMask.w = visibleLight.light.renderingLayerMask.ToFloat();
            otherLightDirectionsAndMask[index] = dirAndMask;

            Light light = visibleLight.light;
            otherLightShadowData[index] = shadow.ReserveOtherShadow(light, visibleIndex);
        }

        // 聚光灯 有位置，有方向
        private void SetupSpotLights(int index, int visibleIndex, ref VisibleLight visibleLight) {
            otherLightColors[index] = visibleLight.finalColor;
            var dirAndMask = -visibleLight.localToWorldMatrix.GetColumn(2);
            dirAndMask.w = visibleLight.light.renderingLayerMask.ToFloat();
            otherLightDirectionsAndMask[index] = dirAndMask;

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

            otherLightShadowData[index] = shadow.ReserveOtherShadow(light, visibleIndex);
        }
    }
}
