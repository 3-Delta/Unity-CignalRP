﻿#ifndef CRP_BRDF_INCLUDED
#define CRP_BRDF_INCLUDED

#include "Surface.hlsl"

// 物体反射的辐射能量占总辐射能量的百分比，称为反射率,总是 < 1
// https://zhuanlan.zhihu.com/p/335664226
// https://zhuanlan.zhihu.com/p/372984872
// https://zhuanlan.zhihu.com/p/54118959
// 自然界的物质根据光照特性大体可以分为金属和非金属。
/*
* 总光照 = 漫反射 + 高光反射 + 吸收， 也就涉及到 高光反射率， 漫反射率, 高光反射率 + 漫反射率 = reflectivity
* 
金属的光照特性是：漫反射率基本为0，所以漫反射颜色也为0（黑色），所以总光照 = 高光反射 + 吸收，那么高光反射到底占总光照的多少呢，
我们使用reflctivity（高光反射率） * 总光照来获得，reflctivity在[70 % ，100%]。
而高光颜色总是偏金属本身的颜色，例如黄金的高光颜色是金黄色，白银的高光颜色是灰色，黄铜的高光颜色是黄色。

金属：漫反射率 = 0，漫反射颜色 = 黑，高光反射率 = reflctivity，高光颜色 = 自身颜色

非金属的光照特性是：高光反射率在4 % 左右（高光颜色几乎为黑色0），而漫反射很强，漫反射颜色 = （1 - reflctivity） * albedo，
其中1 - reflctivity == “漫反射 + 吸收”的光照比例，再乘以diffuse后就是漫反射颜色。

非金属：漫反射率 = 1 - reflctivity，漫反射颜色 = 自身颜色，高光反射率 = 0.04，高光颜色 = 灰黑		
*/
// 非金属的反射率有所不同，但平均约为0.04
// reflctivity就是高光反射率
#define MIN_REFLECTIVITY 0.04

struct BRDF
{
    // 光线在表面： 吸收和散射，散射包括镜面反射和折射， 折射包括漫反射，折射，吸收
    float3 specular;
    float3 diffuse;
    float roughness;

    float perceptualRoughness;
    // 菲涅尔，考虑反射，也考虑折射
    float fresnal;
};

// REFLECTIVITY其实是高光占比，所以下面计算的是 漫反射率
// 漫反射率
// metallic越大，则自身颜色越不明显，对周围颜色的反射越清晰，其实就是漫反射越小，高光反射越大
// 但是metallic为0的时候，此时漫反射最大，高光反射却不是0，有个最小的高光反射0.04
float OneMinusReflectivity(float metallic)
{
    // 如果metallic == 1？也就是纯金属，那么ret = 0
    // 如果metallic == 0？也就是纯非金属，那么ret = 0.96
    // 
    // 如果metallic == 1，也就是纯金属，那么高光反射率就是 1
    // 如果metallic == 0，也就是非金属，那么高光反射率就是 MIN_REFLECTIVITY == 0.04
    // 
    // 	金属没有漫反射率，但是非金属有高光反射率
    // 
    // 差值，漫反射0.96~0，高光反射0.04~1 如果不考虑吸收的话
    return lerp(1 - MIN_REFLECTIVITY, 0, metallic);

    // 正常如果不考虑最低的高光反射的话，应该是 return 1 - metallic; 也就会返回范围在[0, 1]之间
    // 如果考虑最低的高光反射的话，就需要限制返回返回在[0, 1-0.04]之间， 也就是(1 - metallic) * (1 - 0.04)

    // 换算：(MIN_REFLECTIVITY - 1) * metallic + (1 - MIN_REFLECTIVITY)
    // = (1 - MIN_REFLECTIVITY) * (1 - metallic)
}

