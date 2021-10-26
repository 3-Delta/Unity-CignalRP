using UnityEngine;
using UnityEditor;

namespace CignalRP {
    [CanEditMultipleObjects]
    [CustomEditorForRenderPipeline(typeof(Light), typeof(CRPCreator))]
    public class CRPLightEditor : LightEditor {
        public override void OnInspectorGUI() {
            base.OnInspectorGUI();

            // 只针对spot的innerAngle
            if (!settings.lightType.hasMultipleDifferentValues && (LightType)settings.lightType.enumValueIndex == LightType.Spot) {
                settings.DrawInnerAndOuterSpotAngle();
                settings.ApplyModifiedProperties();
            }
        }
    }
}
