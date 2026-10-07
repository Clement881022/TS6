Shader "CGShader/UIBarTicks"
{
    Properties
    {
        [PerRendererData]_MainTex ("Base (RGB)", 2D) = "white" { }
        [HideInInspector]_Color ("Bar Color", Color) = (0,1,0,1)
        _TickColor ("Tick Color", Color) = (1,1,1,1)
        _TickCount ("Tick Count", Range(1, 1000)) = 20
        _TickWidth ("Tick Width", Range(0.001, 1)) = 0.01
        [HideInInspector]_StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector]_Stencil ("Stencil ID", Float) = 0
        [HideInInspector]_StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector]_StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector]_StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector]_ColorMask ("Color Mask", Float) = 15
        [HideInInspector][Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

   SubShader
    {
        Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }
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
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            struct appdata
            {
                float4 vertex: POSITION;
                float2 uv: TEXCOORD0;
                float4 color: COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 uv: TEXCOORD0;
                float4 worldPosition: TEXCOORD1;
                float4 color: COLOR;
                float4 vertex: SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float4 _TickColor;
            int _TickCount;
            float _TickWidth;

            v2f vert(appdata v)
            {
				v2f o;
				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
					
				o.worldPosition = v.vertex;
				o.vertex = UnityObjectToClipPos(o.worldPosition);
				o.uv.xy = TRANSFORM_TEX(v.uv.xy, _MainTex);
				o.uv.zw = float2(0, 0);
				o.color = v.color;
					
				return o;
			}

            fixed4 frag(v2f i): SV_Target
            {
                float2 uv = i.uv.xy;
                half4 baseCol = float4(0, 0, 0, 0);
                
                // 計算刻度線的等距分布
                // 如果有 n 條線，就有 (n+1) 個間距（包括兩端）
                float totalSpaces = _TickCount + 1.0;
                float tickLine = 0.0;
                // 遍歷每條刻度線，檢查當前位置是否接近任一刻度線
                for (int i = 0; i < int(_TickCount); i++)
                {
                    // 第 i 條線的位置（從 1 開始，確保兩端等距）
                    float linePosition = (float(i) + 1.0) / totalSpaces;
                    float distanceToLine = abs(uv.x - linePosition);
                    float lineWidth = _TickWidth;
                    if (distanceToLine < lineWidth)
                    {
                        float lineIntensity = smoothstep(lineWidth, 0, distanceToLine);
                        tickLine = max(tickLine, lineIntensity);
                    }
                }
                
                baseCol = _TickColor * saturate(tickLine);

                return baseCol;
            }
            ENDCG
        }
    }
}
