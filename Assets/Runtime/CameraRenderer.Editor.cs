#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;

namespace CignalRP {
    public partial class CameraRenderer {
        // 因为srpbatcher需要特定的shader数据格式，所以除了SRPDefaultUnlit之外的基本全部不支持
        private static readonly ShaderTagId[] UnsupportedShaderTagIds = {
            new ShaderTagId("Always"),
            new ShaderTagId("ForwardBase"),
            new ShaderTagId("PrepassBase"),
            new ShaderTagId("Vertex"),
            new ShaderTagId("VertexLMRGBM"),
            new ShaderTagId("VertexLM"),

            // 《入门紧要》 p183
            // new ShaderTagId("ForwardAdd"),
            // new ShaderTagId("Deferred"),
            // new ShaderTagId("PrepassFinal"),
        };

        private static Material ErrorMaterial;

        private void DrawUnsupported() {
            if (ErrorMaterial == null) {
                ErrorMaterial = new Material(Shader.Find("Hidden/InternalErrorShader"));
            }

            // 使用camera的distance排序渲染
            var sortingSettings = new SortingSettings(this.camera);
            var drawingSettings = new DrawingSettings(UnsupportedShaderTagIds[0], sortingSettings);
            for (int i = 1, length = UnsupportedShaderTagIds.Length; i < length; i++) {
                drawingSettings.SetShaderPassName(i, UnsupportedShaderTagIds[i]);
            }

            // 覆盖原始的material，使用errorMaterial
            // todo： URP中的overrideMaterial估计也是这样使用的
            drawingSettings.overrideMaterial = ErrorMaterial;

            var filteringSetttings = FilteringSettings.defaultValue;
            context.DrawRenderers(cullingResults, ref drawingSettings, ref filteringSetttings);
        }

        private void DrawGizmosBeforeFX() {
            if (Handles.ShouldRenderGizmos()) {
                if (useInterBuffer) {
                    DrawFrameBuffer(CameraDepthAttachmentId, BuiltinRenderTextureType.CameraTarget, true);
                    ExecuteCmdBuffer(ref context, cmdBuffer);
                }

                context.DrawGizmos(camera, GizmoSubset.PreImageEffects);
            }
        }

        private void DrawGizmosAfterFX() {
            if (Handles.ShouldRenderGizmos()) {
                context.DrawGizmos(this.camera, GizmoSubset.PostImageEffects);
            }
        }

        private void PrepareForSceneWindow() {
            // https://catlikecoding.com/unity/tutorials/custom-srp/custom-render-pipeline/
            // 解决UGUI在gameview显示，不在sceneView显示的问题，不管Canvas在那种渲染模式
            // 只是在渲染模式为Overlay的时候，framedebugger中会将UI的渲染独立出来，而不是在renderpipeline中一起渲染
            // 渲染模式为camera的时候，framebebugger会将UI合并到renderpipeline中一起渲染。
            // 不被渲染的情况下，我们在editor下依然可以进行编辑操作，也就是recttransform.sizeDelta改变之后，边框gizmos会变化
            if (this.camera.cameraType == CameraType.SceneView) {
                ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
                // scene禁用scale
                useRenderScale = false;
            }
        }

        private void Prepare() {
            this.PrepareForSceneWindow();
        }
    }
}
#endif
