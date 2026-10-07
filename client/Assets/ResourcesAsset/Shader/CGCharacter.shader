Shader "CGShader/CGCharacter"
{
    Properties
    {
        [Header(________________________Base________________________)][Space(15)]
        _BaseMap ("基礎貼圖 Base Map", 2D) = "white" {}
        [HDR]_BaseColor ("基礎染色 Base Color", Color) = (1,1,1,1)
        _Chroma("彩度遮罩 ChromaMask", Range(0, 1)) = 0
        [Toggle(MAINLIGHT_ON)] _MainLightEnable ("是否受即時光影響 Enable MainLight", Float) = 1
        [Toggle(COLORING_ON)] _ColoringEnable ("是否用AlphaMask染色 Enable AlphaMaskColoring", Float) = 0
        [HDR]_ColoringColor ("AlphaMask染色 AlphaMaskColor", Color) = (1,1,1,1)
        _Smoothness ("平滑度 Smoothness", Range(0,1)) = 0.5
        _Metallic ("金屬感 Metallic", Range(0,1)) = 0.5
        _NormalMap ("法線貼圖 Normal Map", 2D) = "bump" {}
        _NormalIntensity ("法線效果程度 Normal Intensity", Range(0,2)) = 0
        _ShadowStrength ("影子強度 Shadow Strength", Range(0,1)) = 0.5

        [Header(________________________BlendMode________________________)][Space(15)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src", Float) = 5.0
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst", Float) = 10.0
        [Enum(Off, 0, On, 1)]  _ZWrite ("ZWrite", Float) = 1.0
        [Enum(UnityEngine.Rendering.CompareFunction)]  _ZTest ("ZTest", Float) = 4

        [Header(________________________OutLine________________________)][Space(15)]
        _OutlineColor ("描線顏色 Outline Color", Color) = (0.14, 0.14, 0.14, 1)
        _OutlineWidth ("描線粗細 Outline Width", Float) = 0.01
        _EdgeAngleThreshold ("邊緣角度閾值 Edge Angle Threshold", Range(0.1, 1.0)) = 0.5
        _CreaseDetection ("褶皺檢測強度 Crease Detection", Range(0, 2)) = 1.0

        [Header(________________________Fresnel________________________)][Space(15)]
        [HDR]_FresnelColor ("邊緣光顏色 Fresnel Color", Color) = (0.4,1,1,1)
        _FresnelPower ("邊緣光強度 Fresnel Power", Range(1, 10)) = 1
        _FresnelIntensity ("邊緣光亮度 Fresnel Intensity", Range(0, 2)) = 0
        _FresnelThreshold ("邊緣光位置 Fresnel Threshold", Range(0, 1)) = 0.7
        _FresnelLineWidth ("邊緣光線寬 Fresnel Line Width", Range(0.001, 0.5)) = 0.05

        [Header(________________________Ice Effect________________________)][Space(15)]
        _IceColor("冰凍效果基礎顏色 Ice Color", Color) = (0.7, 0.9, 1.0, 1)
        _IceAmount("冰凍程度 Ice Amount", Range(0, 1)) = 0
        _IceSpikeNormalMap("冰凍水流貼圖 Ice Spike Normal", 2D) = "bump" {}
        _IceSpikeStrength("冰凍水流程度 Ice Spike Strength", Range(0, 2)) = 1
        _IceMaskTex("冰刺效果範圍遮罩 Ice Mask", 2D) = "white" {}
        _IceSpikeNoise("冰刺貼圖 Ice Spike Noise", 2D) = "white" {}
        _IceSpikeNoiseStrength("冰刺使用範圍程度 Ice Spike Noise Strength", Range(0, 2)) = 0
        _IceSpikeSmoothness("冰凍效果平滑度 Ice Spike Smoothness", Range(0, 1)) = 0
        _IceDeformAmount("冰刺擠出程度 Ice Deform Amount", Range(0, 1)) = 0

        [Header(________________________Stone Effect________________________)][Space(15)]
        _StoneColor ("石化基礎顏色 Stone Color", Color) = (0.5, 0.5, 0.5, 1)
        _StoneAmount ("石化程度 Stone Amount", Range(0,1)) = 0
        _StoneSmoothness ("石化平滑度 Stone Smoothness", Range(0,1)) = 0.2
        _StoneTex ("石化基礎貼圖 Stone Texture", 2D) = "white" {}
        _StoneNormalMap ("石化法線貼圖 Stone Normal Map", 2D) = "bump" {}
        _StoneNormalIntensity ("石化法線程度 Stone Normal Intensity", Range(0,2)) = 1.0

        [Header(________________________Burn Effect________________________)][Space(15)]
        [Toggle(_BURN_ON)] _BurnEnable ("燃燒效果開關 Enable Burn Effect", Float) = 0
        _BurnColor ("燃燒顏色 Burn Color", Color) = (0.1,0.05,0.05,1)
        [HDR]_BurnEdgeColor ("燃燒邊緣色 Burn Edge Color", Color) = (1,0.5,0.1,1)
        _BurnAmount ("燃燒程度 Burn Amount", Range(0,1)) = 0.5
        _BurnEdgeWidth ("燃燒範圍 Burn Edge Width", Range(0.01,0.5)) = 0.5
        _BurnTex ("燃燒範圍貼圖 Burn Noise", 2D) = "white" {}
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent"  }

        // -------- PASS 1: OUTLINE ----------
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="UniversalForwardOnly" }
            Stencil
            {
                Ref 128
                Comp GEqual
            }

            ZWrite On // 幽靈效果:Off
            ZTest LEqual 
            Cull Front
            Blend [_SrcBlend][_DstBlend]
            //幽靈效果: SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float4 color : COLOR; // R通道: 邊緣標記, G通道: 相鄰面法線差異, B通道: 褶皺強度
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float edgeIntensity : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float creaseIntensity : TEXCOORD3;
            };

            float _OutlineWidth;
            float4 _OutlineColor;
            float _EdgeAngleThreshold;
            float _CreaseDetection;

            // 簡化的邊緣檢測 - 移除複雜運算
            float CalculateEdgeIntensity(float3 normalWS, float3 viewDirWS, float4 vertexData)
            {
                float manualEdge = vertexData.r;
                float normalAngleDiff = vertexData.g;
                float creaseStrength = vertexData.b;
                
                // 簡化角度邊緣 - 移除smoothstep，使用線性插值
                float angleThreshold = 1.0 - _EdgeAngleThreshold;
                float angleEdge = saturate((normalAngleDiff - angleThreshold) * 2.0);
                
                // 簡化褶皺檢測 - 直接使用
                float creaseEdge = creaseStrength * _CreaseDetection;
                
                // 簡化輪廓邊緣 - 移除pow運算，使用平方
                float viewAngle = abs(dot(normalWS, viewDirWS));
                float silhouetteEdge = (1.0 - viewAngle) * (1.0 - viewAngle); // x^2 instead of pow(x, 2.5)
                
                // 直接取最大值
                return saturate(max(manualEdge, max(angleEdge, max(creaseEdge, silhouetteEdge * 0.7))));
            }
            
            // 簡化褶皺計算
            float CalculateCreaseIntensity(float4 vertexData)
            {
                // 直接使用頂點數據，避免複雜計算
                return vertexData.b * _CreaseDetection;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                
                // 基本變換
                float3 positionOS = input.positionOS.xyz;
                float3 normalOS = input.normalOS;
                float3 posWS = TransformObjectToWorld(positionOS);
                float3 normalWS = normalize(TransformObjectToWorldNormal(normalOS));
                float3 viewDirWS = normalize(_WorldSpaceCameraPos - posWS);
                
                // === 描線計算 ===
                
                // 1. 簡化邊緣強度計算
                float edgeIntensity = CalculateEdgeIntensity(normalWS, viewDirWS, input.color);
                float creaseIntensity = CalculateCreaseIntensity(input.color);
                
                // 2. 移除噪音計算以提升性能
                float totalEdgeStrength = max(edgeIntensity, creaseIntensity * 0.5);
                
                // 3. 保持距離補償效果但簡化計算
                float distToCamera = distance(posWS, _WorldSpaceCameraPos);
                float distanceScale = lerp(0.6, 1.6, saturate(distToCamera * 0.067)); // 1/15 = 0.067
                
                // 4. 最終描線寬度
                float finalOutlineWidth = _OutlineWidth * totalEdgeStrength * distanceScale;
                
                // 5. 簡化法線處理，移除分支
                float3 outlineNormal = normalWS;
                float normalViewDot = dot(outlineNormal, viewDirWS);
                // 使用saturate避免分支判斷
                outlineNormal = normalize(outlineNormal + viewDirWS * saturate(-normalViewDot) * 0.2);
                
                // 6. 頂點擠出
                posWS += outlineNormal * finalOutlineWidth;
                
                // 7. 最終位置計算
                output.positionHCS = TransformWorldToHClip(posWS);
                
                // 8. 簡化數據傳遞
                output.edgeIntensity = totalEdgeStrength; // 合併傳遞
                output.creaseIntensity = 0; // 不使用
                output.viewDirWS = viewDirWS;
                output.normalWS = outlineNormal;
                
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // 直接返回固定的描線顏色，完全不受任何光照影響
                return _OutlineColor;
            }
            ENDHLSL
        }

        // -------- PASS 2: LIT BODY ----------
        Pass
        {
            Name "Forward"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            Blend [_SrcBlend][_DstBlend]
            ZWrite [_ZWrite]
            ZTest [_ZTest]
            LOD 300

            Stencil
            {
                Ref 128
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #pragma prefer_hlslcc gles
            #pragma exclude_renderers d3d11_9x
            #pragma target 4.5

            //#pragma multi_compile _ _MAIN_LIGHT_SHADOWS
            #define _MAIN_LIGHT_SHADOWS 1
            //#pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
            #define _MAIN_LIGHT_SHADOWS_CASCADE 1
            //#pragma multi_compile _ _SHADOWS_SOFT
            #define _SHADOWS_SOFT 1
            #pragma multi_compile _ _ADDITIONAL_LIGHTS

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 uv2 : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 viewDirWS : TEXCOORD3;
                float3 tangentWS : TEXCOORD4;
                float3 bitangentWS : TEXCOORD5;
                float vertexIceMask : TEXCOORD6;
                float3 positionWS : TEXCOORD7;
            };

            sampler2D _BaseMap;
            float4 _BaseColor;
            float _Chroma;
            float _Smoothness;
            float _Metallic;
            float _MainLightEnable;
            float _ColoringEnable;
            sampler2D _NormalMap;
            float _NormalIntensity;
            float _ShadowStrength;
            float4 _ColoringColor;

            float4 _FresnelColor;
            float _FresnelPower;
            float _FresnelIntensity;
            float _FresnelThreshold;
            float _FresnelLineWidth;

            float4 _IceColor;
            float _IceAmount;
            sampler2D _IceSpikeNormalMap;
            sampler2D _IceSpikeNoise;
            float _IceSpikeStrength;
            float _IceSpikeNoiseStrength;
            float _IceSpikeSmoothness;
            float _IceDeformAmount;

            TEXTURE2D(_IceMaskTex);
            SAMPLER(sampler_IceMaskTex);

            float4 _StoneColor;
            float _StoneAmount;
            float _StoneSmoothness;
            sampler2D _StoneTex;
            sampler2D _StoneNormalMap;
            float _StoneNormalIntensity;

            float4 _BurnColor;
            float4 _BurnEdgeColor;
            float _BurnAmount;
            float _BurnEdgeWidth;
            sampler2D _BurnTex;
            float _BurnEnable;

            Varyings vert(Attributes input)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);

                // 冰刺位移
                if (_IceDeformAmount > 0.01 && input.color.b > 0.01)
                {
                    float2 noiseUV = posWS.xz * 2.0;
                    float noise = tex2Dlod(_IceSpikeNoise, float4(noiseUV, 0, 0)).r;
                    float noiseStrength = saturate(noise * _IceSpikeNoiseStrength);
                    float sharp = smoothstep(1.0 - _IceSpikeSmoothness, 1.0, noiseStrength);
                    float spike = _IceDeformAmount * sharp * input.color.b;
                    posWS += float3(0, -1, 0) * spike;
                }

                o.positionHCS = TransformWorldToHClip(posWS);
                o.uv = input.uv;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.positionWS = posWS;

                // 計算 tangent space
                float3 tangentWS = TransformObjectToWorldDir(input.tangentOS.xyz);
                float tangentSign = input.tangentOS.w * unity_WorldTransformParams.w;
                float3 bitangentWS = cross(o.normalWS, tangentWS) * tangentSign;
                o.tangentWS = tangentWS;
                o.bitangentWS = bitangentWS;
                o.vertexIceMask = input.color.b;

                o.viewDirWS = normalize(_WorldSpaceCameraPos - posWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // Albedo
                half3 albedo = tex2D(_BaseMap, i.uv).rgb * _BaseColor.rgb;
                
                if(_ColoringEnable > 0.01)
                {
                    half4 baseMapSample = tex2D(_BaseMap, i.uv);
                    half baseMask = baseMapSample.a; // 取出BaseMap的Alpha
                    if(baseMask > 0.01) // 只對非黑色區塊染色
                    {
                        albedo.rgb = lerp(albedo.rgb, baseMask * _ColoringColor.rgb, _ColoringEnable);
                    }
                }

                // 取得遮罩貼圖（IceMaskTex）影響範圍
                half iceMaskTex = SAMPLE_TEXTURE2D(_IceMaskTex, sampler_IceMaskTex, i.uv).r;

                // 加上遮罩限制
                half iceMask = saturate(i.vertexIceMask * _IceAmount * iceMaskTex);

                // 冰外觀
                half3 iceAlbedo = lerp(albedo, _IceColor.rgb, iceMask);

                // --- 石化紋理混合 ---
                half3 stoneTex = tex2D(_StoneTex, i.uv * 2).rgb;
                half3 stoneBase = lerp(_StoneColor.rgb, stoneTex, 0.7);
                half3 stoneAlbedo = lerp(iceAlbedo, stoneBase, _StoneAmount);

                // --- 燒焦/燃燒混合 ---
                half burnNoise = tex2D(_BurnTex, i.uv).r;
                half burnMask = 0;
                half burnEdge = 0;
                if (_BurnEnable > 0.5)
                {
                    burnMask = smoothstep(_BurnAmount - _BurnEdgeWidth, _BurnAmount, burnNoise);
                    burnEdge = smoothstep(_BurnAmount, _BurnAmount + _BurnEdgeWidth, 0.7 + burnNoise) - burnMask;
                }
                else
                {
                    burnMask = 0;
                    burnEdge = 0;
                }
                // 三段顏色混合：原貼圖→火焰→燒焦
                half3 baseColor = stoneAlbedo;
                // 3. 燒焦邊緣發光（直接加在 finalColor，且只在燃燒邊緣）
                half3 burnEdgeCol = _BurnEdgeColor.rgb * pow(burnEdge, 0.5) * 2.5;
                half3 burnAlbedo = lerp(baseColor, _BurnColor.rgb, burnMask);

                // --- 法線混合 ---
                half3 normalTS = UnpackNormal(tex2D(_NormalMap, i.uv));
                half3 stoneNormalTS = UnpackNormal(tex2D(_StoneNormalMap, i.uv * 2));
                normalTS.xy *= _NormalIntensity;
                stoneNormalTS.xy *= _StoneNormalIntensity;
                normalTS = normalize(lerp(normalTS, stoneNormalTS, _StoneAmount));
                // 若完全燃燒，法線不再混合石化
                normalTS = normalize(lerp(normalTS, half3(0,0,1), burnMask));


                half3x3 TBN = half3x3(normalize(i.tangentWS), normalize(i.bitangentWS), normalize(i.normalWS));
                half3 normalWS = normalize(mul(normalTS, TBN));

                // 冰刺法線混合
                if (iceMask > 0.01)
                {
                    half2 spikeUV = i.uv * half2(1, 2) + half2(0, -_Time.y * 0.1);
                    half3 iceSpikeNormalTS = UnpackNormal(tex2D(_IceSpikeNormalMap, spikeUV));
                    iceSpikeNormalTS.xy *= _IceSpikeStrength;
                    normalTS = normalize(lerp(normalTS, iceSpikeNormalTS, iceMask));
                    normalWS = normalize(mul(normalTS, TBN));
                }

                // 高光根據石化/冰凍混合
                half smoothness = lerp(lerp(_Smoothness, _IceSpikeSmoothness, _IceAmount), _StoneSmoothness, _StoneAmount);
                half3 diffuse = burnAlbedo * _BaseColor.rgb;
                // Specular (Blinn-Phong)
                half3 viewDir = normalize(i.viewDirWS);
                half3 halfDir = normalize(viewDir);
                half NdotH = saturate(dot(normalWS, halfDir));
                half spec = pow(NdotH, lerp(1.0, 128.0, smoothness)) * smoothness;
                half3 specular = spec * _BaseColor.rgb * lerp(0.04, 1.0, _Metallic);

                if(_MainLightEnable > 0.01)
                {
                    // Lighting
                    Light mainLight = GetMainLight();
                    half3 lightDir = normalize(mainLight.direction);
                    half NdotL = saturate(dot(normalWS, lightDir));
                    NdotL = lerp(1.0, NdotL, _ShadowStrength); // 考慮陰影強度

                    // // 加入陰影衰減
                    half shadowAttenuation = mainLight.shadowAttenuation;
                    NdotL *= shadowAttenuation;
                    diffuse = burnAlbedo * mainLight.color.rgb * NdotL;

                    // Specular (Blinn-Phong)
                    halfDir = normalize(lightDir + viewDir);
                    specular = spec * mainLight.color.rgb * lerp(0.04, 1.0, _Metallic);
                }
                // Environment Reflection
                half3 env = SampleSH(normalWS);

                // 冰邊緣 Fresnel
                half iceFresnel = pow(1.0 - saturate(dot(normalWS, viewDir)), 3.0);
                half3 iceFresnelColor = iceFresnel * _IceColor.rgb * iceMask * (1 - _StoneAmount);

                // 石化邊緣 Fresnel
                half stoneFresnel = pow(1.0 - saturate(dot(normalWS, viewDir)), 2.0) * _StoneAmount;
                half3 stoneFresnelColor = stoneFresnel * stoneBase * 1.2;

                // Fresnel (實線描邊效果)
                half rimDot = 1.0 - saturate(dot(i.normalWS, i.viewDirWS));
                half solidFresnel = smoothstep(_FresnelThreshold - _FresnelLineWidth, _FresnelThreshold, rimDot)
                                  * (1.0 - smoothstep(_FresnelThreshold, _FresnelThreshold + _FresnelLineWidth, rimDot));
                half fresnelPulse = solidFresnel * _FresnelIntensity;
                half3 fresnelColor = fresnelPulse * _FresnelColor.rgb * (1 - burnMask); // 燒焦時不顯示原 Fresnel

                // 冰柱高光（可選）
                half3 iceSpikeSpec = half3(0,0,0);
                if (_IceAmount > 0.01)
                {
                    half spikeFresnel = pow(1.0 - saturate(dot(normalWS, viewDir)), 8.0) * _IceAmount;
                    iceSpikeSpec = _IceColor.rgb * spikeFresnel * 2.5;
                }

                // 組合最終顏色
                half3 finalColor = diffuse + specular + env * _Chroma + fresnelColor
                    + iceFresnelColor * (1-burnMask)
                    + stoneFresnelColor * (1-burnMask)
                    + burnEdgeCol                          // 邊緣發光直接加
                    + iceSpikeSpec;                        // 冰柱高光

                // 透明度：石化或燃燒時可強制不透明
                half alpha = lerp(lerp(_BaseColor.a, 1, _IceAmount), 1, max(_StoneAmount, burnMask));
                if(_ColoringEnable > 0.01)
                {
                    alpha = lerp(lerp(_ColoringColor.a, 1, _IceAmount), 1, max(_StoneAmount, burnMask));
                }
            
                return half4(finalColor, alpha);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings o;
                float3 posWS = TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(posWS);
                return o;
            }

            float4 frag(Varyings i) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}