// 戰場地板：不透明的石磚（兩色棋盤紋 + 邊緣暗角），不受光照，避免 URP 材質問題。
// 以 Cube 的 UV 運作：頂面 uv 0–1 對應整個棋盤，_Tiles 是棋盤格數。
Shader "SanGuo/BattleFloor"
{
    Properties
    {
        _ColorA ("Color A", Color) = (0.56, 0.51, 0.44, 1)
        _ColorB ("Color B", Color) = (0.50, 0.45, 0.39, 1)
        _Tiles ("Tiles", Vector) = (5, 5, 0, 0)
        _Vignette ("Edge Darkening", Range(0, 1)) = 0.25
    }
    SubShader
    {
        Tags { "Queue" = "Geometry" "RenderType" = "Opaque" }
        Cull Back
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _ColorA;
            fixed4 _ColorB;
            float4 _Tiles;
            float _Vignette;

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
                float2 t = floor(i.uv * _Tiles.xy);
                float parity = fmod(t.x + t.y, 2.0);
                fixed4 c = lerp(_ColorA, _ColorB, parity);
                float2 d = min(i.uv, 1.0 - i.uv);
                float edge = saturate(min(d.x, d.y) * 6.0);
                c.rgb *= lerp(1.0 - _Vignette, 1.0, edge);
                return c;
            }
            ENDCG
        }
    }
}
