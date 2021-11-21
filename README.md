# Unity-CignalRP
学习URP, 实现自定义的渲染管线

预计实现：
1. 每个相机渲染频率差异化控制
2. 尽可能使用同一个相机渲染 场景和UI
3. hsr对于渲染物体顺序影响？是否可以在渲染不透明物体的时候，不在cpu端进行从近到远的排序？
4. normalbias推近多少合适？
5. 每个光源对应的layer设计？顶点光?像素光? 只针对某些layer进行后处理?
6. 为什么使用hdr时候，会对于gamma/linear有影响？也就是会导致画面变暗？
7. lightmap采样的时候,如何得知是哪张lightmap中进行采样? 是不是传递给shader的unity_Lightmap已经是确定的了[通过framedebugger确认]? 
    A: cpu提前计算得到具体的哪种lightmap, 然后传递给gpu
8. lightmap是否压缩是不是lightsetting中设置的?可以通过调整这个设置看一下framedebugger的参数传递
9. 为什么shadowmask下 min(bakedShadow, realTimeShadow);是这种实现方式?而且是lerp(1)开始?


学习资料:
1. https://edu.uwa4d.com/course-intro/0/282?purchased=true 
2. https://catlikecoding.com/unity/tutorials/custom-srp/ 
3. https://github.com/3-Delta/Unity-Urp 
4. https://learnopengl-cn.readthedocs.io/zh/latest/01%20Getting%20started/04%20Hello%20Triangle/
5. https://edu.uwa4d.com/course-intro/0/283
6. https://www.zhihu.com/column/c_1237044646569447424
7. https://space.bilibili.com/7398208/video
8. https://www.zhihu.com/people/zilch-5/columns
9. https://www.zhihu.com/people/luckywjr/posts
10. https://www.zhihu.com/people/yang-yang-90-83/columns
11. https://www.zhihu.com/people/xiao-shi-chang-23/posts 引擎
12. https://www.zhihu.com/people/tomxx/posts
13. https://www.zhihu.com/people/cris-66-91/posts
14. https://www.zhihu.com/people/jeff-wong-92/posts
15. https://www.zhihu.com/column/c_1189143258499133440 软渲染
16. https://baddogzz.github.io/page2/
17. https://github.com/Maligan/unity-subassets-drag-and-drop
18. https://www.bilibili.com/video/BV1aJ411t7N6?spm_id_from=333.999.0.0
19. https://www.bilibili.com/video/BV1X7411F744?spm_id_from=333.999.0.0
20. https://www.bilibili.com/video/BV1Wv41167i2?spm_id_from=333.999.0.0
21. https://www.bilibili.com/video/BV1Ea4y1j7gu?spm_id_from=333.999.0.0
22. https://www.bilibili.com/video/BV1ca4y1W7wN?spm_id_from=333.999.0.0
23. https://learn.u3d.cn/tutorial/ilruntime
24. https://zhuanlan.zhihu.com/p/265463655 lightmap
25. https://zhuanlan.zhihu.com/p/37639418
