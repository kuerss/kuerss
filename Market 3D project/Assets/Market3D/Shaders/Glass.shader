// 便利店玻璃：半透明 + 屏幕空间平面反射（反射出店内货架，参考图里玻璃上那层叠影）
Shader "Market/Glass"
{
    Properties
    {
        _Color ("玻璃色", Color) = (0.42, 0.48, 0.55, 0.10)
        _ReflectionTex ("平面反射", 2D) = "black" {}
        _ReflectionStrength ("反射强度", Range(0, 3)) = 0.55
        _FresnelBoost ("边缘反射增益", Range(0, 3)) = 1.6
        _Glossiness ("光滑度", Range(0, 1)) = 0.95
        _Metallic ("金属度", Range(0, 1)) = 0.0
    }

    SubShader
    {
        Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        CGPROGRAM
        #pragma surface surf Standard alpha:fade
        #pragma target 3.0

        sampler2D _ReflectionTex;
        fixed4 _Color;
        half _ReflectionStrength, _FresnelBoost, _Glossiness, _Metallic;

        struct Input
        {
            float3 viewDir;
            float4 screenPos;
            float3 worldNormal;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.screenPos.xy / max(IN.screenPos.w, 1e-4);
            float3 refl = tex2D(_ReflectionTex, saturate(uv)).rgb;

            float f = pow(1.0 - saturate(normalize(IN.viewDir).z), 4.0);

            o.Albedo = _Color.rgb;
            o.Alpha = _Color.a;
            o.Metallic = _Metallic;
            o.Smoothness = _Glossiness;
            o.Emission = refl * _ReflectionStrength * (0.18 + f * _FresnelBoost);
        }
        ENDCG
    }
    FallBack "Transparent/Diffuse"
}
