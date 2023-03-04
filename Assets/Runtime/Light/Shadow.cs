using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace CignalRP {
    public class Shadow {
        public struct ShadowedDirectionalLight {
            public int indexOfVisibleLights;

            // 斜度比例偏差值
            public float slopeScaleBias;

            // 光源近裁剪
            public float nearPlaneOffset;

#if UNITY_EDITOR
            public string lightName;
#endif
        }

        public struct ShadowedOtherLight {
            public int visibleLightIndex;
            public float slopeScaleBias;
            public float normalBias;
            public bool isPointLight;
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
        // 内部缓存，不传递给GPU
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
        private Vector4 atlasSizes;

        private bool useShadowMask = false;

        private static string[] shadowMaskKeywords = {
            "_SHADOW_MASK_ALWAYS", // 会将静态物体的实时阴影替换成bakedshadow
            "_SHADOW_MASK_DISTANCE",
        };

        public const int MAX_SHADOW_OTHER_LIGHT_COUNT = 16;
        private int shadowedOtherLightCount = 0;

        private static string[] otherFilterKeywords = {
            "_OTHER_PCF3",
            "_OTHER_PCF5",
            "_OTHER_PCF7",
        };

        // 阴影平坠适用于正交矩阵
        private static readonly int shadowPancakingId = Shader.PropertyToID("_ShadowPancaking");

        private static readonly int otherLightShadowAtlasId = Shader.PropertyToID("_OtherLightShadowAtlas");

        private static readonly int otherShadowMatricesId = Shader.PropertyToID("_OtherShadowLightMatrices");
        private static readonly Matrix4x4[] otherShadowMatrices = new Matrix4x4[MAX_SHADOW_OTHER_LIGHT_COUNT];

        private static readonly int otherShadowTilesId = Shader.PropertyToID("_OtherShadowTiles");
        private static readonly Vector4[] otherShadowTiles = new Vector4[MAX_SHADOW_OTHER_LIGHT_COUNT];

        private ShadowedOtherLight[] shadowedOtherLights = new ShadowedOtherLight[MAX_SHADOW_OTHER_LIGHT_COUNT];

        public void Setup(ref ScriptableRenderContext context, ref CullingResults cullingResults, ShadowSettings shadowSettings) {
            this.context = context;
            this.cullingResults = cullingResults;
            this.shadowSettings = shadowSettings;

            shadowedDirectionalLightCount = 0;
            shadowedOtherLightCount = 0;
            useShadowMask = false;
        }

        public void Clean() {
            cmdBuffer.ReleaseTemporaryRT(dirLightShadowAtlasId);
            if (shadowedOtherLightCount > 0) {
                cmdBuffer.ReleaseTemporaryRT(otherLightShadowAtlasId);
            }

            CmdBufferExt.Execute(ref context, cmdBuffer);
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

            if (shadowedOtherLightCount > 0) {
                RenderOtherShadow();
            }
            else {
                // 指向
                cmdBuffer.SetGlobalTexture(otherLightShadowAtlasId, dirLightShadowAtlasId);
            }
            
            CmdBufferExt.ProfileSample(ref context, cmdBuffer, EProfileStep.Begin, ProfileName, false);
            int shadowMaskIndex = -1;
            if (useShadowMask && shadowSettings.useShadowMask) {
                // shadowMaskIndex最终由光源和QualitySettings.shadowmaskMode一起决定
                shadowMaskIndex = QualitySettings.shadowmaskMode == ShadowmaskMode.Shadowmask ? 0 : 1;
            }

            SetKeywords(shadowMaskKeywords, shadowMaskIndex);

            cmdBuffer.SetGlobalInt(cascadeCountId, shadowedDirectionalLightCount > 0 ? shadowSettings.directionalShadow.cascadeCount : 0);
            float cascadeFade = 1f - shadowSettings.directionalShadow.cascadeFade;
            cmdBuffer.SetGlobalVector(shadowDistanceVSadeId, new Vector4(1f / shadowSettings.maxShadowVSDistance, 1f / shadowSettings.distanceFade, 1f / (1f - cascadeFade * cascadeFade)));

            cmdBuffer.SetGlobalVector(shadowAtlasSizeId, atlasSizes);

            CmdBufferExt.ProfileSample(ref context, cmdBuffer, EProfileStep.End, ProfileName);
        }

        // 只有平行光由shadowmap,其他光源没有shadowmap
        private void RenderDirectionalShadow() {
            int atlasSize = (int) shadowSettings.directionalShadow.shadowMapAtlasSize;
            cmdBuffer.GetTemporaryRT(dirLightShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
            // 给shadowmap设定RenderTarget, 然后正常渲染非shadowmap的时候需要重新切换RenderTarget到屏幕吗？
            cmdBuffer.SetRenderTarget(dirLightShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            // 因为是shadowmap,所以只需要clearDepth
            cmdBuffer.ClearRenderTarget(true, false, Color.clear);
            cmdBuffer.SetGlobalFloat(shadowPancakingId, 1f);
            CmdBufferExt.ProfileSample(ref context, cmdBuffer, EProfileStep.Begin, ProfileName);

            int tileCount = shadowedDirectionalLightCount * shadowSettings.directionalShadow.cascadeCount;
            // atlas中每行几个
            // 比如2light * 3cascade, 还是atlas中每行4个
            // https://edu.uwa4d.com/lesson-detail/282/1311/0?isPreview=0 因为unity源码支持最大的cascade是4 
            int countPerLine = tileCount <= 1 ? 1 : tileCount <= 4 ? 2 : 4;
            int tileSize = atlasSize / countPerLine;
            for (int i = 0; i < shadowedDirectionalLightCount; ++i) {
#if UNITY_EDITOR
                var light = shadowedDirectionalLights[i];
                string name = $"{i.ToString()} DirectionalLight -> {light.lightName}";

                // 貌似对于CmdBuffer底层的一些默认绘制，总是会和CmdBuffer.name挂钩，而不是和指定的profileName挂钩
                // 比如ClearRenderTarget,比如DrawShadow, 所以我们这里手动修改cmdBuffer.name， 然后后面再还原回去
                cmdBuffer.name = name;
                CmdBufferExt.ProfileSample(ref context, cmdBuffer, EProfileStep.Begin, ProfileName);
#endif
                RenderDirectionalShadow(i, countPerLine, tileSize);

#if UNITY_EDITOR
                // 还原 cmdBuffer.name
                cmdBuffer.name = ProfileName;
                CmdBufferExt.ProfileSample(ref context, cmdBuffer, EProfileStep.End, ProfileName);
#endif
            }

            cmdBuffer.SetGlobalVectorArray(cascadeCullingSpheresId, cascadeCullingSpheres);
            cmdBuffer.SetGlobalVectorArray(cascadeDataId, cascadeData);
            cmdBuffer.SetGlobalMatrixArray(dirShadowMatricesId, dirShadowMatrices);

            SetKeywords(directionalFilterKeywords, (int) (shadowSettings.directionalShadow.filterMode) - 1);

            atlasSizes.x = atlasSize;
            atlasSizes.y = 1f / atlasSize;

            CmdBufferExt.ProfileSample(ref context, cmdBuffer, EProfileStep.End, ProfileName);
        }

        private void RenderDirectionalShadow(int lightIndex, int countPerLine, int tileSize) {
            var light = shadowedDirectionalLights[lightIndex];
            var shadowDrawSettings = new ShadowDrawingSettings(cullingResults, light.indexOfVisibleLights) {
                // 让shadow也受layermask影响
                useRenderingLayerMaskTest = this.shadowSettings.useRenderingLayerMask,
            };
            int cascadeCount = shadowSettings.directionalShadow.cascadeCount;
            int startTileIndexOfThisLight = lightIndex * cascadeCount;
            Vector3 ratios = shadowSettings.directionalShadow.cascadeRatios;
            float tileScale = 1f / countPerLine;

            for (int i = 0; i < cascadeCount; ++i) {
                // 其实这个函数是计算当前camera的i级联子视锥体 和 当前light的裁剪球的交叉
                cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(light.indexOfVisibleLights, i, cascadeCount, ratios,
                    tileSize, light.nearPlaneOffset,
                    // 每个级联的矩阵都不一样， 两个light的同一个级联的矩阵也不一样
                    out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData splitData);

                // 所有相机全部使用同样的级联，所以也就是所有的相机使用同样的裁剪球
                // 其实也可以给每个相机设定不同的级联设置，类似每个camera有自己的renderScale
                if (lightIndex == 0) {
                    // 为什么传递裁剪球给GPU呢？因为如图SphereShadowBound.png所以，球体内部会包含一些在camera视锥体之外的范围，这就可能导致在视锥体之外还能看到阴影
                    // 这就需要shader中计算岗前片源是属于哪个cascade(逐个比对每个裁剪球半径), 这就需要片源着色器中判断某个片源是否在裁剪球之外
                    // 为什么使用裁剪球，而不是裁剪矩形，是因为相机经常旋转，这时候方形也会跟随变化，这就会导致生成shadowmap的时候，内容变化比较大，导致采样shadowmap的时候，相比较camera的上个旋转会突变
                    SetCascadeData(i, splitData.cullingSphere, tileSize);
                }

                shadowDrawSettings.splitData = splitData;
                int tileIndex = startTileIndexOfThisLight + i;
                // 设置viewPort, 如果不设置，则会全部绘制在屏幕上，叠在一起
                Vector2 viewport = SetTileViewport(tileIndex, countPerLine, tileSize);

                // 得到world->light的矩阵， 此时camera在light位置
                dirShadowMatrices[tileIndex] = ConvertToAtlasMatrix(projMatrix, viewMatrix, viewport, tileScale);

                cmdBuffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
                cmdBuffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
                CmdBufferExt.Execute(ref context, cmdBuffer);

#if UNITY_EDITOR
                string name = $"cascade {i.ToString()}";
                cmdBuffer.name = name;
                CmdBufferExt.ProfileSample(ref context, cmdBuffer, EProfileStep.Begin, ProfileName);
#endif
                // 只渲染lightmode为shadowcaster的pass
                context.DrawShadows(ref shadowDrawSettings);

#if UNITY_EDITOR
                // 还原 cmdBuffer.name
                cmdBuffer.name = ProfileName;
                CmdBufferExt.ProfileSample(ref context, cmdBuffer, EProfileStep.End, ProfileName);
#endif

                // 还原
                cmdBuffer.SetGlobalDepthBias(0f, 0f);
            }
        }

        private void RenderOtherShadow() {
            int atlasSize = (int) shadowSettings.otherShadow.shadowMapAtlasSize;
            atlasSizes.z = atlasSize;
            atlasSizes.w = 1f / atlasSize;

            cmdBuffer.GetTemporaryRT(otherLightShadowAtlasId, atlasSize, atlasSize, 32, FilterMode.Bilinear, RenderTextureFormat.Shadowmap);
            cmdBuffer.SetRenderTarget(otherLightShadowAtlasId, RenderBufferLoadAction.DontCare, RenderBufferStoreAction.Store);
            cmdBuffer.ClearRenderTarget(true, false, Color.clear);
            // shadowpancaking只在平行光影响，因为平行光阴影是虚拟正交相机
            cmdBuffer.SetGlobalFloat(shadowPancakingId, 0f);

            CmdBufferExt.ProfileSample(ref context, cmdBuffer, EProfileStep.Begin, ProfileName);

            int tileCount = shadowedOtherLightCount;
            int countPerLine = tileCount <= 1 ? 1 : tileCount <= 4 ? 2 : 4;
            int tileSize = atlasSize / countPerLine;
            for (int i = 0; i < shadowedOtherLightCount;) {
                if (!shadowedOtherLights[i].isPointLight) {
                    RenderSpotShadow(i, countPerLine, tileSize);
                    i += 1;
                }
                else {
                    RenderPointShadow(i, countPerLine, tileSize);
                    i += 6;
                }
            }

            cmdBuffer.SetGlobalMatrixArray(otherShadowMatricesId, otherShadowMatrices);
            cmdBuffer.SetGlobalVectorArray(otherShadowTilesId, otherShadowTiles);
            SetKeywords(otherFilterKeywords, (int) (shadowSettings.otherShadow.filterMode) - 1);

            CmdBufferExt.ProfileSample(ref context, cmdBuffer, EProfileStep.End, ProfileName);
        }

        private void RenderSpotShadow(int lightIndex, int countPerLine, int tileSize) {
            var light = shadowedOtherLights[lightIndex];
            var shadowDrawSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex) {
                useRenderingLayerMaskTest = this.shadowSettings.useRenderingLayerMask,
            };

            cullingResults.ComputeSpotShadowMatricesAndCullingPrimitives(light.visibleLightIndex, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData splitData);
            shadowDrawSettings.splitData = splitData;

            // todo 没懂
            float textlSize = 2f / (tileSize * projMatrix.m00);
            float filterSize = textlSize * ((float) shadowSettings.otherShadow.filterMode + 1f);
            float bias = light.normalBias * filterSize * 1.4142136f;

            Vector2 viewport = SetTileViewport(lightIndex, countPerLine, tileSize);
            float tileScale = 1f / countPerLine;
            SetOtherTileData(lightIndex, viewport, tileScale, bias);

            otherShadowMatrices[lightIndex] = ConvertToAtlasMatrix(projMatrix, viewMatrix, viewport, tileScale);

            cmdBuffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
            // 斜度比率
            cmdBuffer.SetGlobalDepthBias(0f, light.slopeScaleBias);
            CmdBufferExt.Execute(ref context, cmdBuffer);

            context.DrawShadows(ref shadowDrawSettings);
            // 还原
            cmdBuffer.SetGlobalDepthBias(0f, 0f);
        }

        private void RenderPointShadow(int lightIndex, int countPerLine, int tileSize) {
            var light = shadowedOtherLights[lightIndex];
            var shadowDrawSettings = new ShadowDrawingSettings(cullingResults, light.visibleLightIndex) {
                useRenderingLayerMaskTest = this.shadowSettings.useRenderingLayerMask,
            };

            float texelSize = 2f / tileSize;
            float filterSize = texelSize * ((float) shadowSettings.otherShadow.filterMode + 1f);
            float bias = light.normalBias * filterSize * 1.4142136f;
            float tileScale = 1f / countPerLine;

            float fovBias = Mathf.Atan(1f + bias + filterSize) * Mathf.Rad2Deg * 2f - 90f;
            for (int i = 0; i < 6; ++i) {
                cullingResults.ComputePointShadowMatricesAndCullingPrimitives(light.visibleLightIndex, (CubemapFace) i, fovBias, out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData splitData);
                shadowDrawSettings.splitData = splitData;
                // 因为渲染point的阴影的时候，Unity将三角形颠倒了，我们可以通过设置相机的view矩阵的y==-y, 实现相机的翻转
                viewMatrix.m11 = -viewMatrix.m11;
                viewMatrix.m12 = -viewMatrix.m12;
                viewMatrix.m13 = -viewMatrix.m13;

                int tileIndex = lightIndex + i;
                Vector2 viewport = SetTileViewport(tileIndex, countPerLine, tileSize);
                SetOtherTileData(tileIndex, viewport, tileScale, bias);
                otherShadowMatrices[tileIndex] = ConvertToAtlasMatrix(projMatrix, viewMatrix, viewport, tileScale);

                cmdBuffer.SetViewProjectionMatrices(viewMatrix, projMatrix);
                cmdBuffer.SetGlobalDepthBias(0f, light.slopeScaleBias);

                CmdBufferExt.Execute(ref context, cmdBuffer);

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
            float pcfFilterSize = texelSize * ((float) shadowSettings.directionalShadow.filterMode + 1f);
            cullingSphere.w -= pcfFilterSize;

            cullingSphere.w *= cullingSphere.w;
            cascadeCullingSpheres[cascadeIndex] = cullingSphere;

            // https://zhuanlan.zhihu.com/p/379042993
            cascadeData[cascadeIndex] = new Vector4(1f / cullingSphere.w, pcfFilterSize * 1.4142136f);
        }

        // vp矩阵将positionWS转换到ndc中， 这个矩阵将positionWS转换到size=1的CUBE区域中的某个tile块中
        // 也可以理解为转换到shadowspace
        private Matrix4x4 ConvertToAtlasMatrix(Matrix4x4 projMatrix, Matrix4x4 viewMatrix, Vector2 offset, float scale) {
            Matrix4x4 worldToShadow = GetShadowTransform(projMatrix, viewMatrix);

            Matrix4x4 sliceTransform = Matrix4x4.identity;
            // 缩放xy轴
            sliceTransform.m00 = scale;
            sliceTransform.m11 = scale;

            // 平移xy轴
            sliceTransform.m03 = offset.x * scale;
            sliceTransform.m13 = offset.y * scale;

            // 必须对xy轴进行sclae以及translate的转换，否则采样shadowmap的时候会有问题，导致最终阴影表现不正确
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

            cmdBuffer.SetViewport(new Rect(offset.x * tileSize, offset.y * tileSize, tileSize, tileSize));
            return offset;
        }

        private void SetOtherTileData(int index, Vector2 offset, float scale, float bias) {
            float border = atlasSizes.w * 0.5f;
            Vector4 data = Vector4.zero;
            data.x = offset.x * scale + border;
            data.x = offset.y * scale + border;
            data.z = scale - border - border;
            data.w = bias;
            otherShadowTiles[index] = data;
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

        public Vector3 ReserveDirectionalShadows(Light light, int indexOfVisibleLights) {
            if (shadowedDirectionalLightCount < MAX_SHADOW_DIRECTIONAL_LIGHT_COUNT &&
                light.shadows != LightShadows.None && light.shadowStrength > 0f) {
                int shadowMaskChannel = -1;
                LightBakingOutput lbo = light.bakingOutput;
                if (lbo.lightmapBakeType == LightmapBakeType.Mixed && lbo.mixedLightingMode == MixedLightingMode.Shadowmask) {
                    useShadowMask = true;
                    shadowMaskChannel = lbo.occlusionMaskChannel;
                }

                // 如果被裁剪,返回 -light.shadowStrength而不是, 正的-light.shadowStrength
                if (!cullingResults.GetShadowCasterBounds(indexOfVisibleLights, out Bounds bounds)) {
                    return new Vector4(-light.shadowStrength, 0f, 0f, shadowMaskChannel);
                }

                // 光源设置为投射阴影，但是没有物件接收阴影，不需要shadowmap
                shadowedDirectionalLights[shadowedDirectionalLightCount] = new ShadowedDirectionalLight() {
#if UNITY_EDITOR
                    lightName = light.name,
#endif

                    indexOfVisibleLights = indexOfVisibleLights,
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
            if (light.shadows == LightShadows.None || light.shadowStrength <= 0f) {
                return new Vector4(0f, 0f, 0f, -1f);
            }

            float maskChannel = -1f;
            float shadowStrength = light.shadowStrength;
            LightBakingOutput lbo = light.bakingOutput;
            if (lbo.lightmapBakeType == LightmapBakeType.Mixed && lbo.mixedLightingMode == MixedLightingMode.Shadowmask) {
                useShadowMask = true;
                maskChannel = lbo.occlusionMaskChannel;
            }

            bool isPointLight = light.type == LightType.Point;
            // 点光源辐射四面八方，所以使用cube的6个面进行渲染，简单将点光源视为6个灯光，会占用6个tile块，所以16个里面最多只能有两个
            // 点光源阴影的tile
            int newLightCount = shadowedOtherLightCount + (isPointLight ? 6 : 1);
            // 非平行光超过了max
            if (newLightCount >= MAX_SHADOW_OTHER_LIGHT_COUNT || !cullingResults.GetShadowCasterBounds(visibleLightIndex, out var bounds)) {
                return new Vector4(-shadowStrength, 0, 0f, maskChannel);
            }

            shadowedOtherLights[shadowedOtherLightCount] = new ShadowedOtherLight {
                visibleLightIndex = visibleLightIndex,
                slopeScaleBias = light.shadowBias,
                normalBias = light.shadowNormalBias,
                isPointLight = isPointLight,
            };

            Vector4 data = new Vector4(light.shadowStrength, shadowedOtherLightCount, isPointLight ? 1f : 0f, maskChannel);
            shadowedOtherLightCount = newLightCount;
            return data;
        }
    }
}
