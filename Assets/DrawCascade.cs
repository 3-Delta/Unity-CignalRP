using UnityEngine;
using UnityEngine.Rendering;
using CignalRP;
using Unity.Collections;

public class DrawCascade : CameraRenderTrigger {
#if UNITY_EDITOR
    public ShadowSettings shadowSettings;

    public Camera cam;
    public Light dirLight;

    public Color adjust = new Color(1f, 1f, 1f, 0.5f);
    public Color[] colors = new Color[] {
        Color.red, Color.green, Color.blue, Color.black
    };

    private ScriptableRenderContext context;
    private CullingResults cullingResults;

    private int countPerLine;
    private int tileSize;
    private bool hasInitContext = false;

    protected override void OnEnable() {
        int tileCount = shadowSettings.directionalShadow.cascadeCount;
        countPerLine = tileCount <= 1 ? 1 : tileCount <= 4 ? 2 : 4;
        tileSize = (int)shadowSettings.directionalShadow.shadowMapAtlasSize / countPerLine;

        for (int i = 0; i < colors.Length; i++)
        {
            colors[i] *= adjust;
        }

        hasInitContext = false;
        base.OnEnable();
    }

    protected override void OnDisable() {
        base.OnDisable();
        hasInitContext = false;
    }

    private void OnDrawGizmos() {
        if (shadowSettings == null || cam == null || dirLight == null || !hasInitContext) {
            return;
        }

        if (cam.TryGetCullingParameters(out ScriptableCullingParameters cullingParams)) {
            cullingParams.shadowDistance = Mathf.Min(shadowSettings.maxShadowVSDistance, this.cam.farClipPlane);
            cullingResults = this.context.Cull(ref cullingParams);

            NativeArray<VisibleLight> visibleLights = cullingResults.visibleLights;
            int visibleLightIndex = -1;
            for (int i = 0; i < visibleLights.Length; ++i) {
                if (visibleLights[i].light == this.dirLight) {
                    visibleLightIndex = i;
                    break;
                }
            }

            if (visibleLightIndex == -1) {
                return;
            }

            int cascadeCount = shadowSettings.directionalShadow.cascadeCount;
            for (int i = 0; i < cascadeCount; ++i) {
                cullingResults.ComputeDirectionalShadowMatricesAndCullingPrimitives(visibleLightIndex, i, cascadeCount, this.shadowSettings.directionalShadow.cascadeRatios, tileSize,
                    // https://edu.uwa4d.com/lesson-detail/282/1311/0?isPreview=0
                    // 影响unity阴影平坠的shadowmap的形成
                    dirLight.shadowNearPlane,
                    // 每个级联的矩阵都不一样， 两个light的同一个级联的矩阵也不一样
                    out Matrix4x4 viewMatrix, out Matrix4x4 projMatrix, out ShadowSplitData splitData);

                var sphere = splitData.cullingSphere;
                var farPlane = cullingParams.GetCullingPlane(i);
                // 绘制裁剪球
                var oldColor = Gizmos.color;
                Gizmos.color = colors[i];
                Gizmos.DrawSphere(sphere, sphere.w);
                Gizmos.color = oldColor;

                // 绘制相机整个视锥体
                UnityEditor.CameraEditorUtils.DrawFrustumGizmo(cam);

                // 绘制相机cascade视锥体
                oldColor = Gizmos.color;
                var oldFar = cam.farClipPlane;
                Gizmos.color = Color.cyan;
                cam.farClipPlane *= (i < cascadeCount - 1 ? shadowSettings.directionalShadow.cascadeRatios[i] : 1f);
                UnityEditor.CameraEditorUtils.DrawFrustumGizmo(cam);
                Gizmos.color = oldColor;
                cam.farClipPlane = oldFar;

                // 如何利用计算出来的viewMatrix进行绘制呢?
                //Gizmos.DrawFrustum(cam.transform.position, cam.fieldOfView, farPlane.distance, cam.nearClipPlane, cam.aspect);
            }
        }
    }

    protected override void OnBeginCameraRender(ScriptableRenderContext context, Camera camera) {
        this.context = context;
        hasInitContext = true;
    }

    protected override void OnEndCameraRender(ScriptableRenderContext context, Camera camera) {
        this.context = context;
        hasInitContext = true;
    }
#endif
}
