// 日落天空盒：竖向渐变 + 太阳圆盘/光晕 + 两层程序化云。
// 这是 Market 场景氛围的主体，颜色全部由脚本在生成场景时写好。
Shader "Market/SunsetSky"
{
    Properties
    {
        _TopColor ("天顶色", Color) = (0.13, 0.07, 0.28, 1)
        _MidColor ("中天色", Color) = (0.55, 0.20, 0.45, 1)
        _LowColor ("低空粉", Color) = (0.95, 0.42, 0.40, 1)
        _HorizonColor ("地平线色", Color) = (1.0, 0.52, 0.20, 1)
        _GroundColor ("地平线下色", Color) = (0.20, 0.13, 0.16, 1)
        _GlowColor ("太阳光晕色", Color) = (1.0, 0.62, 0.28, 1)
        _SunColor ("太阳本体色", Color) = (1.0, 0.88, 0.66, 1)
        _SunDir ("太阳方向(世界)", Vector) = (0, 0.08, 1, 0)
        _SunSize ("太阳大小", Range(0.9, 0.9999)) = 0.99982
        _CloudLit ("云受光色", Color) = (1.0, 0.66, 0.62, 1)
        _CloudDark ("云阴影色", Color) = (0.30, 0.16, 0.38, 1)
        _CloudCoverage ("云量", Range(0, 1)) = 0.52
        _CloudContrast ("云对比", Range(0.5, 3)) = 1.35
        _CloudScale ("云尺度", Float) = 1.0
        _SkyIntensity ("天空亮度", Float) = 1.25
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            float4 _TopColor, _MidColor, _LowColor, _HorizonColor, _GroundColor, _GlowColor, _SunColor;
            float4 _SunDir;
            float4 _CloudLit, _CloudDark;
            float _SunSize, _CloudCoverage, _CloudContrast, _CloudScale, _SkyIntensity;

            float hash21 (float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
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

            float fbm (float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int i = 0; i < 5; i++)
                {
                    v += a * vnoise(p);
                    p = p * 2.03 + float2(3.1, 7.7);
                    a *= 0.5;
                }
                return v;
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float3 sunDir = normalize(_SunDir.xyz);
                float sunDot = dot(dir, sunDir);
                float sd = saturate(sunDot);
                float h = saturate(dir.y);

                // ---- 基础渐变：橘 -> 粉 -> 洋红 -> 靛紫 ----
                float g1 = smoothstep(0.00, 0.085, dir.y);
                float g2 = smoothstep(0.05, 0.24, dir.y);
                float g3 = smoothstep(0.18, 0.60, dir.y);

                float3 sky = lerp(_HorizonColor.rgb, _LowColor.rgb, g1);
                sky = lerp(sky, _MidColor.rgb, g2);
                sky = lerp(sky, _TopColor.rgb, g3);
                sky = lerp(_GroundColor.rgb, sky, saturate(dir.y * 18.0 + 0.5));

                // ---- 太阳光晕（收窄，避免整个天空被烧成橘色） ----
                float halo = pow(sd, 14.0) * 0.20 + pow(sd, 70.0) * 0.9 + pow(sd, 900.0) * 2.0;
                sky += _GlowColor.rgb * halo;

                // ---- 云 ----
                if (dir.y > 0.002)
                {
                    // 用「方位角 + 仰角」铺云，避免平面投影在地平线附近被拉成细条
                    float az = atan2(dir.z, dir.x);
                    float el = asin(clamp(dir.y, -1.0, 1.0));
                    float2 uv = float2(az * 1.55, el * 7.0);
                    uv += float2(_Time.y * 0.0045, _Time.y * 0.0015);

                    float n1 = fbm(uv * 1.05 * _CloudScale);
                    float n2 = fbm(uv * 2.30 * _CloudScale + 17.3);
                    float shape = n1 * 0.74 + n2 * 0.42;
                    float mask = smoothstep(0.44 - _CloudCoverage * 0.26, 0.94 - _CloudCoverage * 0.12, shape);
                    mask *= smoothstep(0.0, 0.022, dir.y);
                    mask = saturate(pow(mask, _CloudContrast));

                    // 云底暗紫、云边被夕阳烧红
                    float litEdge = pow(sd, 1.5) * 0.62 + n2 * 0.78;
                    float3 cloudCol = lerp(_CloudDark.rgb, _CloudLit.rgb, saturate(litEdge));
                    cloudCol += _GlowColor.rgb * pow(sd, 30.0) * 1.6;
                    cloudCol *= 0.78 + 0.46 * n1;

                    sky = lerp(sky, cloudCol, mask * 0.85);
                }

                // ---- 太阳本体 ----
                float disc = saturate((sunDot - _SunSize) / max(1.0 - _SunSize, 1e-5) * 34.0);
                sky += _SunColor.rgb * saturate(disc * 1.6) * 13.0;

                return float4(sky * _SkyIntensity, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
