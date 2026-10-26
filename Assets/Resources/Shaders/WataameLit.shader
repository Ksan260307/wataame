Shader "Wataame/Lit"
{
    // やわらかいトゥーン調のライティング。_Metal で金属 (ボウル・ヘッド) らしい映り込みを出す
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _Gloss ("Gloss", Range(0, 1)) = 0.4
        _Metal ("Metal", Range(0, 1)) = 0
        _Rim ("Rim", Range(0, 1)) = 0.25
        _Emission ("Emission", Color) = (0, 0, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "WataameCommon.cginc"

            fixed4 _Color;
            half _Gloss;
            half _Metal;
            half _Rim;
            fixed4 _Emission;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
                float3 worldNormal : TEXCOORD1;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                float3 v = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 l = normalize(_WataameLightDir.xyz);

                float diffuse = smoothstep(-0.15, 0.75, dot(n, l));
                float3 albedo = _Color.rgb;
                float3 col = albedo * (WataameAmbient(n) + _WataameLightColor.rgb * diffuse);

                // 金属: スタジオの照明が映り込んだような簡易環境反射
                float3 r = reflect(-v, n);
                float strip = smoothstep(0.55, 0.8, r.y) * 0.9 + smoothstep(0.25, 0.0, abs(r.x - 0.35)) * 0.5 * saturate(r.y + 0.6);
                float3 env = lerp(_WataameGroundColor.rgb * 1.4, _WataameSkyColor.rgb * 1.6, saturate(r.y * 0.5 + 0.5)) + strip;
                col = lerp(col, env * albedo, _Metal);

                float3 h = normalize(l + v);
                float spec = pow(saturate(dot(n, h)), lerp(12.0, 220.0, _Gloss)) * _Gloss;
                col += spec * _WataameLightColor.rgb;

                float rim = pow(1.0 - saturate(dot(n, v)), 3.0) * _Rim;
                col += rim * float3(1.0, 0.95, 0.98);
                col += _Emission.rgb;
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
