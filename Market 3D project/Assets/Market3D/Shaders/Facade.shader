// 远景建筑立面：按世界坐标生成窗格，一部分窗户亮灯（夜里/黄昏的街景感）
Shader "Market/Facade"
{
    Properties
    {
        _WallColor ("墙面色", Color) = (0.24, 0.22, 0.24, 1)
        _WinColor ("窗暗色", Color) = (0.05, 0.06, 0.09, 1)
        _LitColor ("窗亮色", Color) = (1.0, 0.72, 0.42, 1)
        _LitRatio ("亮窗比例", Range(0, 1)) = 0.32
        _WinPerMeterX ("横向窗密度", Float) = 0.55
        _WinPerMeterY ("纵向窗密度", Float) = 0.42
        _Glow ("亮窗自发光", Float) = 1.6
        _Seed ("随机种子", Float) = 3.7
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        fixed4 _WallColor, _WinColor, _LitColor;
        half _LitRatio, _WinPerMeterX, _WinPerMeterY, _Glow;
        float _Seed;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
        };

        float hash21 (float2 p)
        {
            p = frac(p * float2(123.34, 345.45));
            p += dot(p, p + 34.345);
            return frac(p.x * p.y);
        }

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            float3 n = normalize(IN.worldNormal);
            float horiz = (abs(n.x) > 0.55) ? IN.worldPos.z : IN.worldPos.x;

            float u = horiz * _WinPerMeterX;
            float v = IN.worldPos.y * _WinPerMeterY;
            float2 id = floor(float2(u, v));
            float2 g = frac(float2(u, v));

            float win = step(0.16, g.x) * step(g.x, 0.84) * step(0.26, g.y) * step(g.y, 0.82);
            win *= step(0.5, abs(n.y) < 0.5 ? 1.0 : 0.0);

            float rnd = hash21(id + _Seed);
            float lit = step(1.0 - _LitRatio, rnd) * win;

            float grain = hash21(id * 1.7 + floor(IN.worldPos.yy * 3.0));

            float3 wall = _WallColor.rgb * (0.78 + 0.45 * grain);
            // 每层楼板压一条暗线
            float band = step(0.90, frac(IN.worldPos.y * _WinPerMeterY));

            float3 col = lerp(wall, _WinColor.rgb, win * 0.92);
            col = lerp(col, col * 0.55, band * 0.6);

            o.Albedo = col;
            o.Metallic = 0.0;
            o.Smoothness = lerp(0.08, 0.45, win);
            o.Emission = _LitColor.rgb * lit * _Glow * (0.55 + 0.9 * grain);
        }
        ENDCG
    }
    FallBack "Diffuse"
}
