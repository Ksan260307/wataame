#ifndef WATAAME_COMMON_INCLUDED
#define WATAAME_COMMON_INCLUDED

// WataameMaterials.ApplyLighting() で設定するグローバル値。
// パイプラインのライトに頼らないので Built-in / URP どちらでも同じに見える。
float4 _WataameLightDir;    // xyz: ライトへ向かう方向
float4 _WataameLightColor;
float4 _WataameSkyColor;
float4 _WataameGroundColor;

float3 WataameAmbient(float3 n)
{
    return lerp(_WataameGroundColor.rgb, _WataameSkyColor.rgb, n.y * 0.5 + 0.5);
}

float WataameHash(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.zyx + 31.32);
    return frac((p.x + p.y) * p.z);
}

float3 WataameHash3(float3 p)
{
    return float3(WataameHash(p), WataameHash(p + 17.3), WataameHash(p + 41.7));
}

float WataameNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float n000 = WataameHash(i);
    float n100 = WataameHash(i + float3(1, 0, 0));
    float n010 = WataameHash(i + float3(0, 1, 0));
    float n110 = WataameHash(i + float3(1, 1, 0));
    float n001 = WataameHash(i + float3(0, 0, 1));
    float n101 = WataameHash(i + float3(1, 0, 1));
    float n011 = WataameHash(i + float3(0, 1, 1));
    float n111 = WataameHash(i + float3(1, 1, 1));
    return lerp(lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y),
                lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y), f.z);
}

#endif