BRDF GetBRDF(FragSurface surface)
{
    // 非金属的纯粹漫反射的物体
    BRDF brdf;
    float oneMinus = OneMinusReflectivity(surface.metallic);
    // 金属的漫反射是黑色，高光是本身颜色
    // 非金属漫反射接近本身颜色，高光是最小反射率*白色
    brdf.diffuse = surface.color * oneMinus;

    // todo 为什么不是如下？ 因为非金属虽然有最小高光反射0.04，但是却不影响高光颜色，会用白色(1, 1, 1);
    // brdf.specular = lerp(MIN_REFLECTIVITY, 1, surface.metallic) * surface.color;
    // 高光颜色总是偏金属本身的颜色，例如黄金的高光颜色是金黄色，白银的高光颜色是灰色，黄铜的高光颜色是黄色。
    float3 minColor = MIN_REFLECTIVITY * (1, 1, 1);
    brdf.specular = lerp(minColor, surface.color, surface.metallic);

    brdf.perceptualRoughness = PerceptualSmoothnessToPerceptualRoughness(surface.smoothness);
    brdf.roughness = PerceptualRoughnessToRoughness(brdf.perceptualRoughness);

    brdf.fresnal = saturate(surface.smoothness + 1.0 - oneMinus);
    
    return brdf;
}

// 高光强度，根据视角方向和反射方向的平行程度，有具体公式
float SpecularStrength(FragSurface surface, BRDF brdf, Light light)
{
    float3 h = SafeNormalize(light.fragToLightDirectionWS + surface.frag2CameraWS);
    float nh2 = Square(saturate(dot(surface.normalWS, h)));
    float lh2 = Square(saturate(dot(light.fragToLightDirectionWS, h)));
    float r2 = Square(brdf.roughness);
    float d2 = Square(nh2 * (r2 - 1.0) + 1.00001);
    float normalization = brdf.roughness * 4.0 + 2.0;
    return r2 / (d2 * max(0.1, lh2) * normalization);
}

// 直接光
// https://zhuanlan.zhihu.com/p/393174880
// https://zhuanlan.zhihu.com/p/152226698
// brdf = F(l, v) = KdFd + KsFs
// Fd, Fs分别为漫反射,高光反射brdf函数，Kd, Ks分别是漫反射，高光反射的反射系数，因为能量守恒，Kd + Ks < 1, 因为有部分被吸收了
// 其实pbr中，这里粗糙度其实影响的是 法线分布函数， 这里全部究极到了高光强度这个概念中
// 这里使用的brdf是Minimalist CookTorrance BRDF的一种变体
float3 DirectBRDF(FragSurface surface, BRDF brdf, Light light)
{
    // 高光强度和光滑度有关系
    float specularStrength = SpecularStrength(surface, brdf, light);
    #if defined(_PREMULTIPLY_ALPHA)
        // 为了解决变化alpha的时候,高光也跟随变化的情况，也就是只让漫反射跟随变化，高光不变化
        // diffuse必须PreMulAlpha，而且blendMode为one, other
        // 假设一种极端情况，surface.alpha为0，此时如果需要高光显示，那么必然和colorbuffer混合的时候，不能使用srcAlpha作为混合因子，因为此时srcAlpha ==0,
        // scrAlpha * surfaceColor + dstColor * (otherFactor) == dstColor * (otherFactor), 很显然，不能使用这个混合模式
        // 为了确保srcColor一定被保留，必须one, other的混合模式
        // 然后为了alpha只影响到diffuse, 而不影响specular, 可以在DirectBRDF函数中，将diffuse设置为跟随alpha变化，也就是*alpha
        // 同时也说明，alpha其实作用只是blend, 没有其他任何作用
        // 一般情况下感觉alpha起作用，完全是因为blendmode是srcAlpha, 会导致srcColor被完全消除，所以感觉alpha起了作用
        return specularStrength * brdf.specular + brdf.diffuse * surface.alpha;
    #else
        return specularStrength * brdf.specular + brdf.diffuse;
    #endif
}

// 环境光是怎么作用在surface的
float3 IndirectBRDF(FragSurface surface, BRDF brdf, float3 giDiffuse, float3 giSpecular)
{
    float NDotV = dot(surface.normalWS, surface.frag2CameraWS);
    float fresnalStrength = surface.fresnalStrength * Pow4(1.0 - saturate(NDotV));

    // 高光 乘 高光， 如果没有菲涅尔的话是：giSpecular * brdf.specular
    float3 reflection = giSpecular * lerp(brdf.specular, brdf.fresnal, fresnalStrength);
    reflection /= (brdf.roughness * brdf.roughness + 1.0);
    
    // 这里为甚两个diffuse相乘？因为brdf的diffuse其实是frag属性，giDiffuse其实是间接光属性，把间接光可以当做一个新的临时光源处理，那就需要*
    // 直接光光源怎么计算diffuse的？就是光源颜色*frag颜色*dotNL！
    return giDiffuse * brdf.diffuse + reflection;
}

#endif
