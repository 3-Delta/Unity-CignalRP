using UnityEngine;
using UnityEditor;

namespace CignalRP {
    [CanEditMultipleObjects]
    [CustomEditorForRenderPipeline(typeof(Light), typeof(CRPCreator))]
    public class CRPLightEditor : LightEditor {
        public static readonly GUIContent renderingLayerMaskLabel =
            new GUIContent("Rendering Layer Mask", "Functional version of above property.");
        
        public override void OnInspectorGUI() {
            base.OnInspectorGUI();

            RenderingLayerMaskDrawer.Draw(settings.renderingLayerMask, renderingLayerMaskLabel);
            
            // 只针对spot的innerAngle
            if (!settings.lightType.hasMultipleDifferentValues && (LightType)settings.lightType.enumValueIndex == LightType.Spot) {
                settings.DrawInnerAndOuterSpotAngle();
                settings.ApplyModifiedProperties();
            }
        }
    }
}
