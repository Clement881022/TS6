Shader "SanGuo/ProductionBoardSurface"
{
    Properties
    {
        _BaseColor("Base color",Color)=(.86,.82,.70,1)
        _Grain("Stone grain",Range(0,.12))=.025
        _Metal("Metal",Range(0,1))=0
    }
    SubShader
    {
        Tags{"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"}
        Pass
        {
            Tags{"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Input{float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;};
            struct Varying{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;};
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor;float _Grain;float _Metal;
            CBUFFER_END
            float Hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
            float Noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);}
            Varying Vert(Input v){Varying o;o.positionWS=TransformObjectToWorld(v.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.uv=v.uv;return o;}
            half4 Frag(Varying i):SV_Target
            {
                float3 n=normalize(i.normalWS),l=normalize(float3(-.42,.86,.28));
                float broad=Noise(i.positionWS.xz*3.5),fine=Noise(i.positionWS.xz*46);
                float vein=smoothstep(.55,.75,Noise(i.positionWS.xz*float2(5,13)+broad));
                float grain=(broad-.5)*.75+(fine-.5)*.25-vein*.2;
                float diffuse=.57+.43*saturate(dot(n,l));
                float3 view=normalize(_WorldSpaceCameraPos-i.positionWS),h=normalize(l+view);
                float spec=pow(saturate(dot(n,h)),28)*_Metal*.18;
                float3 color=(_BaseColor.rgb+grain*_Grain)*diffuse+spec*float3(1,.88,.61);
                float stone=(1-step(.05,_Metal))*smoothstep(.75,.95,n.y);
                float2 edge=min(i.uv,1-i.uv);float e=min(edge.x,edge.y);
                float outer=smoothstep(.006,.050,e);
                float2 p=min(frac(i.uv*2),1-frac(i.uv*2));
                float seam=(1-smoothstep(.003,.013,min(p.x,p.y)))*smoothstep(.015,.055,e);
                color-=stone*(seam*float3(.045,.04,.033)+(1-outer)*float3(.05,.045,.032));
                float cornerLine=max((1-smoothstep(.0015,.004,abs(edge.x-.043)))*step(edge.y,.15)*step(.043,edge.y),(1-smoothstep(.0015,.004,abs(edge.y-.043)))*step(edge.x,.15)*step(.043,edge.x));
                color=lerp(color,float3(.62,.47,.24)*diffuse,cornerLine*stone*.5);
                return half4(color,1);
            }
            ENDHLSL
        }
    }
}
