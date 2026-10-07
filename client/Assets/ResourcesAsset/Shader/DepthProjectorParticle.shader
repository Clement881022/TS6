Shader "CGShader/DepthProjectorParticle"
{
    Properties
    {
        [Header(Main Settings)]
        [Space(10)]
        [HDR]_TintColor ("Tint Color", Color) = (1, 1, 1, 1)
        _MainTex ("Main Texture", 2D) = "white" {}
        _Intensity ("Intensity", Range(0, 10)) = 2
        
        [Header(Projection Settings)]
        [Space(10)]
        _ProjectionSize ("Projection Size", Range(0.1, 50)) = 5
        _ProjectionOffset ("Projection Offset", Vector) = (0, 0, 0, 0)
        _RotationAngle ("Rotation Angle", Range(-180, 180)) = 0
        _RotationSpeed ("Rotation Speed", Range(0, 10)) = 0
        _ProjectionDirection ("Projection Direction", Vector) = (0, -1, 0, 0)
        _UseVerticalProjection ("Use Vertical Projection", Float) = 1
        _UsePlaneCenter ("Use Plane Center as Origin", Float) = 1
        
        [Header(Depth Settings)]
        [Space(10)]
        _MaxProjectionDistance ("Max Projection Distance", Range(1, 100)) = 20
        _DepthFade ("Depth Fade", Range(0, 5)) = 1
        
        [Header(Animation)]
        [Space(10)]
        _Panner ("UV Panner (XY Speed, Z Multiplier)", Vector) = (0, 0, 1, 0)
        _PulseSpeed ("Pulse Speed", Range(0, 10)) = 0
        _PulseAmount ("Pulse Amount", Range(0, 1)) = 0
        
        [Header(Edge Effects)]
        [Space(10)]
        _EdgeSoftness ("Edge Softness", Range(0, 1)) = 0.2
        _FadeDistance ("Fade Distance", Range(0, 50)) = 10

        [HideInInspector][Header(Debug)]
        _DebugMode ("Debug Mode (0=Off, 1=UV, 2=Depth, 3=Reference)", Range(0, 3)) = 0
        
        [Header(Render Settings)]
        [Space(10)]
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 1

        [IntRange] _StencilRef ("Stencil Reference Value", Range(0, 255)) = 0
        [Enum(UnityEngine.Rendering.CompareFunction)] _StencilComp ("Stencil Compare Value", Range(0, 255)) = 4
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderPipeline" = "UniversalPipeline" 
            "RenderType" = "Transparent" 
            "Queue" = "Transparent" 
        }
        
        Pass
        {
            LOD 128
            Name "DepthParticleProjector"
            Tags {"LightMode" = "UniversalForward"}
            
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest LEqual
            Cull Off

            Stencil
            {
                Ref [_StencilRef]
                ReadMask 255
                WriteMask 255
                Comp [_StencilComp]
            }
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma multi_compile_instancing
            #pragma multi_compile_particles
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"
            
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _TintColor;
                float4 _Panner;
                float4 _ProjectionOffset;
                float4 _ProjectionDirection;
                float _Intensity;
                float _ProjectionSize;
                float _RotationAngle;
                float _RotationSpeed;
                float _MaxProjectionDistance;
                float _DepthFade;
                float _PulseSpeed;
                float _PulseAmount;
                float _EdgeSoftness;
                float _FadeDistance;
                float _UseVerticalProjection;
                float _UsePlaneCenter;
                float _DebugMode;
            CBUFFER_END
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 uv : TEXCOORD0;
                float4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv : TEXCOORD0;
                float4 color : COLOR;
                float4 screenPos : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                float3 viewRayWS : TEXCOORD3;
                float3 projectionRayWS : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            
            // 2D 旋轉函數
            float2 Rotate2D(float2 uv, float2 center, float angle)
            {
                float c = cos(radians(angle));
                float s = sin(radians(angle));
                float2x2 rotMatrix = float2x2(c, -s, s, c);
                return mul(uv - center, rotMatrix) + center;
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                // 基本變換
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.screenPos = ComputeScreenPos(output.positionCS);
                output.worldPos = TransformObjectToWorld(input.positionOS.xyz);
                
                // 計算從相機到頂點的視線射線（世界空間）
                float3 cameraWorldPos = GetCameraPositionWS();
                output.viewRayWS = output.worldPos - cameraWorldPos;
                
                // 計算投影射線方向
                if (_UseVerticalProjection > 0.5)
                {
                    // 垂直投影（向下投射到地面）
                    output.projectionRayWS = normalize(_ProjectionDirection.xyz);
                }
                else
                {
                    // 相機方向投影（原來的行為）
                    output.projectionRayWS = normalize(output.viewRayWS);
                }
                
                // 傳遞數據
                output.uv = input.uv;
                output.color = input.color;
                
                return output;
            }
            
            float4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                  // 動畫效果
                float time = _Time.y;
                
                // 獲取相機世界位置（只定義一次）
                float3 cameraWorldPos = GetCameraPositionWS();
                
                // 屏幕座標
                float2 screenUV = input.screenPos.xy / input.screenPos.w;
                
                // 深度取樣
                float rawDepth = SampleSceneDepth(screenUV);
                float sceneDepth = LinearEyeDepth(rawDepth, _ZBufferParams);
                
                // 當前像素的深度
                float pixelDepth = LinearEyeDepth(input.positionCS.z / input.positionCS.w, _ZBufferParams);
                
                // 計算投影距離（限制最大投影距離）
                float projectionDistance = min(sceneDepth, _MaxProjectionDistance);
                
                // 計算投影點
                float3 projectionPoint;
                if (_UseVerticalProjection > 0.5)
                {
                    // 垂直投影模式：使用屏幕射線重建世界位置
                    float3 viewDirection = normalize(input.viewRayWS);
                    float3 worldPosFromDepth = cameraWorldPos + viewDirection * sceneDepth;
                    projectionPoint = worldPosFromDepth;
                }
                else
                {
                    // 相機方向投影（原來的行為）
                    float3 viewDirection = normalize(input.viewRayWS);
                    projectionPoint = cameraWorldPos + viewDirection * projectionDistance;
                }
                
                // 將投影點轉換為相對於合適參考點的局部坐標
                float3 referencePoint;
                if (_UsePlaneCenter > 0.5)
                {
                    // 使用 Plane 中心作為參考點（通常是 transform.position）
                    referencePoint = TransformObjectToWorld(float3(0, 0, 0));
                }
                else
                {
                    // 使用粒子當前位置作為參考點
                    referencePoint = input.worldPos;
                }
                
                float3 relativePos = projectionPoint - referencePoint;
                
                // 投影到 XZ 平面（地面）並縮放，如果是垂直投影的話
                float2 projectorUV;
                if (_UseVerticalProjection > 0.5)
                {
                    // 垂直投影：使用 XZ 平面作為 UV 座標（地面投影）
                    projectorUV = (relativePos.xz / _ProjectionSize) + 0.5;     
                }
                else
                {
                    // 相機投影：使用 XY 平面（原來的行為）
                    projectorUV = (relativePos.xy / _ProjectionSize) + 0.5;
                }
              
                // 應用偏移
                projectorUV += _ProjectionOffset.xy;
                
                // 應用旋轉
                if (abs(_RotationAngle) > 0.1)
                {
                    projectorUV = Rotate2D(projectorUV, float2(0.5, 0.5), _RotationAngle * time *_RotationSpeed);
                }
                
                // UV 動畫
                float2 animatedUV = projectorUV + _Panner.xy * time * _Panner.z;
                
                // 脈衝效果
                float pulse = 1.0;
                if (_PulseSpeed > 0.01)
                {
                    pulse = 1.0 + sin(time * _PulseSpeed) * _PulseAmount;
                }
                
                // 邊緣柔化
                float2 edgeDistance = abs(projectorUV - 0.5);
                float edgeMask = 1.0 - smoothstep(0.5 - _EdgeSoftness, 0.5, max(edgeDistance.x, edgeDistance.y));
                
                // UV 邊界檢查
                float uvMask = step(0.0, projectorUV.x) * step(projectorUV.x, 1.0) * 
                              step(0.0, projectorUV.y) * step(projectorUV.y, 1.0);
                
                // 深度淡出
                float depthFade = 1.0;
                if (_UseVerticalProjection < 0.5)
                {
                    // 只在相機投影模式下使用深度淡出
                    float depthDiff = abs(sceneDepth - pixelDepth);
                    depthFade = saturate(depthDiff / _DepthFade);
                }
                else
                {
                    // 垂直投影模式：基於距離的淡出
                    float distanceFromParticle = length(projectionPoint - input.worldPos);
                    depthFade = saturate(distanceFromParticle / _MaxProjectionDistance);
                }
                
                // 距離淡出
                float distanceToCamera = length(input.worldPos - cameraWorldPos);
                float distanceFade = 1.0 - saturate(distanceToCamera / _FadeDistance);
                
                // 取樣主紋理
                float4 mainColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, animatedUV);
                
                // 調試模式
                if (_DebugMode > 2.5) // Debug Mode 3: 顯示參考點和投影關係
                {
                    float3 refPoint = (_UsePlaneCenter > 0.5) ? 
                        TransformObjectToWorld(float3(0, 0, 0)) : input.worldPos;
                    float distanceToRef = length(projectionPoint - refPoint) / _ProjectionSize;
                    return float4(distanceToRef, projectorUV.x, projectorUV.y, 1.0);
                }
                else if (_DebugMode > 1.5) // Debug Mode 2: 顯示深度
                {
                    float depthVis = sceneDepth / _MaxProjectionDistance;
                    return float4(depthVis, depthVis, depthVis, 1.0);
                }
                else if (_DebugMode > 0.5) // Debug Mode 1: 顯示 UV
                {
                    return float4(projectorUV, 0.0, uvMask);
                }
                
                // 顏色合成
                float3 finalColor = mainColor.rgb * input.color.rgb * _TintColor.rgb * _Intensity * pulse;
                
                // 透明度合成
                float finalAlpha = mainColor.a * input.color.a * _TintColor.a;
                finalAlpha *= edgeMask * uvMask * depthFade * distanceFade;
                
                // 粒子生命週期淡出（基於 vertex color alpha）
                finalAlpha *= saturate(input.color.a * 2.0);
                
                // 裁切完全透明的像素
                clip(finalAlpha - 0.001);
                
                return float4(finalColor, finalAlpha);
            }
            
            ENDHLSL
        }
    }
    
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
