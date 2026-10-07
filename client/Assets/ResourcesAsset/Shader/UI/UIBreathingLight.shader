Shader "CGShader/UI/UIBreathingLight"
{
    Properties
    {
        [PerRendererData]_MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        [Header(Breathing Light Effect)]
        [Space(10)]
        [HDR]_BreathingColor ("Breathing Color", Color) = (1,1,1,1)
        _BreathingSpeed ("Breathing Speed", Range(0.1, 5)) = 1.0
        _BreathingIntensity ("Breathing Intensity", Range(0, 3)) = 1.0
        _MinAlpha ("Min Alpha", Range(0, 1)) = 0.3
        _MaxAlpha ("Max Alpha", Range(0, 1)) = 1.0
        _MinBrightness ("Min Brightness", Range(0, 1)) = 0.5
        _MaxBrightness ("Max Brightness", Range(0, 2)) = 1.5
        
        [Header(UI Settings)]
        [Space(10)]
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "Default"
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                fixed4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _BreathingColor;
            float _BreathingSpeed;
            float _BreathingIntensity;
            float _MinAlpha;
            float _MaxAlpha;
            float _MinBrightness;
            float _MaxBrightness;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                
                OUT.worldPosition = v.vertex;
                OUT.vertex = UnityObjectToClipPos(OUT.worldPosition);
                OUT.texcoord = TRANSFORM_TEX(v.texcoord, _MainTex);
                OUT.color = v.color * _Color;
                return OUT;
            }

            // 呼吸燈效果計算
            float CalculateBreathing(float time)
            {
                // 使用更平滑的曲線
                float breathing = sin(time * _BreathingSpeed) * 0.5 + 0.5;
                breathing = smoothstep(0.0, 1.0, breathing);
                return breathing;
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // 取樣基礎紋理
                half4 color = (tex2D(_MainTex, IN.texcoord) + _TextureSampleAdd) * IN.color;
                
                // 計算呼吸效果
                float time = _Time.y;
                float breathingValue = CalculateBreathing(time);
                
                // 應用透明度呼吸效果
                float breathingAlpha = lerp(_MinAlpha, _MaxAlpha, breathingValue);
                color.a *= breathingAlpha;
                
                // 應用亮度呼吸效果
                float breathingBrightness = lerp(_MinBrightness, _MaxBrightness, breathingValue);
                color.rgb *= breathingBrightness;
                
                // 應用呼吸顏色
                color.rgb = lerp(color.rgb, color.rgb * _BreathingColor.rgb, _BreathingIntensity * breathingValue);

                #ifdef UNITY_UI_CLIP_RECT
                color.a *= UnityGet2DClipping(IN.worldPosition.xy, _ClipRect);
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip (color.a - 0.001);
                #endif

                return color;
            }
            ENDCG
        }
    }
}
