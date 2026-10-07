Shader "CGShader/UI/UIShapeMask"
{
    Properties
    {
       [PerRendererData]_MainTex ("Base (RGB)", 2D) = "white" { }

       [HideInInspector]_StencilComp ("Stencil Comparison", Float) = 8
       [HideInInspector]_Stencil ("Stencil ID", Float) = 0
       [HideInInspector]_StencilOp ("Stencil Operation", Float) = 0
       [HideInInspector]_StencilWriteMask ("Stencil Write Mask", Float) = 255
       [HideInInspector]_StencilReadMask ("Stencil Read Mask", Float) = 255
       [HideInInspector]_ColorMask ("Color Mask", Float) = 15

       [Header(ShapeMask)]
       [Space(15)]
       [KeywordEnum(Smooth,Solid)] _EdgeStyle ("EdgeStyle", Float) = 1
       [HDR]_Color ("EdgeColor", Color) = (1, 1, 1, 1)
       _MaskClip ("MaskClip", Float) = 0.01
       _EdgeLength ("EdgeLength", Float) = 0.0
       _PivotScale("pivotScale", Float) = 1.95
       _PivotXY ("pivotXY", Vector) = (0.5, 0.5, 1, 1)

       [Header(GrayScale)]
       [Space(15)]
       [KeywordEnum(Horizontal,Vertical,Radial)] _GrayStyle ("GrayStyle", Float) = 2
       [KeywordEnum(Reverse,Positive)] _GrayDirection ("GrayDirection",Float) = 1

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
            #pragma target 2.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "Assets/ResourcesAsset/Shader/ShaderLibrary/GrayScale.hlsl"

            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #pragma multi_compile _EDGESTYLE_SMOOTH _EDGESTYLE_SOLID
            #pragma multi_compile _GRAYSTYLE_HORIZONTAL _GRAYSTYLE_VERTICAL _GRAYSTYLE_RADIAL
            #pragma multi_compile _GRAYDIRECTION_REVERSE _GRAYDIRECTION_POSITIVE

            struct appdata
            {
                float4 vertex: POSITION;
                float2 uv: TEXCOORD0;
                float2 uv1: TEXCOORD1;
                float2 uv2: TEXCOORD2;
                float2 uv3: TEXCOORD3;
                float4 color: COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 uv: TEXCOORD0;
                float2 uv1: TEXCOORD1;
                float2 uv2: TEXCOORD2;
                float2 uv3: TEXCOORD3;
                float4 worldPosition: TEXCOORD4;
                half4  mask : TEXCOORD5;
                float4 color: COLOR;
                float4 vertex: SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float4 _ClipRect;
            fixed _EdgeLength;
            fixed _MaskClip;
            float2 _PivotXY;
            float _PivotScale;
            float4 _MainTex_TexelSize;
            float _UIMaskSoftnessX;
            float _UIMaskSoftnessY;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.worldPosition = v.vertex;
                o.vertex = UnityObjectToClipPos(o.worldPosition);
                o.uv.xy = TRANSFORM_TEX(v.uv.xy, _MainTex);
                o.uv.zw = v.uv.xy;
                o.uv1 = v.uv1;
                o.uv2 = v.uv2;
                o.uv3 = v.uv3;

                float2 pixelSize = o.vertex.w;
                pixelSize /= float2(1, 1) * abs(mul((float2x2)UNITY_MATRIX_P, _ScreenParams.xy));

                float4 clampedRect = clamp(_ClipRect, -2e10, 2e10);
                float2 maskUV = (v.vertex.xy - clampedRect.xy) / (clampedRect.zw - clampedRect.xy);
                o.mask = half4(v.vertex.xy * 2 - clampedRect.xy - clampedRect.zw, 0.25 / (0.25 * half2(_UIMaskSoftnessX, _UIMaskSoftnessY) + abs(pixelSize.xy)));

                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i): SV_Target
            {
               half4 col =  GrayScaleMask(_MainTex,_PivotXY,_MainTex_TexelSize,i.uv,i.uv1.x,i.uv2.y,i.uv1.y,i.uv3.x);
               half2 polarCoord = atan2(i.uv.x - col.x, _PivotScale - i.uv.y - col.y) * 0.3183098861928886;
               half2 centerUV = (i.uv - _PivotXY) * 2.0;
               half sphereMask = length(centerUV);
               polarCoord.y = sphereMask;

                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(i.mask.xy)) * i.mask.zw);
                col.a *= m.x * m.y;
                #endif

               #ifdef UNITY_UI_ALPHACLIP
                    clip(col.a - 0.001);
               #endif

               #ifdef _EDGESTYLE_SMOOTH
               half alphaFade = smoothstep(_MaskClip + i.uv2.x - _EdgeLength, _MaskClip, _PivotScale - sphereMask);
               half alphaHard = smoothstep(_MaskClip + i.uv2.x - _EdgeLength * _PivotXY, _MaskClip, _PivotScale - sphereMask);
               half edge = saturate((alphaFade - alphaHard) * 10);
               col.a *= alphaFade * i.color.a;
               col.rgb = lerp(col.rgb, i.color.rgb, edge);
               #endif

               #ifdef _EDGESTYLE_SOLID
               half alphaFade = step(_MaskClip + i.uv2.x - _EdgeLength, _PivotScale - sphereMask);
               half alphaHard = step(_MaskClip + i.uv2.x - _EdgeLength * _PivotXY, _PivotScale - sphereMask);
               half edge = saturate((alphaFade - alphaHard) * 2.0);
               col.a *= alphaFade * i.color.a;
               col.rgb = lerp(col.rgb, i.color.rgb, edge);
               #endif

               return col;
            }
            ENDCG
        }
    }
}