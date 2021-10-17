﻿using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace CignalRP {
    public class Shadow {
        public struct ShadowedDirectionalLight {
            public int visibleLightIndex;
        }

        public const string ProfileName = "Shadow";

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

        public void Setup(ref ScriptableRenderContext context, ref CullingResults cullingResults, ShadowSettings shadowSettings) {
            this.context = context;
            this.cullingResults = cullingResults;
            this.shadowSettings = shadowSettings;

            shadowedDirectionalLightCount = 0;
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
        }

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

            cmdBuffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);

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
                    tileSize, 0f,
                    out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData splitData);

                shadowDrawSettings.splitData = splitData;
                int tileIndex = startTileIndexOfThisLight + i;
                Vector2 viewport = SetTileViewport(tileIndex, countPerLine, tileSize);
                // 得到world->light的矩阵， 此时camera在light位置
                dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(projMatrix, viewMatrix, viewport, countPerLine);
                cmdBuffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
                CameraRenderer.ExecuteCmdBuffer(ref context, cmdBuffer);
                context.DrawShadows(ref shadowDrawSettings);
            }
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

        public void Clean() {
            cmdBuffer.ReleaseTemporaryRT(dirLightShadowAtlasId);
            CameraRenderer.ExecuteCmdBuffer(ref context, cmdBuffer);
        }

        public Vector2 ReserveDirectionalShadows(Light light, int visibleLightIndex) {
            if (shadowedDirectionalLightCount < MAX_SHADOW_DIRECTIONAL_LIGHT_COUNT &&
                light.shadows != LightShadows.None && light.shadowStrength > 0f &&
                cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds bounds)) {
                // 光源设置为投射阴影，但是没有物件接收阴影，不需要shadowmap
                shadowedDirectionalLights[shadowedDirectionalLightCount] = new ShadowedDirectionalLight() {
                    visibleLightIndex = visibleLightIndex,
                };

                return new Vector2(light.shadowStrength, shadowSettings.directionalShadow.cascadeCount * shadowedDirectionalLightCount++);
            }

            return Vector2.zero;
        }
    }
}
