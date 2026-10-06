// 卡通著色（Built-in 管線）：階梯式明暗 + 暗部色偏 + 邊緣光 + 反向外殼描邊。
// 角色模型在執行時會被換上這個 shader（見 CharacterView.ApplyToon），顏色沿用 FBX 內的顏色。
Shader "SanGuo/ToonLit"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
        _ShadowTint ("Shadow Tint", Color) = (0.42, 0.36, 0.56, 1)
        _RimColor ("Rim Color", Color) = (1.0, 0.93, 0.78, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 2.6
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.45
        _Steps ("Light Steps", Range(2, 5)) = 3
        _OutlineColor ("Outline Color", Color) = (0.07, 0.05, 0.08, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.06)) = 0.028
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }

        // ---- 描邊：把背面往法線方向膨脹，只畫背面 ----
        Pass
        {
            Name "OUTLINE"
            Cull Front

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _OutlineColor;
            float _OutlineWidth;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; };

            v2f vert(appdata v)
            {
                v2f o;
                float3 n = normalize(v.normal);
                o.pos = UnityObjectToClipPos(float4(v.vertex.xyz + n * _OutlineWidth, 1));
                return o;
            }

            fixed4 frag(v2f i) : SV_Target { return _OutlineColor; }
            ENDCG
        }

        // ---- 本體：階梯明暗 + 邊緣光 ----
        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "ForwardBase" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            fixed4 _Color, _ShadowTint, _RimColor;
            float _RimPower, _RimStrength, _Steps;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; float3 worldNormal : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                float ndl = dot(n, normalize(_WorldSpaceLightPos0.xyz)) * 0.5 + 0.5;   // 半 Lambert
                float stepped = floor(ndl * _Steps) / max(_Steps - 1.0, 1.0);          // 階梯化
                stepped = saturate(stepped);
                float3 shaded = lerp(_Color.rgb * _ShadowTint.rgb, _Color.rgb, stepped);

                // 邊緣光：正交攝影機的視線方向 = 攝影機前方的反方向。
                float3 camForward = normalize(mul((float3x3)unity_CameraToWorld, float3(0, 0, 1)));
                float rim = pow(1.0 - saturate(dot(n, -camForward)), _RimPower);
                shaded += _RimColor.rgb * rim * _RimStrength * stepped;

                shaded *= lerp(1.0, _LightColor0.rgb, 0.5);
                return fixed4(shaded, 1);
            }
            ENDCG
        }
    }

    Fallback "VertexLit"
}
