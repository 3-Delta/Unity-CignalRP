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
        
        public static readonly int dirLightShadowAtlasId = Shader.PropertyToID("_DirectionalLightShadowAtlas");

        // 最大投射阴影的平行光数量
        public const int MAX_SHADOW_DIRECTIONAL_LIGHT_COUNT = 1;

        private int shadowedDirectionalLightCount = 0;
        private ShadowedDirectionalLight[] shadowedDirectionalLights = new ShadowedDirectionalLight[MAX_SHADOW_DIRECTIONAL_LIGHT_COUNT];
        
        public void Setup(ref ScriptableRenderContext context, ref CullingResults cullingResults, ShadowSettings shadowSettings) {
            this.context = context;
            this.cullingResults = cullingResults;
            this.shadowSettings = shadowSettings;
            
            shadowedDirectionalLightCount = 0;
        }

        public void Render() {
            if (shadowedDirectionalLightCount > 0) {
                RenderDirectionalShadow(ref context, ref cullingResults, shadowSettings);
            }
            else {
                // https://catlikecoding.com/unity/tutorials/custom-srp/directional-shadows/
                // 为什么还需要申请dummy的shadowmap呢？
                // 在webgl2.0情况下，如果material不提供纹理的话，会失败
                cmdBuffer.GetTemporaryRT(dirLightShadowAtlasId, 1, 1, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
            }
        }

        private void RenderDirectionalShadow(ref ScriptableRenderContext context, ref CullingResults cullingResults, ShadowSettings shadowSettings) {
            int atlasSize = (int)shadowSettings.directionalShadow.shadowMapAtlasSize;
            cmdBuffer.GetTemporaryRT(dirLightShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
            cmdBuffer.SetRenderTarget(dirLightShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            // 因为是shadowmap,所以只需要clearDepth
            cmdBuffer.ClearRenderTarget(true, false, Color.clear);
            
            cmdBuffer.BeginSample(ProfileName);
            CameraRenderer.ExecuteCmdBuffer(ref context, cmdBuffer);
            
            for (int i = 0; i < shadowedDirectionalLightCount; ++i) {
                RenderDirectionalShadow(ref context, ref cullingResults, shadowSettings, i, atlasSize);
            }
            
            cmdBuffer.EndSample(ProfileName);
            CameraRenderer.ExecuteCmdBuffer(ref context, cmdBuffer);
        }
        
        private void RenderDirectionalShadow(ref ScriptableRenderContext context, ref CullingResults cullingResults, ShadowSettings shadowSettings, int index, int tileSize) {
            var light = shadowedDirectionalLights[index];
            
            var shadowDrawSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex);
            cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(light.visibleLightIndex, 0, 1, Vector3.zero, tileSize, 0f, 
                out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData splitData);
            shadowDrawSettings.splitData = splitData;
            cmdBuffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
            CameraRenderer.ExecuteCmdBuffer(ref context, cmdBuffer);
            context.DrawShadows(ref shadowDrawSettings);
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
