#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.Rendering;

namespace CignalRP {
#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(RenderingLayerMaskField))]
    public class RenderingLayerMaskDrawer : PropertyDrawer {
        public static void Draw(Rect position, SerializedProperty property, GUIContent label) {
            EditorGUI.showMixedValue = property.hasMultipleDifferentValues;
            EditorGUI.BeginChangeCheck();

            int mask = property.intValue;
            bool isUInt = property.type == "uint";
            if (isUInt && mask == int.MaxValue) {
                mask = -1;
            }

            mask = EditorGUI.MaskField(position, label, mask, GraphicsSettings.currentRenderPipeline.renderingLayerMaskNames);
            if (EditorGUI.EndChangeCheck()) {
                property.intValue = isUInt && mask == -1 ? int.MinValue : mask;
            }

            EditorGUI.showMixedValue = false;
        }

        public static void Draw(SerializedProperty property, GUIContent label) {
            Draw(EditorGUILayout.GetControlRect(), property, label);
        }

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label) {
            Draw(position, property, label);
        }
    }
#endif

    public class RenderingLayerMaskField : PropertyAttribute { }
}
