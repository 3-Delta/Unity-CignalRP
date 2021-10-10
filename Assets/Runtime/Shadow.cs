using System;
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

        private int shadowedDirectionalLightCount = 0;
        private ShadowedDirectionalLight[] shadowedDirectionalLights = new ShadowedDirectionalLight[MAX_SHADOW_DIRECTIONAL_LIGHT_COUNT];
        
        public static readonly int dirLightShadowAtlasId = Shader.PropertyToID("_DirectionalLightShadowAtlas");
        
        private static readonly int dirShadowMatricesId = Shader.PropertyToID("_DirectionalShadowLightMatrices");
        private static readonly Matrix4x4[] dirShadowMatrices = new Matrix4x4[MAX_SHADOW_DIRECTIONAL_LIGHT_COUNT];
        
        public void Setup(ref ScriptableRenderContext context, ref CullingResults cullingResults, ShadowSettings shadowSettings) {
            this.context = context;
            this.cullingResults = cullingResults;
            this.shadowSettings = shadowSettings;
            
            shadowedDirectionalLightCount = 0;
        }

        public void Render() {
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

            int tileCount = shadowedDirectionalLightCount;
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
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(light.visibleLightIndex, 0, 1, Vector3.zero, tileSize, 0f, 
                out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData splitData);
            shadowDrawSettings.splitData = splitData;
            
            int tileIndex = lightIndex;
            Vector2 viewport = SetTileViewport(tileIndex, countPerLine, tileSize);
            // 得到world->light的矩阵， 此时camera在light位置
            dirShadowMatrices[lightIndex] = ConvertToAtlasMatrix(projMatrix * viewMatrix, viewport, countPerLine);
            cmdBuffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
            CameraRenderer.ExecuteCmdBuffer(ref context, cmdBuffer);
            context.DrawShadows(ref shadowDrawSettings);
        }

        // vp矩阵将positionWS转换到ndc中， 这个矩阵将positionWS转换到size=1的CUBE区域中的某个tile块中
        // 也可以理解为转换到shadowspace
        private Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 vpMatrix, Vector2 offset, int countPerLine) {
            // matrix是列优先
            if (SystemInfo.usesReversedZBuffer) {
                // todo 为什么这么设计？
                vpMatrix.m20 = -vpMatrix.m20;
                vpMatrix.m21 = -vpMatrix.m21;
                vpMatrix.m22 = -vpMatrix.m22;
                vpMatrix.m23 = -vpMatrix.m23;
            }
            
            return vpMatrix;
        }

        private Vector2 SetTileViewport (int index, int countPerLine, float tileSize) {
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

        public void ReserveDirectionalShadows(Light light, int visibleLightIndex) {
            if (shadowedDirectionalLightCount < MAX_SHADOW_DIRECTIONAL_LIGHT_COUNT &&
                light.shadows != LightShadows.None && light.shadowStrength > 0f &&
                cullingResults.GetShadowCasterBounds(visibleLightIndex, out Bounds bounds)) {
                // 光源设置为投射阴影，但是没有物件接收阴影，不需要shadowmap
                shadowedDirectionalLights[shadowedDirectionalLightCount++] = new ShadowedDirectionalLight() {
                    visibleLightIndex = visibleLightIndex,
                };
            }
        }
    }
}
