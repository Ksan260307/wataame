Shader "Wataame/Particle"
{
    // パーティクル (糸・ざらめ)、丸い影、ガイド線やリング用。頂点カラー × _Tint。
    // _Band = 0: UV の中心からやわらかく消える丸 / 1: UV の y 方向だけ両端が薄くなる帯
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _Band ("Band", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Tint;
            float _Band;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Tint;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float d = _Band > 0.5 ? abs(i.uv.y * 2.0 - 1.0) : length(i.uv * 2.0 - 1.0);
                float a = saturate(1.0 - d);
                return fixed4(i.color.rgb, i.color.a * a * a);
            }
            ENDCG
        }
    }
}
