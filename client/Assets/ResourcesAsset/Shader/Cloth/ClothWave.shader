// =============================================================================
//  CGShader/ClothWave
//  URP 3D 布料飄動 Shader — Cloth Wave Simulation
// -----------------------------------------------------------------------------
//  原理 Principle:
//    頂點著色器中對 Noise 貼圖做 tex2Dlod 採樣，取代多層正弦波疊加。
//    GPU 頂點 texture fetch 比三角函式計算便宜，適合手機。
//    Noise R channel → 沿風向主要位移；G channel → Y 軸晃動。
//    Vertex-stage tex2Dlod on a noise texture replaces sine-wave math.
//    Noise R → lateral wind displacement; G → vertical sway.
//
//  UV 約定 UV Convention:
//    _FixedUMin / _FixedUMax — 固定 UV.x=0 / UV.x=1 的邊緣（左 / 右）
//    _FixedVMin / _FixedVMax — 固定 UV.y=0 / UV.y=1 的邊緣（下 / 上）
//    各參數範圍 0~1：0=不固定，1=完全固定。可同時啟用多條邊。
//    Range 0–1: 0=free, 1=fully pinned. Multiple edges can be active at once.
//
//  法線更新 Normal Update:
//    Finite-difference 方法在頂點著色器中解析位移梯度，
//    重新計算世界空間法線以獲得正確光照。
//    Analytical finite-difference in vertex shader reconstructs world normals.
//
//  渲染特性 Render Features:
//    • 雙面渲染（正/背面自動翻轉法線）Double-sided with back-face normal flip.
//    • 陰影投射 pass 套用相同位移，影子形狀正確。
//      Shadow-caster pass applies identical displacement for accurate shadows.
//    • 支援 SRP Batcher。SRP Batcher compatible.
// =============================================================================

