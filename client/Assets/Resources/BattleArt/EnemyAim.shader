Shader "SanGuo/EnemyAim"
{
    Properties
    {
        _BaseColor ("Aim Red", Color) = (1,.16,.12,.9)
        _DashRepeat ("Dashes Per UV", Float) = 6
        _DashDuty ("Dash Coverage", Range(0,1)) = .55
        _BlinkSpeed ("Blink Cycles Per Second", Float) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off ZWrite Off ZTest LEqual
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _DashRepeat, _DashDuty, _BlinkSpeed;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            Varyings Vert(Attributes v) { Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.color=v.color;o.uv=v.uv;return o; }
            half4 Frag(Varyings i):SV_Target
            {
                clip(_DashDuty-frac(i.uv.x*_DashRepeat));
                half4 color=_BaseColor*i.color;
                color.a*=lerp(.25,1,.5+.5*sin(_Time.y*6.2831853*_BlinkSpeed));
                return color;
            }
            ENDHLSL
        }
    }
}
