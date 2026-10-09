Shader "SanGuo/CharacterSurface"
{
    Properties
    {
        _BaseMap ("Painted surface", 2D) = "white" {}
        _BaseColor ("Surface tint", Color) = (1,1,1,1)
        _Smoothness ("Smoothness", Range(0,1)) = .38
        _MetalStrength ("Gold response", Range(0,1)) = .45
        _ReliefStrength ("Painted gold relief", Range(0,2)) = .6
        _Cutoff ("Alpha cutoff", Range(0,1)) = .08
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
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
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST, _BaseMap_TexelSize, _BaseColor;
                float _Smoothness, _MetalStrength, _Cutoff, _ReliefStrength;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float4 tangentOS:TANGENT; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; float4 tangentWS:TEXCOORD3; };
            Varyings Vert(Attributes v)
            {
                Varyings o;
                o.positionWS=TransformObjectToWorld(v.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                o.normalWS=TransformObjectToWorldNormal(v.normalOS);
                o.tangentWS=float4(TransformObjectToWorldDir(v.tangentOS.xyz),v.tangentOS.w*GetOddNegativeScale());
                o.uv=TRANSFORM_TEX(v.uv,_BaseMap);
                return o;
            }
            float Relief(float3 paint)
            {
                float mask=saturate((paint.r-paint.b)*3)*saturate((paint.g-paint.b)*2);
                return dot(paint,float3(.30,.59,.11))*mask;
            }
            half4 Frag(Varyings i):SV_Target
            {
                half4 paint=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor;
                // The source atlas alpha is a tint mask, not mesh transparency.
                float3 n=normalize(i.normalWS), v=normalize(GetWorldSpaceViewDir(i.positionWS));
                if(_ReliefStrength>.001 && dot(i.tangentWS.xyz,i.tangentWS.xyz)>.01)
                {
                    float2 delta=_BaseMap_TexelSize.xy*2;
                    float du=Relief(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv+float2(delta.x,0)).rgb)-Relief(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv-float2(delta.x,0)).rgb);
                    float dv=Relief(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv+float2(0,delta.y)).rgb)-Relief(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv-float2(0,delta.y)).rgb);
                    float3 tangent=normalize(i.tangentWS.xyz),bitangent=normalize(cross(n,tangent))*i.tangentWS.w;
                    n=normalize(n-(tangent*du+bitangent*dv)*_ReliefStrength);
                }
                Light key=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                // Stable studio fill preserves the painted face in both the board and portrait camera.
                float3 fill=normalize(float3(v.x-.45,.75,v.z+.30));
                float diffuse=smoothstep(-.20,.85,dot(n,fill));
                float visibility=lerp(.70,1,key.shadowAttenuation);
                float3 color=paint.rgb*(.56+.44*diffuse)*visibility;
                float gold=saturate((paint.r-paint.b)*2.8)*saturate((paint.g-paint.b)*2.1);
                float spec=pow(saturate(dot(n,normalize(v+fill))),lerp(16,80,_Smoothness));
                color+=spec*lerp(float3(.05,.06,.07),float3(.68,.43,.16),gold)*_MetalStrength;
                float rim=pow(1-saturate(dot(n,v)),4);
                color+=rim*float3(.065,.070,.075)*diffuse;
                return half4(color,1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
