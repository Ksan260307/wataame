Shader "Wataame/CottonCandy"
{
    // シェル (毛皮) 方式のわたあめ。_Shell = 0 が本体、> 0 は法線方向に押し出した層で、
    // ノイズで穴をあけて細い糸のように見せる。色は頂点カラー (積もったざらめの色)
    Properties
    {
        _Shell ("Shell", Range(0, 1)) = 0
        _ShellLength ("Shell Length", Float) = 0.007
        _FiberScale ("Fiber Scale", Float) = 650
        _Tint ("Tint", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" }
        Pass
        {
            Cull Back
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "WataameCommon.cginc"

            float _Shell;
            float _ShellLength;
            float _FiberScale;
            fixed4 _Tint;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 objPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                float3 objNormal : TEXCOORD3;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                float3 p = v.vertex.xyz + v.normal * (_Shell * _ShellLength);
                p.y -= _Shell * _Shell * _ShellLength * 0.25; // 外側の糸はほんの少したれる
                o.pos = UnityObjectToClipPos(float4(p, 1));
                o.objPos = v.vertex.xyz;
                o.worldPos = mul(unity_ObjectToWorld, float4(p, 1)).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.objNormal = v.normal;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // 押し出す前の位置でノイズを取ると、層をまたいで糸がつながる。少しずらして糸を曲げる
                float3 q = (i.objPos + i.objNormal * (_Shell * _ShellLength * 0.6)) * _FiberScale;
                float fiber = WataameNoise(q) * 0.55 + WataameNoise(q * 2.3 + 11.7) * 0.3 + WataameNoise(q * 5.1 + 3.1) * 0.15;
                if (_Shell > 0.0)
                    clip(fiber - (0.42 + _Shell * 0.5));

                float3 n = normalize(i.worldNormal);
                float3 v = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 l = normalize(_WataameLightDir.xyz);

                // わたあめは明るいパステル。外側の糸ほど白っぽく光る
                float3 base = lerp(i.color.rgb, float3(1, 1, 1), 0.12 + _Shell * 0.3) * _Tint.rgb;
                float wrap = saturate((dot(n, l) + 0.8) / 1.8);
                float clump = WataameNoise(i.objPos * 90.0);
                float occlusion = lerp(0.78, 1.0, saturate(_Shell * 0.8 + fiber * 0.25 + clump * 0.3));
                float3 col = base * (0.72 + 0.42 * wrap) * occlusion;

                // 逆光で光が透ける感じ
                col += base * pow(saturate(dot(-v, l)), 3.0) * 0.3;
                // ふちがふわっと明るい
                float rim = pow(1.0 - saturate(dot(n, v)), 2.5);
                col = lerp(col, float3(1, 1, 1), rim * 0.4);

                // ざらめの結晶がきらっと光る
                float3 cell = floor(i.objPos * 650.0);
                float sparkleMask = step(0.985, WataameHash(cell));
                float3 facet = normalize(n + (WataameHash3(cell) - 0.5) * 1.6);
                float sparkle = pow(saturate(dot(reflect(-v, facet), l)), 24.0) * sparkleMask;
                col += sparkle * 0.9;

                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
