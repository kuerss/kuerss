// 店内地面：瓷砖贴图 + 平面反射（和室外湿路面共用同一张反射 RT）
Shader "Market/GlossyTile"
{
    Properties
    {
        _MainTex ("瓷砖", 2D) = "white" {}
        _Color ("染色", Color) = (1, 1, 1, 1)
        _ReflectionTex ("平面反射", 2D) = "black" {}
        _ReflectionStrength ("反射强度", Range(0, 3)) = 0.35
        _Smoothness ("光滑度", Range(0, 1)) = 0.7
        _Metallic ("金属度", Range(0, 1)) = 0.05
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex, _ReflectionTex;
        fixed4 _Color;
        half _ReflectionStrength, _Smoothness, _Metallic;

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;
            float4 screenPos;
        };

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float2 uv = IN.screenPos.xy / max(IN.screenPos.w, 1e-4);
            float3 refl = tex2D(_ReflectionTex, saturate(uv)).rgb;

            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = 1.0;
            o.Metallic = _Metallic;
            o.Smoothness = _Smoothness;

            float f = pow(1.0 - saturate(normalize(IN.viewDir).y), 3.0);
            o.Emission = refl * _ReflectionStrength * (0.12 + 0.88 * f);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
