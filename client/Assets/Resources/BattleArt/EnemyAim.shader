Shader "SanGuo/EnemyAim"
{
    Properties { _BaseColor ("Aim Red", Color) = (1,.16,.12,.9) }
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
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float4 color:COLOR; };
            Varyings Vert(Attributes v) { Varyings o;o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.color=v.color;return o; }
            half4 Frag(Varyings i):SV_Target { return _BaseColor*i.color; }
            ENDHLSL
        }
    }
}
