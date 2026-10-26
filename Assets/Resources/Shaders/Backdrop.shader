Shader "Wataame/Backdrop"
{
    // 背景の大きな球。上は水色、下はピンクのやさしいグラデーションと水玉
    Properties
    {
        _Top ("Top", Color) = (0.78, 0.89, 1.0, 1)
        _Bottom ("Bottom", Color) = (1.0, 0.88, 0.92, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Background" "Queue" = "Background" }
        Pass
        {
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Top;
            fixed4 _Bottom;

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 dir : TEXCOORD0;
            };

            v2f vert(appdata_base v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float3 col = lerp(_Bottom.rgb, _Top.rgb, smoothstep(-0.15, 0.45, d.y));
                // 水玉
                float2 uv = float2(atan2(d.z, d.x) * 6.0, d.y * 9.0);
                float2 cell = frac(uv) - 0.5;
                float dots = smoothstep(0.16, 0.12, length(cell)) * smoothstep(-0.1, 0.2, d.y);
                col = lerp(col, float3(1, 1, 1), dots * 0.35);
                return fixed4(col, 1);
            }
            ENDCG
        }
    }
}
