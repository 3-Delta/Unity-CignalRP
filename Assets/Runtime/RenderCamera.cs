using System;
using System.Collections.Generic;
using CignalRP;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;

// 该组件需要提前挂载
// 方便查看lights的排序，因为排序按照光源类型，距离camera的远近等因素排序的
[DisallowMultipleComponent]
public class RenderCamera : MonoBehaviour {
    public CameraRenderer render;
    public CullingResults results;
    public List<string> cullLights = new List<string>();

    public static RenderCamera Instance { get; private set; }

    private void Awake() {
        Instance = this;
    }

    private void OnDestroy() {
        Instance = null;
    }

    public void Set(CameraRenderer render, CullingResults results) {
        this.render = render;
        this.results = results;
        
        cullLights.Clear();

        foreach (var l in results.visibleLights) {
            cullLights.Add(l.light.name);
        }
    }
}
