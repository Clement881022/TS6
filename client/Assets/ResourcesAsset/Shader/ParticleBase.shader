Shader "CGShader/ParticleBase"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        [HDR]_TintColor ("Tint Color", Color) = (1,1,1,1)
        _BlackClip ("Black Clip Threshold", Range(0,1)) = 0.5
        [Toggle]_BlackBGCompensate ("Black BG Compensate (HDR)", Float) = 0
        _UseAlpha ("Use Alpha", Float) = 0
        _Alpha ("Alpha", Range(0,1)) = 1
        _MaskTex ("Mask Texture", 2D) = "white" {}
        _ClipTex ("Clip (Dissolve) Texture", 2D) = "white" {}
        _Clip ("Clip Threshold", Range(0,1)) = 0.5
        _GradientColorA ("Gradient Color A", Color) = (1,1,1,1)
        _GradientColorB ("Gradient Color B", Color) = (0,1,1,1)
        _GradientColorC ("Gradient Color C", Color) = (1,0,0,1)
        _GradientUorV ("Gradient U(0)/V(1)", Range(0,1)) = 0
        _GradientBlend ("Gradient Blend", Range(0,1)) = 1
        _PolarUV ("Use Polar UV", Float) = 0
        _FlowSpeed ("Flow Speed", Vector) = (0, 0, 0, 0)
        [HideInInspector]_SoftParticles ("Soft Particles", Range(0.01,2)) = 0.5
        _TwistMaskTex ("Twist Mask Texture", 2D) = "white" {}
        _TwistMaskStrength ("Twist Mask Strength", Range(0,2)) = 1
        _MaskBlendStage ("Mask Blend Stage", Range(-1,1)) = 0
        _MaskClipThreshold ("Mask Blend ClipTex", Range(0,1)) = 0
        _MaskNullAlpha ("Mask Null Alpha", Float) = 0
        _InvertMask ("Invert Mask", Float) = 0

        _UseTwistUV ("Use Twist UV", Float) = 1
        _TwistUVSpeed ("Twist UV Speed", Vector) = (0.2, 0.3, 0, 0)
        _TwistUVIntensity ("Twist UV Intensity", Range(0,1)) = 0.1

        [Enum(Off,0,On,1)]_ZWriteMode("Z Write Mode", Float) = 0
		[Enum(UnityEngine.Rendering.CompareFunction)]_ZTestMode("Z Test Mode", Float) = 4

        [HideInInspector]_StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector]_Stencil ("Stencil ID", Float) = 0
        [HideInInspector]_StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector]_StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector]_StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector]_ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        LOD 0
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }

        Pass
        {
            Name "Forward"
			Tags { "LightMode"="UniversalForwardOnly" }

            Stencil
			{
				Ref[_Stencil]
				Comp[_StencilComp]
				Pass[_StencilOp]
				ReadMask[_StencilReadMask]
				WriteMask[_StencilWriteMask]
			}

			Blend SrcAlpha OneMinusSrcAlpha
			ZWrite [_ZWriteMode]
			ZTest [_ZTestMode]
			Offset 0,0
			ColorMask RGBA

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ SOFTPARTICLES_ON
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _REQUIRE_OPAQUE_TEXTURE
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            TEXTURE2D(_MainTex);        SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex);        SAMPLER(sampler_MaskTex);
            TEXTURE2D(_ClipTex);        SAMPLER(sampler_ClipTex);
            TEXTURE2D(_TwistMaskTex);   SAMPLER(sampler_TwistMaskTex);
            TEXTURE2D(_DistortTex);     SAMPLER(sampler_DistortTex);

            float4 _MainTex_ST, _MaskTex_ST, _ClipTex_ST, _TwistMaskTex_ST;
            float4 _TintColor;
            float _Alpha, _Clip, _InvertMask, _MaskClipThreshold;
            float4 _GradientColorA, _GradientColorB, _GradientColorC;
            float _GradientUorV, _GradientBlend, _PolarUV;
            float4 _FlowSpeed;
            float _TwistMaskStrength;
            float _SoftParticles;
            float _MaskBlendStage, _MaskNullAlpha;
            float _BlackClip;
            float _BlackBGCompensate;
            float _UseAlpha;
            float _UseTwistUV;
            float4 _TwistUVSpeed;
            float _TwistUVIntensity;

            float _DistortStrength;
            float4 _DistortSpeed;

            #if defined(REQUIRES_OPAQUE_TEXTURE)
                TEXTURE2D_X(_CameraOpaqueTexture);
                SAMPLER(sampler_CameraOpaqueTexture);
            #endif


            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float4 screenPos : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
				UNITY_VERTEX_OUTPUT_STEREO
            };

            float3 SampleTriGradient(float t, float3 a, float3 b, float3 c)
            {
                if (t < 0.5)
                    return lerp(a, b, t * 2);
                else
                    return lerp(b, c, (t - 0.5) * 2);
            }
            #define UNITY_PI 3.14159265359

            float2 GetPolarUV(float2 uv)
            {
                float2 centered = uv - float2(0.5, 0.5);
                float r = length(centered) * 2;
                
                // Metal安全的atan2計算
                float theta = atan2(centered.y, centered.x);
                // 正規化到0-1範圍，避免精度問題
                theta = theta * 0.159154943 + 0.5; // 1/(2*PI) ≈ 0.159154943
                
                return float2(r, theta);
            }

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(v.positionOS);
                o.uv = v.uv;
                o.color = v.color * _TintColor;
                o.screenPos = ComputeScreenPos(o.positionCS);
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                // Metal安全的時間計算 - 防止長時間運行溢出
                float safeTime = fmod(_Time.y, 6283.185); // 限制在2π範圍內
                
                // 流動動畫
                float2 flow = _FlowSpeed.xy * safeTime;
                float2 uv = i.uv + flow;

                // Twist UV 擾動
                if (_UseTwistUV > 0.5)
                {
                    float2 uvTwist = TRANSFORM_TEX(uv + safeTime * _TwistUVSpeed.xy, _TwistMaskTex);
                    float2 twistOffset = SAMPLE_TEXTURE2D(_TwistMaskTex, sampler_TwistMaskTex, uvTwist).rg - 0.5;
                    uv += twistOffset * _TwistMaskStrength * _TwistUVIntensity;
                }

                // 極座標切換
                float2 gradUV = lerp(uv, GetPolarUV(uv), step(0.5, _PolarUV));

                // 主紋理
                float2 uvMain = TRANSFORM_TEX(uv, _MainTex);
                float4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uvMain) * i.color;


                // 遮罩 UV 計算
                float2 uv_MaskTex = i.uv * _MaskTex_ST.xy + _MaskTex_ST.zw;

                // 遮罩取樣
                float maskSample = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, uv_MaskTex).r;

                // 遮罩反轉
                float maskValue = lerp(maskSample, 1.0 - maskSample, _InvertMask);
                float maskBlend = saturate(maskValue + _MaskBlendStage);
                float maskFinal = lerp(maskValue, maskBlend, _MaskClipThreshold);
                float maskAlpha = lerp(maskFinal, 1.0, _MaskNullAlpha);

                // TwistMask 取樣
                float2 uvTwistMask = TRANSFORM_TEX(uv, _TwistMaskTex);
                float twistMask = SAMPLE_TEXTURE2D(_TwistMaskTex, sampler_TwistMaskTex, uvTwistMask).r * _TwistMaskStrength;

                // Clip（溶解）
                float2 uvClip = TRANSFORM_TEX(uv, _ClipTex);
                float clipVal = SAMPLE_TEXTURE2D(_ClipTex, sampler_ClipTex, uvClip).r;
                clip(clipVal * twistMask - _Clip);

                // 三色漸層混色
                float t = lerp(gradUV.x, gradUV.y, _GradientUorV);
                t = saturate(t + twistMask * 0.5 - 0.25);
                float3 grad = SampleTriGradient(t, _GradientColorA.rgb, _GradientColorB.rgb, _GradientColorC.rgb);
                col.rgb = lerp(col.rgb, col.rgb * grad, _GradientBlend);

                // BlackClip: 取主紋理灰階
                float gray = dot(col.rgb, float3(0.299, 0.587, 0.114));
                // 若 _UseAlpha==1，直接用 col.a，否則用 gray 與 _BlackClip 比較
                float blackAlpha = lerp(saturate((gray - _BlackClip) / max(1e-5, 1.0 - _BlackClip)), col.a, _UseAlpha);

                // Black BG Compensate: RGB 除以 alpha 補回視覺亮度損失
                // 需要 HDR Render Target，否則超過 1.0 的像素會 clip
                col.rgb /= lerp(1.0, max(blackAlpha, 0.001), _BlackBGCompensate);
                col.a *= _Alpha * maskAlpha * blackAlpha;

                return col;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}