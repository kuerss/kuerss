// 时间系统天空盒：白天/黄昏/星空夜共用一套渐变 + 云 + 太阳 + 月亮 + 星空。
// 所有颜色/方向/强度都由 TimeOfDaySystem 在运行时插值，材质本身的默认值是“黄昏”。
Shader "Market/TimeOfDaySky"
{
    Properties
    {
        // ---- 渐变 ----
        _TopColor ("天顶色", Color) = (0.090, 0.050, 0.210, 1)
        _MidColor ("中天色", Color) = (0.460, 0.155, 0.415, 1)
        _LowColor ("低空粉", Color) = (0.980, 0.400, 0.380, 1)
        _HorizonColor ("地平线色", Color) = (1.000, 0.500, 0.190, 1)
        _GroundColor ("地平线下色", Color) = (0.200, 0.100, 0.130, 1)

        // ---- 太阳 ----
        _GlowColor ("太阳光晕色", Color) = (1.000, 0.460, 0.150, 1)
        _SunColor ("太阳本体色", Color) = (1.000, 0.820, 0.520, 1)
        _SunDir ("太阳方向(世界)", Vector) = (-0.055, 0.078, 1, 0)
        _SunSize ("太阳大小", Range(0.9, 0.99995)) = 0.99974
        _SunDisc ("太阳本体亮度", Float) = 1
        _SunHalo ("太阳光晕亮度", Float) = 1

        // ---- 月亮 ----
        _MoonColor ("月亮本体色", Color) = (0.90, 0.94, 1.0, 1)
        _MoonDir ("月亮方向(世界)", Vector) = (-0.12, 0.25, 1, 0)
        _MoonSize ("月亮大小", Range(0.9, 0.99995)) = 0.99962
        _MoonDisc ("月亮本体亮度", Float) = 0
        _MoonGlowColor ("月亮光晕色", Color) = (0.45, 0.62, 1.0, 1)
        _MoonGlow ("月亮光晕亮度", Float) = 0

        // ---- 星空 ----
        _StarColor ("星星颜色", Color) = (0.88, 0.93, 1.0, 1)
        _StarIntensity ("星星亮度", Float) = 0
        _StarDensity ("星星密度", Range(0, 1)) = 0.12
        _StarSize ("星星大小", Range(0.01, 0.5)) = 0.14
        _StarTwinkle ("星星闪烁", Range(0, 1)) = 0.3
        _MilkyWay ("银河亮度", Float) = 0

        // ---- 云 ----
        _CloudLit ("云受光色", Color) = (1.000, 0.520, 0.440, 1)
        _CloudDark ("云阴影色", Color) = (0.150, 0.070, 0.225, 1)
        _CloudCoverage ("云量", Range(0, 1)) = 0.80
        _CloudContrast ("云对比", Range(0.5, 3)) = 1.30
        _CloudScale ("云尺度", Float) = 1.05
        _CloudSpeed ("云速度", Float) = 1.0

        _SkyIntensity ("天空亮度", Float) = 1.0
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

            float4 _TopColor, _MidColor, _LowColor, _HorizonColor, _GroundColor;
            float4 _GlowColor, _SunColor, _MoonColor, _MoonGlowColor;
            float4 _SunDir, _MoonDir;
            float4 _CloudLit, _CloudDark;
            float4 _StarColor;

            float _SunSize, _SunDisc, _SunHalo;
            float _MoonSize, _MoonDisc, _MoonGlow;
            float _StarIntensity, _StarDensity, _StarSize, _StarTwinkle, _MilkyWay;
            float _CloudCoverage, _CloudContrast, _CloudScale, _CloudSpeed;
            float _SkyIntensity;

            float hash21 (float2 p)
            {
                p = frac(p * float2(123.34, 345.45));
                p += dot(p, p + 34.345);
                return frac(p.x * p.y);
            }

            float2 hash22 (float2 p)
            {
                float n = hash21(p);
                return float2(n, hash21(p + 19.19));
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

            // 按“方位角 × 仰角”铺一层星星；用格子哈希决定星位和亮度。
            float starLayer (float3 dir, float scale, float density, float size, float seed)
            {
                float az = atan2(dir.z, dir.x);
                float el = asin(clamp(dir.y, -1.0, 1.0));
                float2 uv = float2(az, el) * scale;
                float2 cell = floor(uv);
                float2 f = frac(uv);

                float2 rnd = hash22(cell + seed);
                float2 starPos = 0.16 + rnd * 0.68;
                float d = length(f - starPos);

                float present = step(1.0 - density, hash21(cell + seed + 3.7));
                float bright = hash21(cell + seed + 11.3);
                float core = smoothstep(size, 0.0, d);
                float twinkle = 1.0 - _StarTwinkle * 0.5 * (0.5 + 0.5 * sin(_Time.y * 2.3 + bright * 57.0));

                return core * present * twinkle * (0.30 + 0.70 * bright);
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
                float3 moonDir = normalize(_MoonDir.xyz);
                float sunDot = dot(dir, sunDir);
                float moonDot = dot(dir, moonDir);
                float sd = saturate(sunDot);

                // ---- 基础渐变 ----
                float g1 = smoothstep(0.00, 0.085, dir.y);
                float g2 = smoothstep(0.05, 0.24, dir.y);
                float g3 = smoothstep(0.18, 0.60, dir.y);

                float3 sky = lerp(_HorizonColor.rgb, _LowColor.rgb, g1);
                sky = lerp(sky, _MidColor.rgb, g2);
                sky = lerp(sky, _TopColor.rgb, g3);
                sky = lerp(_GroundColor.rgb, sky, saturate(dir.y * 18.0 + 0.5));

                // ---- 太阳光晕 ----
                float halo = pow(sd, 14.0) * 0.20 + pow(sd, 70.0) * 0.9 + pow(sd, 900.0) * 2.0;
                sky += _GlowColor.rgb * halo * _SunHalo;

                // ---- 云 ----
                float cloudMask = 0.0;
                if (dir.y > 0.002)
                {
                    float az = atan2(dir.z, dir.x);
                    float el = asin(clamp(dir.y, -1.0, 1.0));
                    float2 uv = float2(az * 1.55, el * 7.0);
                    uv += float2(_Time.y * 0.0045, _Time.y * 0.0015) * _CloudSpeed;

                    float n1 = fbm(uv * 1.05 * _CloudScale);
                    float n2 = fbm(uv * 2.30 * _CloudScale + 17.3);
                    float shape = n1 * 0.74 + n2 * 0.42;
                    float mask = smoothstep(0.44 - _CloudCoverage * 0.26, 0.94 - _CloudCoverage * 0.12, shape);
                    mask *= smoothstep(0.0, 0.022, dir.y);
                    mask = saturate(pow(mask, _CloudContrast));

                    float litEdge = pow(sd, 1.5) * 0.62 + n2 * 0.78;
                    float3 cloudCol = lerp(_CloudDark.rgb, _CloudLit.rgb, saturate(litEdge));
                    cloudCol += _GlowColor.rgb * pow(sd, 30.0) * 1.6;
                    cloudCol *= 0.78 + 0.46 * n1;

                    cloudMask = mask * 0.85;
                    sky = lerp(sky, cloudCol, cloudMask);
                }

                // ---- 银河（只在星空预设里给一点点，让夜空有层次） ----
                if (_MilkyWay > 0.0001)
                {
                    float3 bandNormal = normalize(float3(0.22, 0.48, -0.85));
                    float band = 1.0 - abs(dot(dir, bandNormal));
                    band = smoothstep(0.80, 1.0, band);
                    float az2 = atan2(dir.z, dir.x);
                    float el2 = asin(clamp(dir.y, -1.0, 1.0));
                    float mwNoise = fbm(float2(az2 * 1.6, el2 * 3.2) + 7.7);
                    float vis = band * (0.22 + 0.78 * mwNoise) * smoothstep(0.0, 0.12, dir.y);
                    sky += _StarColor.rgb * vis * _MilkyWay * (1.0 - cloudMask * 0.7);
                }

                // ---- 星空 ----
                if (_StarIntensity > 0.0001)
                {
                    float starVis = _StarIntensity * smoothstep(0.015, 0.16, dir.y) * (1.0 - cloudMask * 0.92);
                    float s1 = starLayer(dir, 48.0, _StarDensity, _StarSize, 1.7);
                    float s2 = starLayer(dir, 104.0, _StarDensity * 0.55, _StarSize * 0.72, 8.3);
                    sky += _StarColor.rgb * (s1 + s2 * 0.55) * starVis;
                }

                // ---- 太阳本体 ----
                if (_SunDisc > 0.0001)
                {
                    float disc = smoothstep(_SunSize, _SunSize + 0.00012, sunDot) * _SunDisc;
                    sky += _SunColor.rgb * disc * 10.0;
                }

                // ---- 月亮 ----
                if (_MoonGlow > 0.0001)
                {
                    sky += _MoonGlowColor.rgb * pow(saturate(moonDot), 120.0) * _MoonGlow;
                }
                if (_MoonDisc > 0.0001)
                {
                    float moonDisc = smoothstep(_MoonSize, _MoonSize + 0.00010, moonDot) * _MoonDisc;
                    sky += _MoonColor.rgb * moonDisc * 2.4 * (1.0 - cloudMask * 0.55);
                }

                return float4(max(sky, 0.0) * _SkyIntensity, 1.0);
            }
            ENDCG
        }
    }
    Fallback Off
}
