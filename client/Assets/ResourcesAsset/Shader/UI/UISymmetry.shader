Shader "CGShader/UI/UISymmetry"
{
    Properties
    {
        [PerRendererData]_MainTex ("Base (RGB)", 2D) = "white" { }
        [HDR]_Color ("EdgeColor", Color) = (1, 1, 1, 1)
        
       [HideInInspector]_StencilComp ("Stencil Comparison", Float) = 8
       [HideInInspector]_Stencil ("Stencil ID", Float) = 0
       [HideInInspector]_StencilOp ("Stencil Operation", Float) = 0
       [HideInInspector]_StencilWriteMask ("Stencil Write Mask", Float) = 255
       [HideInInspector]_StencilReadMask ("Stencil Read Mask", Float) = 255
        
       [HideInInspector]_ColorMask ("Color Mask", Float) = 15

        [KeywordEnum(FourSide,Horizontal,Vertical)] _Direction ("Direction", Float) = 1
        
        
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
            
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #pragma multi_compile _DIRECTION_FOURSIDE _DIRECTION_HORIZONTAL _DIRECTION_VERTICAL

            struct appdata
            {
                float4 vertex: POSITION;
                float2 uv: TEXCOORD0;
                float2 uv1: TEXCOORD1;
                float2 uv2: TEXCOORD2;
                float4 color: COLOR;
                
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct v2f
            {
                float4 uv: TEXCOORD0;
                float2 uv1: TEXCOORD1;
                float2 uv2: TEXCOORD2;
                float4 worldPosition: TEXCOORD4;
                float4 color: COLOR;
                float4 vertex: SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            sampler2D _MainTex;            
            float4 _MainTex_ST;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            

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
                o.color = v.color * _Color;
                return o;
            }
 
            fixed4 frag(v2f i): SV_Target
            {

                float2 uv = i.uv.xy;

               #ifdef _DIRECTION_FOURSIDE
                    uv = 1 - abs(2 * (uv - 0.5f));
                #endif

                #ifdef _DIRECTION_HORIZONTAL
                    uv.x = 1 - abs(2 * (uv.x - 0.5f));
                #endif
                
                #ifdef _DIRECTION_VERTICAL
                    uv.y = 1 - abs(2 * (uv.y - 0.5f));
                #endif

                half4 col = tex2D(_MainTex, uv);
                
                #ifdef UNITY_UI_ALPHACLIP
                    clip(col.a - 0.001);
                #endif

                #ifdef UNITY_UI_CLIP_RECT
                    col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
                #endif

                col *= i.color;

                return col;
            }
            ENDCG
            
        }
    }
}