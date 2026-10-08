Shader "SanGuo/ToonLit"
{
    Properties
    {
        _Color ("Color", Color) = (1,1,1,1)
        _ShadowTint ("Shadow Tint", Color) = (.68,.67,.63,1)
        _RimStrength ("Rim Strength", Range(0,1)) = .08
        _OutlineWidth ("Legacy Outline Width", Float) = 0
        _BaseMap ("Shadow Alpha", 2D) = "white" {}
        _BaseColor ("Shadow Color", Color) = (1,1,1,1)
        _Cutoff ("Shadow Cutoff", Float) = .5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "UniversalForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Color, _ShadowTint;
                float _RimStrength, _OutlineWidth;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 normalWS:TEXCOORD0; float3 positionWS:TEXCOORD1; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(v.normalOS);
                return o;
            }
            half4 Frag(Varyings i):SV_Target
            {
                Light key=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float3 n=normalize(i.normalWS);
                float3 view=normalize(GetWorldSpaceViewDir(i.positionWS));
                float3 studioLight=normalize(float3(view.x-.25,.85,view.z+.15));
                float ndl=saturate(dot(n,studioLight));
                float steps=smoothstep(.08,.38,ndl)*.55+smoothstep(.48,.80,ndl)*.45;
                float lightFactor=steps*lerp(.72,1,key.shadowAttenuation);
                float3 color=lerp(_Color.rgb*_ShadowTint.rgb,_Color.rgb,lightFactor);
                float rim=pow(1-saturate(dot(n,view)),3);
                color+=rim*_RimStrength*.5;
                color+=pow(saturate(dot(n,normalize(studioLight+view))),32)*.045;
                return half4(color,1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
    }
}
