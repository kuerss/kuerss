// 雨后的湿沥青：程序化噪声底 + 积水遮罩 + 屏幕空间平面反射（由 PlanarReflection 注入 _ReflectionTex）
Shader "Market/WetAsphalt"
{
    Properties
    {
        _Color ("沥青底色", Color) = (0.055, 0.05, 0.058, 1)
        _WetColor ("湿面染色", Color) = (0.95, 0.88, 0.9, 1)
        _ReflectionTex ("平面反射", 2D) = "black" {}
        _ReflectionStrength ("反射强度", Range(0, 3)) = 1.15
        _Wetness ("积水程度", Range(0, 1)) = 0.85
        _NoiseScale ("噪声尺度", Float) = 2.6
        _PuddleScale ("积水尺度", Float) = 0.9
        _RippleStrength ("水面扰动", Range(0, 2)) = 0.45
        _Smoothness ("光滑度", Range(0, 1)) = 0.82
        _DiffuseGain ("漫反射增益", Range(0, 2)) = 0.9
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _ReflectionTex;
        fixed4 _Color, _WetColor;
        half _ReflectionStrength, _Wetness, _Smoothness, _RippleStrength, _DiffuseGain;
        float _NoiseScale, _PuddleScale;

        struct Input
        {
            float3 worldPos;
            float4 screenPos;
            float3 viewDir;
        };

        float hash21 (float2 p)
        {
            p = frac(p * float2(127.34, 311.45));
            p += dot(p, p + 34.345);
            return frac(p.x * p.y);
        }

        float vnoise (float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            float a = hash21(i);
            float b = hash21(i + float2(1, 0));
            float c = hash21(i + float2(0, 1));
            float d = hash21(i + float2(1, 1));
            return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
        }

        float fbm2 (float2 p)
        {
            float v = 0.0;
            float a = 0.5;
            for (int i = 0; i < 4; i++)
            {
                v += a * vnoise(p);
                p = p * 2.07 + float2(5.3, 9.1);
                a *= 0.5;
            }
            return v;
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float2 wp = IN.worldPos.xz;

            // 沥青颗粒
            float grain = fbm2(wp * _NoiseScale * 4.0);
            float patch = fbm2(wp * _NoiseScale);

            // 积水遮罩
            float puddle = smoothstep(0.42, 0.72, fbm2(wp * _PuddleScale * 0.55 + 21.7));
            puddle = saturate(puddle * _Wetness);

            // 屏幕空间反射 + 水面扰动
            float2 uv = IN.screenPos.xy / max(IN.screenPos.w, 1e-4);
            float2 ripple = float2(fbm2(wp * 3.4), fbm2(wp * 3.4 + 13.0)) - 0.5;
            uv += ripple * (_RippleStrength * 0.010) * (1.0 - puddle * 0.6);

            float3 refl = tex2D(_ReflectionTex, saturate(uv)).rgb;

            float fres = pow(1.0 - saturate(normalize(IN.viewDir).y), 4.0);

            // 底色
            float3 albedo = _Color.rgb * lerp(0.72, 1.25, grain * 0.7 + patch * 0.5);
            albedo = lerp(albedo, _Color.rgb * 1.9, 0.12);
            o.Albedo = albedo * _DiffuseGain;
            o.Metallic = 0.0;
            o.Smoothness = lerp(0.35, _Smoothness, puddle);

            // 反射当作自发光叠加（湿地面主要靠反射出彩）
            float weight = _ReflectionStrength * lerp(0.42, 1.0, puddle) * (0.16 + 0.84 * fres);
            o.Emission = refl * weight * _WetColor.rgb;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
