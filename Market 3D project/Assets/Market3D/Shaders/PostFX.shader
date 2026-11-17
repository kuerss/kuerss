// Market 场景专用小型后期：高光泛光 + 电影化调色 + 暗角
Shader "Market/PostFX"
{
    Properties
    {
        _MainTex ("Source", 2D) = "white" {}
        _BloomTex ("Bloom", 2D) = "black" {}
        _Threshold ("泛光阈值", Float) = 1.0
        _Intensity ("泛光强度", Float) = 0.85
        _BlurDir ("_BlurDir", Vector) = (0, 0, 0, 0)
        _Exposure ("曝光", Float) = 1.0
        _Contrast ("对比度", Float) = 1.05
        _Saturation ("饱和度", Float) = 1.10
        _Vignette ("暗角", Float) = 0.55
        _Warmth ("暖调", Float) = 0.04
        _Tint ("泛光染色", Color) = (1.0, 0.85, 0.78, 1)
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        // ---- 0: 亮部提取 ----
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _Threshold;

            float4 frag (v2f_img i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                float br = max(c.r, max(c.g, c.b));
                float w = saturate((br - _Threshold) / max(br, 1e-4));
                return float4(c.rgb * w, 1.0);
            }
            ENDCG
        }

        // ---- 1: 高斯模糊（双向） ----
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _BlurDir;

            float4 frag (v2f_img i) : SV_Target
            {
                float2 d = _BlurDir.xy;
                float4 s = tex2D(_MainTex, i.uv) * 0.227027;
                s += (tex2D(_MainTex, i.uv + d * 1.3846) + tex2D(_MainTex, i.uv - d * 1.3846)) * 0.316216;
                s += (tex2D(_MainTex, i.uv + d * 3.2308) + tex2D(_MainTex, i.uv - d * 3.2308)) * 0.070270;
                return s;
            }
            ENDCG
        }

        // ---- 2: 合成 + 调色 ----
        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _BloomTex;
            float _Intensity, _Exposure, _Contrast, _Saturation, _Vignette, _Warmth;
            float4 _Tint;

            float4 frag (v2f_img i) : SV_Target
            {
                float4 c = tex2D(_MainTex, i.uv);
                float3 bloom = tex2D(_BloomTex, i.uv).rgb * _Tint.rgb * _Intensity;

                float3 col = (c.rgb + bloom) * _Exposure;
                col = col / (1.0 + col * 0.30);

                float lum = dot(col, float3(0.299, 0.587, 0.114));
                col = lerp(float3(lum, lum, lum), col, _Saturation);
                col = (col - 0.5) * _Contrast + 0.5;

                // 高光偏暖、暗部偏青紫
                col += _Warmth * saturate(lum - 0.35) * float3(1.0, 0.45, 0.1);
                col -= _Warmth * 0.6 * saturate(0.35 - lum) * float3(0.1, 0.0, -0.35);

                float2 d = i.uv - 0.5;
                float v = saturate(1.0 - dot(d, d) * (_Vignette * 2.4));
                col *= v;

                return float4(saturate(col), 1.0);
            }
            ENDCG
        }
    }
    FallBack Off
}
