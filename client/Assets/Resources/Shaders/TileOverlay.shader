// 戰場格線：幾乎透明的淡色底 + 細邊框（取代實心地磚）。_Color 的 a 是底色濃度，邊框會比底色再亮一些。
Shader "SanGuo/TileOverlay"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 0.16)
        _Border ("Border Width", Range(0.01, 0.2)) = 0.055
    }
    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Border;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 pos : SV_POSITION; float2 uv : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float2 d = min(i.uv, 1.0 - i.uv);
                float e = min(d.x, d.y);
                float edge = 1.0 - smoothstep(_Border * 0.55, _Border, e);
                fixed4 c = _Color;
                c.a = lerp(_Color.a, saturate(_Color.a + 0.55), edge);
                return c;
            }
            ENDCG
        }
    }
}
