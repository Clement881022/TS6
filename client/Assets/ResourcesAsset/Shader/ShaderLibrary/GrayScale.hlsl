#ifndef CGURP_GRAYSCALE_INCLUDED
#define CGURP_GRAYSCALE_INCLUDED

half3 GrayScale(half4 source, float brightness)
{
   return dot(source.rgb, float3(0.299, 0.587, 0.114) * brightness);
}

half3 GrayScale(half4 source)
{
    return dot(source.rgb, float3(0.299, 0.587, 0.114));
}

half3 Bright(half4 source, float brightness)
{
    return pow(source.rgb * brightness, float3(1,1,1));
}

half4 GrayScaleMask(sampler2D tex,float2 pivotxy, float4 texelSize,float4 uv,float Grayfill, float level,float negative,float Brightfill)
{
    float _value;
    float _direction;
    float _fill;
    half4 grayCol = (tex2D(tex, uv.xy));
    half2 up_uv = uv.xy + float2(0, 1) + pivotxy * texelSize.xy;
    half2 down_uv = uv.xy + float2(0, -1) + pivotxy * texelSize.xy;
    half2 left_uv = uv.xy + float2(-1, 0) + pivotxy * texelSize.xy;
    half2 right_uv = uv.xy + float2(1, 0) + pivotxy * texelSize.xy;
    half2 pivot = up_uv * down_uv * left_uv * right_uv;
    half2 center = (uv - pivotxy) * 2.0;

    #ifdef _GRAYDIRECTION_REVERSE
          _direction = 1;
    #endif

    #ifdef _GRAYDIRECTION_POSITIVE
         _direction = 0;
    #endif

    #ifdef _GRAYSTYLE_HORIZONTAL
          half circle_Mask = abs(_direction-(sin(texelSize.x - center.x) + 1.0) * 0.5);
    #endif

    #ifdef _GRAYSTYLE_VERTICAL
         half circle_Mask = abs(_direction-(sin(texelSize.y - center.y) + 1.0) * 0.5);
    #endif

    #ifdef _GRAYSTYLE_RADIAL
         half circle_Mask = abs(_direction-(atan2(center.x , texelSize.y - center.y) * 0.3183098861928886 + 1.0) * 0.5);
    #endif

    _value = level + 1; 
    half GrayfillAmount = saturate(Grayfill);
    half BrightfillAmount = saturate(Brightfill);
    half3 brightcol_bw = Bright(grayCol, _value);
    half3 graycol_bw = GrayScale(grayCol, _value);
    half Grayblur = min(1.0 - max(0.995, GrayfillAmount), GrayfillAmount);
    half Brightblur = min(1.0 - max(0.995, BrightfillAmount), BrightfillAmount);

    if (Brightfill > 0)
    {
       grayCol.rgb = lerp(grayCol.rgb, brightcol_bw, 1.0 - smoothstep(BrightfillAmount - Brightblur, BrightfillAmount + Brightblur, circle_Mask) * (1.0 - negative));
    }
   else
   {
       grayCol.rgb = lerp(grayCol.rgb, graycol_bw, 1.0 - smoothstep(GrayfillAmount - Grayblur, GrayfillAmount + Grayblur, circle_Mask) * (1.0 - negative));
   }
    
    return grayCol;
}
#endif
