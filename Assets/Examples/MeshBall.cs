using UnityEngine;

public class MeshBall : MonoBehaviour {
    public readonly static int baseColorId = Shader.PropertyToID("_BaseColor");

    [SerializeField] Mesh mesh = default;
    [SerializeField] Material material = default;

    Matrix4x4[] matrices = new Matrix4x4[1023];
    Vector4[] baseColors = new Vector4[1023];

    MaterialPropertyBlock block;

    private void Awake() {
        for (int i = 0, length = matrices.Length; i < length; ++i) {
            // 设置Transform
            matrices[i] = Matrix4x4.TRS(Random.insideUnitSphere * 10f, Quaternion.Euler(Random.value * 360f, Random.value * 360f, Random.value * 360f), 
                Vector3.one * Random.Range(0.5f, 1.5f));
            
            // 设置材质颜色
            baseColors[i] = new Vector4(Random.value, Random.value, Random.value, Random.Range(0.5f, 1f));
        }
    }

    private void Update() {
        if (block == null) {
            block = new MaterialPropertyBlock();
            block.SetVectorArray(baseColorId, baseColors);
        }

        // 需要用block,否则都是用最后一次的mat属性绘制
        // 每个dc最多渲染n个物体，超过则使用多个dc渲染, 这个n根据机器性能动态设置
        Graphics.DrawMeshInstanced(mesh, 0, material, matrices, 1023, block);
    }
}
