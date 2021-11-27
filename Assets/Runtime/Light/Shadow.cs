using System;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace CignalRP {
    public class Shadow {
        public struct ShadowedDirectionalLight {
            public int visibleLightIndex;

            // 斜度比例偏差值
            public float slopeScaleBias;

            // 光源近裁剪
            public float nearPlaneOffset;
        }

        public const string ProfileName = "CRP|Shadow";

        private CommandBuffer cmdBuffer = new CommandBuffer() {
            name = ProfileName
        };

        private ScriptableRenderContext context;
        private CullingResults cullingResults;
        private ShadowSettings shadowSettings;

        // 最大投射阴影的平行光数量
        public const int MAX_SHADOW_DIRECTIONAL_LIGHT_COUNT = 4;
        public const int MAX_CASCADE_COUNT = 4;

        private int shadowedDirectionalLightCount = 0;
        private ShadowedDirectionalLight[] shadowedDirectionalLights = new ShadowedDirectionalLight[MAX_SHADOW_DIRECTIONAL_LIGHT_COUNT];

        private static readonly int dirLightShadowAtlasId = Shader.PropertyToID("_DirectionalLightShadowAtlas");

        private static readonly int dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowLightMatrices");
        private static readonly Matrix4x4[] dirShadowMatrices = new Matrix4x4[MAX_SHADOW_DIRECTIONAL_LIGHT_COUNT * MAX_CASCADE_COUNT];

        private static readonly int shadowDistanceVSadeId = Shader.PropertyToID("_ShadowDistanceVSFade");

        // https://edu.uwa4d.com/lesson-detail/282/1311/0?isPreview=0
        // 相机视锥体的各个裁剪球,因为是球体,所以针对一个camera来说,每个light的裁剪球是一样的,因为是球体,而不是正方形之类的.
        private static readonly int cascadeCountId = Shader.PropertyToID("_CascadeCount");
        private static readonly int cascadeCullingSpheresId = Shader.PropertyToID("_CascadeCullingSpheres");
        private static readonly int cascadeDataId = Shader.PropertyToID("_CascadeData");

        private static Vector4[] cascadeCullingSpheres = new Vector4[MAX_CASCADE_COUNT];
        private static Vector4[] cascadeData = new Vector4[MAX_CASCADE_COUNT];

        private static string[] directionalFilterKeywords = {
            "_Directional_PCF3",
            "_Directional_PCF5",
            "_Directional_PCF7",
        };

        private static int shadowAtlasSizeId = Shader.PropertyToID("_ShadowAtlasSize");

        private bool useShadowMask = false;

        private static string[] shadowMaskKeywords = {
            "_SHADOW_MASK_ALWAYS", // 会将静态物体的实时阴影替换成bakedshadow
            "_SHADOW_MASK_DISTANCE",
        };

        public void Setup(ref ScriptableRenderContext context, ref CullingResults cullingResults, ShadowSettings shadowSettings) {
            this.context = context;
            this.cullingResults = cullingResults;
            this.shadowSettings = shadowSettings;

            shadowedDirectionalLightCount = 0;
            useShadowMask = false;
        }

        public void Render() {
            // 光源不符合shadow的时候,不渲染shadowmap
            if (shadowedDirectionalLightCount > 0) {
                RenderDirectionalShadow();
            }
            else {
                // https://catlikecoding.com/unity/tutorials/custom-srp/directional-shadows/
                // 为什么还需要申请dummy的shadowmap呢？
                // 在webgl2.0情况下，如果material不提供纹理的话，会失败
                cmdBuffer.GetTemporaryRT(dirLightShadowAtlasId, 1, 1, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
            }

            cmdBuffer.BeginSample(ProfileName);
            int shadowMaskIndex = -1;
            if (useShadowMask && shadowSettings.useShadowMask) {
                // shadowMaskIndex最终由光源和QualitySettings.shadowmaskMode一起决定
                shadowMaskIndex = QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1;
            }

            SetKeywords(shadowMaskKeywords, shadowMaskIndex);

            cmdBuffer.EndSample(ProfileName);
            CameraRenderer.ExecuteCmdBuffer(ref context, cmdBuffer);
        }

        // 只有平行光由shadowmap,其他光源没有shadowmap
        private void RenderDirectionalShadow() {
            int atlasSize = (int)shadowSettings.directionalShadow.shadowMapAtlasSize;
            cmdBuffer.GetTemporaryRT(dirLightShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
            cmdBuffer.SetRenderTarget(dirLightShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            // 因为是shadowmap,所以只需要clearDepth
            cmdBuffer.ClearRenderTarget(true, false, Color.clear);

            cmdBuffer.BeginSample(ProfileName);
            CameraRenderer.ExecuteCmdBuffer(ref context, cmdBuffer);

            int tileCount = shadowedDirectionalLightCount * shadowSettings.directionalShadow.cascadeCount;
            // atlas中每行几个
            // 比如2light * 3cascade, 还是atlas中每行4个
            int countPerLine = tileCount <= 1 ? 1 : tileCount <= 4 ? 2 : 4;
            int tileSize = atlasSize / countPerLine;
            for (int i = 0; i < shadowedDirectionalLightCount; ++i) {
                RenderDirectionalShadow(i, countPerLine, tileSize);
            }

            cmdBuffer.SetGlobalInt(cascadeCountId, shadowSettings.directionalShadow.cascadeCount);
            cmdBuffer.SetGlobalVectorArray(cascadeCullingSpheresId, cascadeCullingSpheres);
            cmdBuffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
            cmdBuffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);

            float cascadeFade = 1f - shadowSettings.directionalShadow.cascadeFade;
            cmdBuffer.SetGlobalVector(shadowDistanceVSadeId, new Vector4(1f / shadowSettings.maxShadowVSDistance, 1f / shadowSettings.distanceFade, 1f / (1f - cascadeFade * cascadeFade)));

            SetKeywords(directionalFilterKeywords, (int)(shadowSettings.directionalShadow.filterMode) - 1);
            cmdBuffer.SetGlobalVector(shadowAtlasSizeId, new Vector4(atlasSize, 1f / atlasSize));
            cmdBuffer.EndSample(ProfileName);
            CameraRenderer.ExecuteCmdBuffer(ref context, cmdBuffer);
        }

        private void RenderDirectionalShadow(int lightIndex, int countPerLine, int tileSize) {
            var light = shadowedDirectionalLights[lightIndex];
            var shadowDrawSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
            int cascadeCount = shadowSettings.directionalShadow.cascadeCount;
            int startTileIndexOfThisLight = lightIndex * cascadeCount;
            Vector3 ratios = shadowSettings.directionalShadow.cascadeRatios;

            for (int i = 0; i < cascadeCount; ++i) {
                cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(light.visibleLightIndex, i, cascadeCount, ratios,
                    tileSize, light.nearPlaneOffset,
                    out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData splitData);

                if (lightIndex == 0) {
                    SetCascadeData(i, splitData.cullingSphere, tileSize);
                }

                shadowDrawSettings.splitData = splitData;
                int tileIndex = startTileIndexOfThisLight + i;
                Vector2 viewport = SetTileViewport(tileIndex, countPerLine, tileSize);
                // 得到world->light的矩阵， 此时camera在light位置
                dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(projMatrix, viewMatrix, viewport, countPerLine);
                cmdBuffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
                cmdBuffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
                CameraRenderer.ExecuteCmdBuffer(ref context, cmdBuffer);
                context.DrawShadows(ref shadowDrawSettings);

                // 还原
                cmdBuffer.SetGlobalDepthBias(0f, 0f);
            }
        }

        private void SetCascadeData(int cascadeIndex, Vector4 cullingSphere, float tileSize) {
            // 包围球直径和shadowmap的tilesize的关系
            // 4.4.3 normalbias: ws中一个texel大小就足够了
            float texelSize = 2f * cullingSphere.w / tileSize;

            // pcf滤波范围和normalbias适配
            float pcfFilterSize = texelSize * ((float)shadowSettings.directionalShadow.filterMode + 1f);
            cullingSphere.w -= pcfFilterSize;

            cullingSphere.w *= cullingSphere.w;
            cascadeCullingSpheres[cascadeIndex] = cullingSphere;

            cascadeData[cascadeIndex] = new Vector4(1f / cullingSphere.w, pcfFilterSize * 1.4142136f);
        }

        // vp矩阵将positionWS转换到ndc中， 这个矩阵将positionWS转换到size=1的CUBE区域中的某个tile块中
        // 也可以理解为转换到shadowspace
        private Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 projMatrix, Matrix4x4 viewMatrix, Vector2 offset, int countPerLine) {
            Matrix4x4 worldToShadow = GetShadowTransform(projMatrix, viewMatrix);

            Matrix4x4 sliceTransform = Matrix4x4.identity;
            // 因为shadowmap都是矩形,不存在长方形
            float scale = 1.0f / countPerLine;
            // 缩放, 将[0, 1]的立方体控制为[0, scale]的立方体
            sliceTransform.m00 = scale;
            sliceTransform.m11 = scale;

            // 平移
            sliceTransform.m03 = offset.x * scale;
            sliceTransform.m13 = offset.y * scale;

            return sliceTransform * worldToShadow;
        }

        // 将[-1, 1]的立方体转换为[0, 1]的立方体
        public static Matrix4x4 GetShadowTransform(Matrix4x4 projMatrix, Matrix4x4 viewMatrix) {
            // matrix是列优先
            // Currently CullResults ComputeDirectionalShadowMatricesAndCullingPrimitives doesn't
            // apply z reversal to projection matrix. We need to do it manually here.
            if (SystemInfo.usesReversedZBuffer) {
                projMatrix.m20 = -projMatrix.m20;
                projMatrix.m21 = -projMatrix.m21;
                projMatrix.m22 = -projMatrix.m22;
                projMatrix.m23 = -projMatrix.m23;
            }

            Matrix4x4 worldToShadow = projMatrix * viewMatrix;

            var textureScaleAndBias = Matrix4x4.identity;
            // 控制缩放 [-1, 1]的立方体 --> [-0.5, 0.5]的立方体,中心点为[0, 0, 0]
            textureScaleAndBias.m00 = 0.5f;
            textureScaleAndBias.m11 = 0.5f;
            textureScaleAndBias.m22 = 0.5f;

            // 控制平移 将左下角点位置[-0.5, -0.5, -0.5] 平移到 [0, 0, 0]处
            textureScaleAndBias.m03 = 0.5f;
            textureScaleAndBias.m13 = 0.5f;
            textureScaleAndBias.m23 = 0.5f;

            // Apply texture scale and offset to save a MAD in shader.
            return textureScaleAndBias * worldToShadow;
        }

        private Vector2 SetTileViewport(int index, int countPerLine, float tileSize) {
            // 二维数组的行列
            int row = index / countPerLine;
            int col = index % countPerLine;
            Vector2 offset = new Vector2(col, row);

            // 这个结果计算出来应该是旋转90的吧！！！
            // qustion??? 这个结果计算出来应该是旋转90的吧！！！
            cmdBuffer.SetViewport(new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize));
            return offset;
        }

        private void SetKeywords(string[] keywords, int enabledIndex) {
            for (int i = 0; i < keywords.Length; i++) {
                if (i == enabledIndex) {
                    cmdBuffer.EnableShaderKeyword(keywords[i]);
                }
                else {
                    cmdBuffer.DisableShaderKeyword(keywords[i]);
                }
            }
        }

        public void Clean() {
            cmdBuffer.ReleaseTemporaryRT(dirLightShadowAtlasId);
            CameraRenderer.ExecuteCmdBuffer(ref context, cmdBuffer);
        }

        public Vector3 ReserveDirectionalShadows(Light light, int visibleLightIndex) {
            if (shadowedDirectionalLightCount < MAX_SHADOW_DIRECTIONAL_LIGHT_COUNT &&
                light.shadows != LightShadows.None && light.shadowStrength > 0f) {
                
                int shadowMaskChannel = -1;
                LightBakingOutput lbo = light.bakingOutput;
                if (lbo.lightmapBakeType == LightmapBakeType.Mixed && lbo.mixedLightingMode == MixedLightingMode.Shadowmask) {
                    useShadowMask = true;
                    shadowMaskChannel = lbo.occlusionMaskChannel;
                }

                // 如果被裁剪,返回 -light.shadowStrength而不是, 正的-light.shadowStrength
                if (!cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds bounds)) {
                    return new Vector4(-light.shadowStrength, 0f, 0f, shadowMaskChannel);
                }

                // 光源设置为投射阴影，但是没有物件接收阴影，不需要shadowmap
                shadowedDirectionalLights[shadowedDirectionalLightCount] = new ShadowedDirectionalLight() {
                    visibleLightIndex = visibleLightIndex,
                    slopeScaleBias = light.shadowBias,
                    // https://edu.uwa4d.com/lesson-detail/282/1311/0?isPreview=0
                    // 影响unity阴影平坠的shadowmap的形成
                    nearPlaneOffset = light.shadowNearPlane
                };

                return new Vector4(light.shadowStrength, shadowSettings.directionalShadow.cascadeCount * shadowedDirectionalLightCount++, light.shadowNormalBias, shadowMaskChannel);
            }

            return new Vector4(0f, 0f, 0f, -1f);
        }

        public Vector4 ReserveOtherShadow(Light light, int visibleLightIndex) {
            if (light.shadows != LightShadows.None && light.shadowStrength > 0f) {
                LightBakingOutput lbo = light.bakingOutput;
                if (lbo.lightmapBakeType == LightmapBakeType.Mixed && lbo.mixedLightingMode == MixedLightingMode.Shadowmask) {
                    useShadowMask = true;
                    return new Vector4(light.shadowStrength, 0f, 0f, lbo.occlusionMaskChannel);
                }
            }

            return new Vector4(0f, 0f, 0f, -1f);
        }
    }
}
