Shader "CGShader/UI/UIFlow"
{
	Properties
	{
		[PerRendererData] _MainTex("Base (RGB)", 2D) = "white" { }
		_Color("Tint", Color) = (1, 1, 1, 0)
		_Angle("Angle",Range(10,180)) = 90
		_Width("Width",Range(0.1,5)) = 0.25
		_Interval("Interval",Int) = 3
		_Duration("duartion",Range(0,5)) = 0.5
		_Pivot("pivot",Range(0,1)) = 0.5
		[Toggle]_UseImageBounds("Use Image Bounds", Float) = 0
		[Toggle]_UseCircleFlow("Use Circle Flow", Float) = 0
		_CircleCenter("Circle Center", Vector) = (0.5, 0.5, 0, 0)
		_CircleRadius("Circle Radius", Range(0.1, 1)) = 0.4
		_AlphaThreshold("Alpha Threshold", Range(0, 1)) = 0.1
		[KeywordEnum(Alpha, Red, Green, Blue)] _FlowChannel("Flow Channel", Float) = 0
		[HDR]_FlowColor("Flow Color",Color) = (1,1,1,1)
		[Toggle(WANT_TO_CLIP)]_Clip("Clip", Float) = 1
		[Toggle]_UseGlobalTime("Use Global Time", Float) = 1
		[HideInInspector]_PastTime("PastTime",Float) = 0
		[HideInInspector]_StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector]_Stencil ("Stencil ID", Float) = 0
        [HideInInspector]_StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector]_StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector]_StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector]_ColorMask ("Color Mask", Float) = 15

		_ColorMask("Color Mask", Float) = 15
		[Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip("Use Alpha Clip", Float) = 0
	}
		SubShader
		{
			Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" "CanUseSpriteAtlas" = "True" }

			Stencil
			{
				Ref[_Stencil]
				Comp[_StencilComp]
				Pass[_StencilOp]
				ReadMask[_StencilReadMask]
				WriteMask[_StencilWriteMask]
			}

			Cull Off
			Lighting Off
			ZWrite Off
			ZTest[unity_GUIZTestMode]
			Blend SrcAlpha OneMinusSrcAlpha
			ColorMask[_ColorMask]

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
				//#pragma shader_feature  WANT_TO_CLIP
				#pragma multi_compile _ WANT_TO_CLIP
				#pragma multi_compile _FLOWCHANNEL_ALPHA _FLOWCHANNEL_RED _FLOWCHANNEL_GREEN _FLOWCHANNEL_BLUE

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
					float4 worldPosition: TEXCOORD3;
					float4 color: COLOR;
					float4 vertex: SV_POSITION;
					UNITY_VERTEX_OUTPUT_STEREO
				};

				sampler2D _MainTex;
				fixed4 _Color;
				fixed4 _TextureSampleAdd;
				float4 _ClipRect;
				float4 _MainTex_ST;

				float _Angle;
				fixed _Width;
				int _Interval;
				float _Duration;
				fixed4 _FlowColor;
				float _PastTime;
				float _Pivot;
				float _UseImageBounds;
				float _UseCircleFlow;
				float4 _CircleCenter;
				float _CircleRadius;
				float _AlphaThreshold;
				float _FlowChannel;
				float _UseGlobalTime;

				v2f vert(appdata v)
				{
					v2f o;
					UNITY_SETUP_INSTANCE_ID(v);
					UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
					
					o.worldPosition = v.vertex;
					o.vertex = UnityObjectToClipPos(o.worldPosition);
					o.uv.xy = TRANSFORM_TEX(v.uv.xy, _MainTex);
					o.uv.zw = float2(0, 0); // 初始化 uv.zw
					o.uv1 = v.uv1;
					o.uv2 = v.uv2;
					o.color = v.color;
					
					return o;
				 }

				
				// 獲取指定通道的值來控制流光顯示
				float getChannelValue(fixed4 color)
				{
					#if _FLOWCHANNEL_RED
						return color.r;
					#elif _FLOWCHANNEL_GREEN
						return color.g;
					#elif _FLOWCHANNEL_BLUE
						return color.b;
					#else // _FLOWCHANNEL_ALPHA
						return color.a;
					#endif
				}

				// 圓形流光計算
				fixed inCircleFlow(float angle, float2 uv, fixed width, int interval, float duration)
				{
					// 首先檢查是否在圓形範圍內
					float2 centerPos = _CircleCenter.xy;
					float distanceToCenter = distance(uv, centerPos);
					
					// 如果超出圓形範圍，不顯示流光
					if (distanceToCenter > _CircleRadius)
					{
						return 0;
					}
					
					// 在圓形範圍內，使用原有的斜角流光計算
					float rad = angle * 0.0174444;
					float tanRad = tan(rad);

					float maxYProj2X = 1.0 / tanRad;
					float totalMovX = 1 + width + maxYProj2X;
					float totalTime = interval + duration;

					// 使用全域時間或相對時間
					float fixTime = _UseGlobalTime > 0.5 ? _Time.y : (_Time.y - _PastTime);
					int cnt = fixTime / totalTime;
					float currentTime = fixTime - cnt * totalTime;

					fixed flow = 0;
					if (currentTime < duration)
					{
						fixed x0 = currentTime / (duration / totalMovX);
						float yProj2X = uv.y / tanRad;
						float xLeft = x0 - width - yProj2X;
						float xRight = xLeft + width;
						
						// 簡化邊界檢查
						if (uv.x < xLeft || uv.x > xRight) 
						{
							flow = 0;
						}
						else 
						{
							// 使用_Pivot控制亮度峰值的位置
							float flowCenter = lerp(xLeft, xRight, _Pivot);
							float distanceFromPivot = abs(uv.x - flowCenter);
							float maxDistance = width * 0.3;
							
							// 創造更集中的亮度分布
							float normalizedDistance = saturate(distanceFromPivot / maxDistance);
							
							// 使用更陡峭的 cos 函數創造更集中的鐘形分布
							float brightness = cos(normalizedDistance * 1.5708) * cos(normalizedDistance * 1.5708);
							brightness = brightness * brightness;
							
							// 確保邊緣透明度為0，但保持中心區域的亮度
							float edgeDistance = min(uv.x - xLeft, xRight - uv.x);
							float fadeWidth = width * 0.05;
							float edgeFade = smoothstep(0, fadeWidth, edgeDistance);
							
							// 補償亮度損失
							float compensatedBrightness = brightness;
							if (edgeDistance > fadeWidth) {
								compensatedBrightness = brightness / (1.0 - fadeWidth / (width * 0.5));
								compensatedBrightness = min(compensatedBrightness, 1.0);
							}
							
							// 圓形邊界處的淡化效果
							float circleFade = smoothstep(_CircleRadius, _CircleRadius * 0.9, distanceToCenter);
							
							flow = compensatedBrightness * edgeFade * circleFade;
						}
					}
					
					return flow;
				}

				fixed inFlow(float angle, float2 uv, fixed width, int interval, float duration)
				{
					float rad = angle * 0.0174444;
					float tanRad = tan(rad);

					float maxYProj2X = 1.0 / tanRad;
					float totalMovX = 1 + width + maxYProj2X;
					float totalTime = interval + duration;

					// 使用全域時間或相對時間
					float fixTime = _UseGlobalTime > 0.5 ? _Time.y : (_Time.y - _PastTime);
					int cnt = fixTime / totalTime;
					float currentTime = fixTime - cnt * totalTime;

					fixed flow = 0;
					if (currentTime < duration)
					{
						fixed x0 = currentTime / (duration / totalMovX);
						float yProj2X = uv.y / tanRad;
						float xLeft = x0 - width - yProj2X;
						float xRight = xLeft + width;
						
						// 簡化邊界檢查
						if (uv.x < xLeft || uv.x > xRight) 
						{
							flow = 0;
						}
						else 
						{
							// 使用_Pivot控制亮度峰值的位置
							float flowCenter = lerp(xLeft, xRight, _Pivot);
							float distanceFromPivot = abs(uv.x - flowCenter);
							float maxDistance = width * 0.3;
							
							// 創造更集中的亮度分布
							float normalizedDistance = saturate(distanceFromPivot / maxDistance);
							
							// 使用更陡峭的 cos 函數創造更集中的鐘形分布
							float brightness = cos(normalizedDistance * 1.5708) * cos(normalizedDistance * 1.5708); // cos²(x*π/2)
							// 額外的銳化處理，讓峰值更集中
							brightness = brightness * brightness;
							
							// 確保邊緣透明度為0，但保持中心區域的亮度
							float edgeDistance = min(uv.x - xLeft, xRight - uv.x);
							float fadeWidth = width * 0.05; // 使用更小的邊緣柔化寬度
							float edgeFade = smoothstep(0, fadeWidth, edgeDistance);
							
							// 補償亮度損失，在非邊緣區域提升亮度
							float compensatedBrightness = brightness;
							if (edgeDistance > fadeWidth) {
								compensatedBrightness = brightness / (1.0 - fadeWidth / (width * 0.5));
								compensatedBrightness = min(compensatedBrightness, 1.0);
							}
							
							flow = compensatedBrightness * edgeFade;
						}
					}

					return flow;
				}

				fixed4 frag(v2f i) : SV_Target
				{
					half4 col = (tex2D(_MainTex, i.uv.xy) + _TextureSampleAdd) * i.color;

					#ifdef UNITY_UI_CLIP_RECT
						col.a *= UnityGet2DClipping(i.worldPosition.xy, _ClipRect);
					#endif

					#ifdef UNITY_UI_ALPHACLIP
						clip(col.a - 0.001);
					#endif

					#if WANT_TO_CLIP
						fixed flow = 0;
						
						if (_UseCircleFlow > 0.5)
						{
							// 圓形流光模式：在圓形範圍內顯示斜角流光
							flow = inCircleFlow(_Angle, i.uv.xy, _Width, _Interval, _Duration);
							
							// 圓形流光：純流光效果
							col = _FlowColor * flow;
						}
						else if (_UseImageBounds > 0.5)
						{
							// 圖片範圍模式：根據指定通道限制流光顯示
							flow = inFlow(_Angle, i.uv.xy, _Width, _Interval, _Duration);
							half4 originalColor = (tex2D(_MainTex, i.uv.xy) + _TextureSampleAdd) * i.color;
							float channelValue = getChannelValue(originalColor);
							
							// 如果通道值低於閾值，不顯示流光
							if (channelValue < _AlphaThreshold)
							{
								flow = 0;
							}
							
							// 圖片範圍模式：保留原圖並添加流光效果
							fixed4 flowColor = _FlowColor * flow;
							col.rgb = col.rgb + flowColor.rgb;
						}
						else
						{
							// 四角流光模式：保持原有簡潔實現
							flow = inFlow(_Angle, i.uv.xy, _Width, _Interval, _Duration);
							col = _FlowColor * flow;
						}
				   #endif

					return col;
				}
				ENDCG
			}
		}
}