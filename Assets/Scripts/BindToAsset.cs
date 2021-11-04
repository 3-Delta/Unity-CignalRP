using UnityEngine;
using UnityEditor;

public class BindToAsset : MonoBehaviour {
    public ScriptableObject[] children = new ScriptableObject[0];
    public ScriptableObject parent;

    [ContextMenu("Bind")]
    public void Bind() {
        // string parentPath = AssetDatabase.GetAssetPath(parent);
        // foreach (var one in children) {
        //     string path = AssetDatabase.GetAssetPath(one);
        //     AssetDatabase.
        //     AssetDatabase.AddObjectToAsset(one, parentPath);
        // }
        //
        // AssetDatabase.SetMainObject(parent, parentPath);
    }
}