Shader "CGShader/ClothWave"
{
    Properties
    {
        [Header(________________________Base________________________)][Space(15)]
        _BaseMap ("基礎貼圖 Base Map", 2D) = "white" {}
        [HDR]_BaseColor ("基礎染色 Base Color", Color) = (1,1,1,1)
        _NormalMap ("法線貼圖 Normal Map", 2D) = "bump"{}
        _NormalIntensity ("法線強度 Normal Intensity", Range(0,2))   = 1.0
        _Smoothness ("平滑度 Smoothness", Range(0,1)) = 0.3
        _Metallic ("金屬感 Metallic", Range(0,1)) = 0.0
        _AlphaCutoff ("裁切閾值 Alpha Cutoff", Range(0,1)) = 0.1

        [Header(________________________Wind________________________)][Space(15)]
        _NoiseMap ("飄動 Noise 圖 Noise Map", 2D) = "white" {}
        _NoiseScale ("Noise 縮放 Noise Scale", Float) = 1.0
        _WaveUVDir ("波浪UV方向 Wave UV Dir (XY)", Vector) = (0,1,0,0)
        _WaveFlipU ("搖擺模式 Sway Mode (0=S曲線駐波(法線) / 1=行進波(切線))", Range(0,1)) = 0
        _WindSpeed ("風速 Wind Speed", Float) = 1.5
        _WindStrength ("飄起強度 Billow Strength", Float) = 0.15
        _SwayStrength ("搖擺強度 Sway Strength", Range(0,1)) = 0.05

        [Header(________________________Cloth________________________)][Space(15)]
        _ClothGravity ("布料重力偏垂 Cloth Gravity", Range(-0.5,0.5))  = 0.05
        _FixedUMin ("固定左緣(UV.x=0) Fix U Min", Range(0,1)) = 0.0
        _FixedUMax ("固定右緣(UV.x=1) Fix U Max", Range(0,1)) = 0.0
        _FixedVMin ("固定下緣(UV.y=0) Fix V Min", Range(0,1)) = 0.0
        _FixedVMax ("固定上緣(UV.y=1) Fix V Max", Range(0,1)) = 1.0

        [Header(________________________Render________________________)][Space(15)]
        [Enum(UnityEngine.Rendering.CullMode)]  _Cull ("背面剔除 Cull", Float) = 0.0
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 1.0
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 0.0
        [Enum(Off, 0, On, 1)] _ZWrite   ("ZWrite", Float) = 1.0
    }

    SubShader
    {
        Tags
        {"RenderType"= "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue"= "Geometry"}

        // =====================================================================
        // Pass 1 — ForwardLit:  PBR 光照 + 布料位移
        //           PBR lighting with cloth vertex displacement
        // =====================================================================
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull   [_Cull]
            Blend  [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]

            HLSLPROGRAM
            #pragma vertex   ClothVert
            #pragma fragment ClothFrag

            // target 3.0 = 手機 OpenGL ES 3.0 / Metal / Vulkan 最低需求
            #pragma target 3.0

            // 削減 Shader 變體：不接收陰影，附加光只在 frag 展開，支援 GPU instancing
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile_instancing

            // 布料不接收陰影（包含自陰影），URP Lighting.hlsl 內部會檢查此定義並跤過 shadow sampling
            // Cloth does not receive shadows; URP checks this define internally and skips shadow sampling
            #define _RECEIVE_SHADOWS_OFF
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            // ── Textures ──────────────────────────────────────────────────
            TEXTURE2D(_BaseMap);   SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap); SAMPLER(sampler_NormalMap);
            TEXTURE2D(_NoiseMap);  SAMPLER(sampler_NoiseMap);

            // ── Per-material constants (SRP Batcher 相容) ─────────────────
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _NormalMap_ST;
                float4 _NoiseMap_ST;
                float4 _BaseColor;
                float  _NormalIntensity;
                float  _Smoothness;
                float  _Metallic;
                float  _AlphaCutoff;
                float4 _WaveUVDir;
                float  _WaveFlipU;
                float  _WindSpeed;
                float  _WindStrength;
                float  _SwayStrength;
                float  _NoiseScale;
                float  _ClothGravity;
                float  _FixedUMin;
                float  _FixedUMax;
                float  _FixedVMin;
                float  _FixedVMax;
            CBUFFER_END

            // ── 頂點/片段結構 Vertex / Fragment structs ───────────────────
            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float2 lightmapUV : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 uvNormal : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float3 normalWS : TEXCOORD3;
                float3 tangentWS : TEXCOORD4;
                float3 bitangentWS : TEXCOORD5;
                DECLARE_LIGHTMAP_OR_SH(lightmapUV, vertexSH, 6);
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ── 頂點著色器 Vertex Shader ──────────────────────────────────
            Varyings ClothVert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                // 四邊固定遮罩：每個 pin 只影響自己這半邊，兩側同時固定時中間自由度=1
                // Each pin affects only its own half → both ends pinned gives freedom=1 at center
                float2 uv0 = IN.uv;
                float pinnedU = max(_FixedUMin * saturate(1.0 - uv0.x * 2.0),_FixedUMax * saturate(1.0 - (1.0 - uv0.x) * 2.0));
                float pinnedV = max(_FixedVMin * saturate(1.0 - uv0.y * 2.0), _FixedVMax * saturate(1.0 - (1.0 - uv0.y) * 2.0));
                float rawFreedom = (1.0 - saturate(pinnedU)) * (1.0 - saturate(pinnedV));
                float freedom = rawFreedom * rawFreedom * (3.0 - 2.0 * rawFreedom); // smoothstep

                float3 posWS0 = TransformObjectToWorld(IN.positionOS.xyz);

                // 先建立法線與切線框架（位移方向與 FD 法線重建均需要）
                float3 origNormalWS = TransformObjectToWorldNormal(IN.normalOS);
                float3 meshTangentWS = normalize(TransformObjectToWorldDir(IN.tangentOS.xyz));
                float  tangentSign = IN.tangentOS.w * GetOddNegativeScale();
                float3 meshBitangWS = normalize(cross(origNormalWS, meshTangentWS) * tangentSign);

                // ── Noise 圖飄動位移 ───────────────────────────────────────
                float  tRaw = _Time.y * _WindSpeed;
                // 對時間取模，避免大數值導致 sin() 相位精度損失（float32 只有 ~7 位有效數字）
                // Wrap to [0, 2π] to prevent float32 precision loss in sin() phase at large _Time.y
                float  t = fmod(tRaw, TWO_PI);
                float  noiseScale = abs(_NoiseScale);
                // Noise UV 用原始時間（連續捲動），sin 波用取模後的時間（精度安全）
                // Noise UV uses raw time (continuous scroll); sine waves use wrapped time (precision-safe)
                float2 scrollUV = uv0 * noiseScale + _WaveUVDir.xy * tRaw;

                float2 noiseVal = SAMPLE_TEXTURE2D_LOD(_NoiseMap, sampler_NoiseMap, scrollUV, 0).rg;
                float  noiseR = noiseVal.r * 2.0 - 1.0; // R: billow

                // V-pin 偵測：UV.y 邊緣 pin 量大於 UV.x 時切換 Y 軸飄動模式
                float pinU = saturate(_FixedUMin + _FixedUMax);
                float pinV = saturate(_FixedVMin + _FixedVMax);
                float useV = step(pinU + 0.001, pinV);

                // 預算共用系數，避免重複相乘
                float modeNorm  = 1.0 - _WaveFlipU;
                float modeTang  = _WaveFlipU;
                float swayFree  = _SwayStrength * freedom;
                float phase = uv0.x * PI;

                float  standWave = sin(TWO_PI * uv0.x - t) * swayFree;
                float  sinSway = sin(t - phase) * swayFree;

                float3 disp  = origNormalWS * (noiseR * _WindStrength * freedom);
                disp += origNormalWS * (standWave * modeNorm * (1.0 - useV));
                disp.y += sinSway * modeNorm * useV;
                disp += meshTangentWS * (sinSway * modeTang);
                disp.y -= _ClothGravity * freedom;

                float3 posWS = posWS0 + disp;

                // ── Finite-Difference 法線重建 ─────────────────────────────
                // 使用 UV 空間偏移採樣（不用世界 XZ 投影，對垂直旗幟也正確）
                // UV-space offsets — correct for any cloth orientation (XZ proj fails on vertical flags)
                const float eps    = 0.02;  // world-space position step
                const float eps_uv = 0.02;  // UV-space noise sampling step

                float3 posT0 = posWS0 + meshTangentWS * eps;
                float3 posB0 = posWS0 + meshBitangWS  * eps;

                float2 uvT = (uv0 + float2(eps_uv, 0.0)) * noiseScale + _WaveUVDir.xy * t;
                float2 uvB = (uv0 + float2(0.0, eps_uv)) * noiseScale + _WaveUVDir.xy * t;
                // FD 鄰點只需要 R 通道（billow 方向）
                float  noiseTr = SAMPLE_TEXTURE2D_LOD(_NoiseMap, sampler_NoiseMap, uvT, 0).r * 2.0 - 1.0;
                float  noiseBr = SAMPLE_TEXTURE2D_LOD(_NoiseMap, sampler_NoiseMap, uvB, 0).r * 2.0 - 1.0;

                // T 鄰點：uv.x + eps_uv，波浪相位也偏移
                float  phaseT = (uv0.x + eps_uv) * PI;
                float  standWaveT = sin(TWO_PI * (uv0.x + eps_uv) - t) * swayFree;
                float  sinSwT = sin(t - phaseT) * swayFree;
                float3 dispT  = origNormalWS * (noiseTr * _WindStrength * freedom);
                dispT += origNormalWS * (standWaveT * modeNorm * (1.0 - useV));
                dispT.y += sinSwT * modeNorm * useV;
                dispT += meshTangentWS * (sinSwT * modeTang);
                dispT.y -= _ClothGravity * freedom;

                // B 鄰點：uv.y + eps_uv，uv.x 不變 → standWave/sinSway 和主點相同，直接重用
                float3 dispB  = origNormalWS * (noiseBr * _WindStrength * freedom);
                dispB += origNormalWS * (standWave * modeNorm * (1.0 - useV));
                dispB.y += sinSway * modeNorm * useV;
                dispB += meshTangentWS * (sinSway * modeTang);
                dispB.y -= _ClothGravity * freedom;

                float3 dP_dT = normalize((posT0 + dispT) - posWS);
                float3 dP_dB = normalize((posB0 + dispB) - posWS);
                float3 newNormalWS = normalize(cross(dP_dT, dP_dB));
                newNormalWS *= sign(dot(newNormalWS, origNormalWS));
                // FD 混合係數隨 Billow Strength 自動縮減：billow 越大 noise 法線越不可信
                // Auto-scale FD blend inversely with WindStrength to suppress noise-driven darkening
                float fdBlend = freedom * 0.35 / max(1.0 + abs(_WindStrength) * 2.0, 1.0);
                newNormalWS  = normalize(lerp(origNormalWS, newNormalWS, fdBlend));

                float3 tangentWS   = meshTangentWS;
                float3 bitangentWS = normalize(cross(newNormalWS, tangentWS) * tangentSign);

                OUT.positionHCS = TransformWorldToHClip(posWS);
                OUT.uv = TRANSFORM_TEX(IN.uv,    _BaseMap);
                OUT.uvNormal = TRANSFORM_TEX(IN.uv,    _NormalMap);
                OUT.positionWS = posWS;
                OUT.normalWS = newNormalWS;
                OUT.tangentWS = tangentWS;
                OUT.bitangentWS  = bitangentWS;
                OUTPUT_LIGHTMAP_UV(IN.lightmapUV, unity_LightmapST, OUT.lightmapUV);
                OUTPUT_SH(newNormalWS, OUT.vertexSH);

                return OUT;
            }

            // ── 片段著色器 Fragment Shader ────────────────────────────────
            float4 ClothFrag(Varyings IN, bool isFrontFace : SV_IsFrontFace) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                // 基礎顏色 Base color
                float4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                clip(baseColor.a - _AlphaCutoff);

                // 法線貼圖（切線空間）Normal map (tangent space)
                float3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalMap, sampler_NormalMap, IN.uvNormal), _NormalIntensity);
                float3x3 TBN = float3x3(
                    normalize(IN.tangentWS),
                    normalize(IN.bitangentWS),
                    normalize(IN.normalWS));
                float3 normalWS = normalize(mul(normalTS, TBN));

                // 背面法線翻轉（雙面布料）Back-face normal flip for double-sided cloth
                normalWS *= isFrontFace ? 1.0 : -1.0;

                // ── PBR Surface / Input Data ──────────────────────────────
                SurfaceData surf = (SurfaceData)0;
                surf.albedo = baseColor.rgb;
                surf.metallic = _Metallic;
                surf.smoothness  = _Smoothness;
                surf.normalTS = normalTS;
                surf.occlusion = 1.0;
                surf.alpha = baseColor.a;

                InputData inputData = (InputData)0;
                inputData.positionWS = IN.positionWS;
                inputData.normalWS = normalWS;
                inputData.viewDirectionWS = normalize(GetCameraPositionWS() - IN.positionWS);
                inputData.shadowCoord = float4(0, 0, 0, 0); // 不接收陰影，_RECEIVE_SHADOWS_OFF 已定義
                inputData.fogCoord = 0;
                inputData.vertexLighting = half3(0, 0, 0);
                inputData.bakedGI = SAMPLE_GI(IN.lightmapUV, IN.vertexSH, normalWS);
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(IN.positionHCS);
                inputData.shadowMask = half4(1, 1, 1, 1);

                float4 color = UniversalFragmentPBR(inputData, surf);
                return color;
            }

            ENDHLSL
        }

        // =====================================================================
        // Pass 2 — ShadowCaster:  套用相同頂點位移，使陰影形狀正確
        //           Same displacement applied so shadow matches the cloth silhouette
        // =====================================================================
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest  LEqual
            ColorMask 0
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   ShadowVert
            #pragma fragment ShadowFrag

            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            // Core.hlsl includes Common.hlsl which defines the 'real' type.
            // CommonMaterial.hlsl (which defines LerpWhiteTo used by Shadows.hlsl)
            // must come AFTER Core.hlsl so that 'real' is already declared.
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _NormalMap_ST;
                float4 _NoiseMap_ST;
                float4 _BaseColor;
                float  _NormalIntensity;
                float  _Smoothness;
                float  _Metallic;
                float  _AlphaCutoff;
                float4 _WaveUVDir;
                float  _WaveFlipU;
                float  _WindSpeed;
                float  _WindStrength;
                float  _SwayStrength;
                float  _NoiseScale;
                float  _ClothGravity;
                float  _FixedUMin;
                float  _FixedUMax;
                float  _FixedVMin;
                float  _FixedVMax;
            CBUFFER_END

            // ShadowCaster 位移：R=法線鼓包；Mode0=S曲線駐波(法線)；Mode1=行進波(切線)
            float3 ComputeClothDisplacement(float2 uv, float3 normalWS, float3 tangentWS, float freedom)
            {
                float tRaw = _Time.y * _WindSpeed;
                float t = fmod(tRaw, TWO_PI);
                float noiseScale = abs(_NoiseScale);
                float2 scrollUV = uv * noiseScale + _WaveUVDir.xy * tRaw;
                float2 n = SAMPLE_TEXTURE2D_LOD(_NoiseMap, sampler_NoiseMap, scrollUV, 0).rg * 2.0 - 1.0;
                float3 disp = normalWS * (n.r * _WindStrength * freedom);
                float  pinU_s = saturate(_FixedUMin + _FixedUMax);
                float  pinV_s = saturate(_FixedVMin + _FixedVMax);
                float  useV_s = step(pinU_s + 0.001, pinV_s);
                float standWave  = sin(TWO_PI * uv.x - t) * _SwayStrength * freedom;
                float sinSway = sin(t - uv.x * PI) * _SwayStrength * freedom;
                disp += normalWS * (standWave * (1.0 - _WaveFlipU) * (1.0 - useV_s));
                disp.y += sinSway * (1.0 - _WaveFlipU) * useV_s;
                disp += tangentWS * (sinSway * _WaveFlipU);
                disp.y -= _ClothGravity * freedom;
                return disp;
            }

            // Shadow bias 來源（URP 全域 uniform）
            // Shadow bias source — URP global uniforms
            float3 _LightDirection;
            float3 _LightPosition;

            struct ShadowAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct ShadowVaryings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            ShadowVaryings ShadowVert(ShadowAttributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                ShadowVaryings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float2 uv0 = IN.uv;
                float pinnedU = max(_FixedUMin * saturate(1.0 - uv0.x * 2.0),  _FixedUMax * saturate(1.0 - (1.0 - uv0.x) * 2.0));
                float pinnedV = max(_FixedVMin * saturate(1.0 - uv0.y * 2.0),  _FixedVMax * saturate(1.0 - (1.0 - uv0.y) * 2.0));
                float rawFreedom = (1.0 - saturate(pinnedU)) * (1.0 - saturate(pinnedV));
                float freedom = rawFreedom * rawFreedom * (3.0 - 2.0 * rawFreedom);

                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS  = TransformObjectToWorldNormal(IN.normalOS);
                float3 tangentWS = normalize(TransformObjectToWorldDir(IN.tangentOS.xyz));
                posWS += ComputeClothDisplacement(uv0, normalWS, tangentWS, freedom);

                // 平行光 / 點光源陰影 bias
                // Directional / punctual light shadow bias
            #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 lightDir = normalize(_LightPosition - posWS);
            #else
                float3 lightDir = _LightDirection;
            #endif
                float4 posHCS = TransformWorldToHClip(ApplyShadowBias(posWS, normalWS, lightDir));

                // 防止 shadow pancaking 夾到 near clip
                // Clamp to near clip to prevent shadow pancaking artefacts
            #if UNITY_REVERSED_Z
                posHCS.z = min(posHCS.z, UNITY_NEAR_CLIP_VALUE);
            #else
                posHCS.z = max(posHCS.z, UNITY_NEAR_CLIP_VALUE);
            #endif
                OUT.positionHCS = posHCS;
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half4 ShadowFrag(ShadowVaryings IN) : SV_Target
            {
                // Alpha clip（透明布料需要對陰影也做裁切）
                float4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                clip(baseColor.a - _AlphaCutoff);
                return 0;
            }

            ENDHLSL
        }

        // =====================================================================
        // Pass 3 — DepthOnly:  深度預通道（SSAO / 景深後處理需要）
        //           Depth pre-pass required for SSAO and depth-of-field
        // =====================================================================
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex   DepthVert
            #pragma fragment DepthFrag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NoiseMap); SAMPLER(sampler_NoiseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _NormalMap_ST;
                float4 _NoiseMap_ST;
                float4 _BaseColor;
                float  _NormalIntensity;
                float  _Smoothness;
                float  _Metallic;
                float  _AlphaCutoff;
                float4 _WaveUVDir;
                float  _WaveFlipU;
                float  _WindSpeed;
                float  _WindStrength;
                float  _SwayStrength;
                float  _NoiseScale;
                float  _ClothGravity;
                float  _FixedUMin;
                float  _FixedUMax;
                float  _FixedVMin;
                float  _FixedVMax;
            CBUFFER_END

            // DepthOnly 位移：R=法線鼓包；Mode0=S曲線駐波(法線)；Mode1=行進波(切線)
            float3 ComputeClothDisplacement(float2 uv, float3 normalWS, float3 tangentWS, float freedom)
            {
                float tRaw = _Time.y * _WindSpeed;
                float t = fmod(tRaw, TWO_PI);
                float  noiseScale = abs(_NoiseScale);
                float2 scrollUV   = uv * noiseScale + _WaveUVDir.xy * tRaw;
                float2 n = SAMPLE_TEXTURE2D_LOD(_NoiseMap, sampler_NoiseMap, scrollUV, 0).rg * 2.0 - 1.0;
                float3 disp = normalWS * (n.r * _WindStrength * freedom);
                float pinU_s = saturate(_FixedUMin + _FixedUMax);
                float pinV_s = saturate(_FixedVMin + _FixedVMax);
                float useV_s = step(pinU_s + 0.001, pinV_s);
                float standWave  = sin(TWO_PI * uv.x - t) * _SwayStrength * freedom;
                float sinSway = sin(t - uv.x * PI) * _SwayStrength * freedom;
                disp += normalWS * (standWave * (1.0 - _WaveFlipU) * (1.0 - useV_s));
                disp.y += sinSway * (1.0 - _WaveFlipU) * useV_s;
                disp += tangentWS * (sinSway * _WaveFlipU);
                disp.y -= _ClothGravity * freedom;
                return disp;
            }

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthVaryings DepthVert(DepthAttributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                DepthVaryings OUT;
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float2 uv0 = IN.uv;
                float pinnedU = max(_FixedUMin * saturate(1.0 - uv0.x * 2.0), _FixedUMax * saturate(1.0 - (1.0 - uv0.x) * 2.0));
                float pinnedV = max(_FixedVMin * saturate(1.0 - uv0.y * 2.0), _FixedVMax * saturate(1.0 - (1.0 - uv0.y) * 2.0));
                float rawFreedom = (1.0 - saturate(pinnedU)) * (1.0 - saturate(pinnedV));
                float freedom = rawFreedom * rawFreedom * (3.0 - 2.0 * rawFreedom);

                float3 posWS = TransformObjectToWorld(IN.positionOS.xyz);
                float3 normalWS  = TransformObjectToWorldNormal(IN.normalOS);
                float3 tangentWS = normalize(TransformObjectToWorldDir(IN.tangentOS.xyz));
                posWS += ComputeClothDisplacement(uv0, normalWS, tangentWS, freedom);

                OUT.positionHCS = TransformWorldToHClip(posWS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            half DepthFrag(DepthVaryings IN) : SV_Target
            {
                float4 baseColor = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;
                clip(baseColor.a - _AlphaCutoff);
                return IN.positionHCS.z;
            }

            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Lit"
}
