using UnityEngine;
using UnityEditor;

namespace CignalRP {
    [CanEditMultipleObjects]
    [CustomEditorForRenderPipeline(typeof(Light), typeof(CRPCreator))]
    public class CRPLightEditor : LightEditor {
        public static readonly GUIContent renderingLayerMaskLabel = new GUIContent("⬆ReplaceLayerMask⬆", "Functional version of above property.");

        public override void OnInspectorGUI() {
            base.OnInspectorGUI();
            
            RenderingLayerMaskDrawer.Draw(settings.renderingLayerMask, renderingLayerMaskLabel);

            // 只针对spot的innerAngle
            if (!settings.lightType.hasMultipleDifferentValues && (LightType) settings.lightType.enumValueIndex == LightType.Spot) {
                settings.DrawInnerAndOuterSpotAngle();
            }

            settings.ApplyModifiedProperties();

            var light = target as Light;
            if (light.cullingMask != -1) {
                EditorGUILayout.HelpBox(
                    light.type == LightType.Directional ? "Culling Mask only affects shadows." : "Culling Mask only affects shadow unless Lights Per Objects is on.",
                    MessageType.Warning
                );
            }
        }
    }
}
