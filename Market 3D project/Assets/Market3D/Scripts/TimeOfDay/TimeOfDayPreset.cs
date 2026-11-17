using System;
using UnityEngine;

namespace TenPercent2D.Market3D
{
    /// <summary>
    /// 一个时间点的完整光照配方。三个预设（白天 / 黄昏 / 星空夜）由
    /// <see cref="CreateDefaults"/> 生成，之后会被序列化进场景，可在 Inspector 里微调。
    /// </summary>
    [Serializable]
    public class TimeOfDayPreset
    {
        public string name = "DAY";
        [Tooltip("这个预设对应的时钟小时（0-24），用于自动时间轴的定位和 HUD 显示")]
        public float hour = 10f;

        public SkySettings sky = new SkySettings();
        public LightSettings sunLight = new LightSettings();
        public LightSettings moonLight = new LightSettings();
        public LightSettings windowSpill = new LightSettings();
        public LightSettings fluoro = new LightSettings();
        public LightSettings streetLamp = new LightSettings();
        public LightSettings streetLampGlow = new LightSettings();

        public AmbientSettings ambient = new AmbientSettings();
        public Color fogColor = new Color(0.46f, 0.28f, 0.32f);
        public float fogDensity = 0.0062f;

        [Header("发光材质")]
        [Tooltip("立面亮窗自发光倍率（1 = 黄昏原值）")]
        public float facadeGlowScale = 1f;
        [Tooltip("店内灯管 / 冷柜 / 招牌 / POS 等自发光倍率")]
        public float emissiveScale = 1f;
        [Tooltip("路灯灯罩自发光倍率（1 = 黄昏原值）")]
        public float lampEmissionScale = 1f;
        [Tooltip("湿路面 / 人行道 / 店内瓷砖的平面反射强度倍率（1 = 黄昏原值）")]
        public float reflectionScale = 1f;

        public PostSettings post = new PostSettings();

        // ================= 子结构 =================

        [Serializable]
        public class SkySettings
        {
            [Header("竖向渐变")]
            public Color top = new Color(0.09f, 0.05f, 0.21f);
            public Color mid = new Color(0.46f, 0.155f, 0.415f);
            public Color low = new Color(0.98f, 0.40f, 0.38f);
            public Color horizon = new Color(1.00f, 0.50f, 0.19f);
            public Color ground = new Color(0.20f, 0.10f, 0.13f);

            [Header("太阳")]
            public Color glow = new Color(1.00f, 0.46f, 0.15f);
            public Color sunColor = new Color(1.00f, 0.82f, 0.52f);
            [Range(0.9f, 0.99995f)] public float sunSize = 0.99974f;
            [Range(0f, 3f)] public float sunDisc = 1f;
            [Range(0f, 3f)] public float sunHalo = 1f;

            [Header("月亮")]
            public Color moonColor = new Color(0.90f, 0.94f, 1.00f);
            public Color moonGlowColor = new Color(0.45f, 0.62f, 1.00f);
            [Range(0.9f, 0.99995f)] public float moonSize = 0.99962f;
            [Range(0f, 3f)] public float moonDisc = 0f;
            [Range(0f, 3f)] public float moonGlow = 0f;

            [Header("星空")]
            public Color starColor = new Color(0.88f, 0.93f, 1.00f);
            [Range(0f, 4f)] public float starIntensity = 0f;
            [Range(0f, 1f)] public float starDensity = 0.12f;
            [Range(0.01f, 0.5f)] public float starSize = 0.14f;
            [Range(0f, 1f)] public float starTwinkle = 0.3f;
            [Range(0f, 0.5f)] public float milkyWay = 0f;

            [Header("云")]
            public Color cloudLit = new Color(1.00f, 0.52f, 0.44f);
            public Color cloudDark = new Color(0.15f, 0.07f, 0.225f);
            [Range(0f, 1f)] public float cloudCoverage = 0.80f;
            [Range(0.5f, 3f)] public float cloudContrast = 1.30f;
            public float cloudScale = 1.05f;

            [Range(0f, 3f)] public float skyIntensity = 1f;
        }

        [Serializable]
        public class LightSettings
        {
            public Color color = Color.white;
            [Range(0f, 20f)] public float intensity = 1f;
            [Range(0f, 1f)] public float shadowStrength = 0.85f;
        }

        [Serializable]
        public class AmbientSettings
        {
            public Color sky = new Color(0.13f, 0.10f, 0.17f);
            public Color equator = new Color(0.32f, 0.19f, 0.17f);
            public Color ground = new Color(0.055f, 0.044f, 0.05f);
            [Range(0f, 3f)] public float intensity = 1f;
        }

        [Serializable]
        public class PostSettings
        {
            [Header("泛光")]
            public float bloomThreshold = 1.95f;
            public float bloomIntensity = 0.52f;
            public float bloomRadius = 1.5f;
            public Color bloomTint = new Color(1f, 0.8f, 0.7f);

            [Header("调色")]
            public float exposure = 0.96f;
            public float contrast = 1.12f;
            public float saturation = 1.20f;
            public float vignette = 0.78f;
            public float warmth = 0.06f;
        }

        // ================= 默认预设 =================

        static Color C(float r, float g, float b)
        {
            return new Color(r, g, b, 1f);
        }

        /// <summary>白天 / 黄昏 / 星空夜。数组顺序就是 HUD 上 1 / 2 / 3 的顺序。</summary>
        public static TimeOfDayPreset[] CreateDefaults()
        {
            return new[] { CreateDay(), CreateDusk(), CreateNight() };
        }

        static TimeOfDayPreset CreateDay()
        {
            return new TimeOfDayPreset
            {
                name = "DAY",
                hour = 14f,
                sky = new SkySettings
                {
                    top = C(0.090f, 0.280f, 0.720f),
                    mid = C(0.180f, 0.430f, 0.860f),
                    low = C(0.340f, 0.600f, 0.920f),
                    horizon = C(0.580f, 0.760f, 0.940f),
                    ground = C(0.240f, 0.260f, 0.300f),
                    glow = C(1.000f, 0.940f, 0.820f),
                    sunColor = C(1.000f, 0.970f, 0.900f),
                    sunSize = 0.99978f,
                    sunDisc = 0.85f,
                    sunHalo = 0.55f,
                    moonColor = C(0.85f, 0.90f, 1f),
                    moonGlowColor = C(0.55f, 0.70f, 1f),
                    moonSize = 0.99962f,
                    moonDisc = 0f,
                    moonGlow = 0f,
                    starColor = C(0.85f, 0.90f, 1f),
                    starIntensity = 0f,
                    starDensity = 0f,
                    starSize = 0.14f,
                    starTwinkle = 0f,
                    milkyWay = 0f,
                    cloudLit = C(1.000f, 1.000f, 1.000f),
                    cloudDark = C(0.550f, 0.630f, 0.760f),
                    cloudCoverage = 0.32f,
                    cloudContrast = 1.90f,
                    cloudScale = 1.45f,
                    skyIntensity = 1.05f
                },
                sunLight = new LightSettings { color = C(1.00f, 0.95f, 0.87f), intensity = 1.30f, shadowStrength = 0.60f },
                moonLight = new LightSettings { color = C(0.45f, 0.58f, 1.00f), intensity = 0f },
                windowSpill = new LightSettings { color = C(0.82f, 0.90f, 1.00f), intensity = 5.20f },
                fluoro = new LightSettings { color = C(0.90f, 0.95f, 1.00f), intensity = 1.80f },
                streetLamp = new LightSettings { color = C(1.00f, 0.72f, 0.42f), intensity = 0f },
                streetLampGlow = new LightSettings { color = C(1.00f, 0.80f, 0.55f), intensity = 0f },
                ambient = new AmbientSettings
                {
                    sky = C(0.360f, 0.480f, 0.680f),
                    equator = C(0.520f, 0.550f, 0.600f),
                    ground = C(0.220f, 0.210f, 0.200f),
                    intensity = 1.25f
                },
                fogColor = C(0.550f, 0.660f, 0.800f),
                fogDensity = 0.0026f,
                facadeGlowScale = 0.03f,
                emissiveScale = 0.30f,
                lampEmissionScale = 0.02f,
                reflectionScale = 0.22f,
                post = new PostSettings
                {
                    bloomThreshold = 1.55f,
                    bloomIntensity = 0.25f,
                    bloomRadius = 1.2f,
                    bloomTint = C(1.00f, 0.97f, 0.92f),
                    exposure = 0.98f,
                    contrast = 1.03f,
                    saturation = 1.05f,
                    vignette = 0.35f,
                    warmth = 0f
                }
            };
        }

        static TimeOfDayPreset CreateDusk()
        {
            return new TimeOfDayPreset
            {
                name = "DUSK",
                hour = 18.34f,
                sky = new SkySettings
                {
                    top = C(0.090f, 0.050f, 0.210f),
                    mid = C(0.460f, 0.155f, 0.415f),
                    low = C(0.980f, 0.400f, 0.380f),
                    horizon = C(1.00f, 0.50f, 0.19f),
                    ground = C(0.20f, 0.10f, 0.13f),
                    glow = C(1.00f, 0.46f, 0.15f),
                    sunColor = C(1.00f, 0.82f, 0.52f),
                    sunSize = 0.99974f,
                    sunDisc = 1f,
                    sunHalo = 1f,
                    moonColor = C(0.90f, 0.94f, 1.00f),
                    moonGlowColor = C(0.45f, 0.62f, 1.00f),
                    moonSize = 0.99962f,
                    moonDisc = 0f,
                    moonGlow = 0f,
                    starColor = C(0.88f, 0.93f, 1.00f),
                    starIntensity = 0f,
                    starDensity = 0f,
                    starSize = 0.14f,
                    starTwinkle = 0f,
                    milkyWay = 0f,
                    cloudLit = C(1.00f, 0.52f, 0.44f),
                    cloudDark = C(0.150f, 0.070f, 0.225f),
                    cloudCoverage = 0.80f,
                    cloudContrast = 1.30f,
                    cloudScale = 1.05f,
                    skyIntensity = 1.00f
                },
                sunLight = new LightSettings { color = C(1.00f, 0.62f, 0.36f), intensity = 1.75f },
                moonLight = new LightSettings { color = C(0.45f, 0.58f, 1.00f), intensity = 0.02f },
                windowSpill = new LightSettings { color = C(1.00f, 0.54f, 0.26f), intensity = 2.80f },
                fluoro = new LightSettings { color = C(1.00f, 0.94f, 0.86f), intensity = 1.85f },
                streetLamp = new LightSettings { color = C(1.00f, 0.72f, 0.42f), intensity = 0.60f },
                streetLampGlow = new LightSettings { color = C(1.00f, 0.80f, 0.55f), intensity = 0.20f },
                ambient = new AmbientSettings
                {
                    sky = C(0.130f, 0.100f, 0.170f),
                    equator = C(0.320f, 0.190f, 0.170f),
                    ground = C(0.055f, 0.044f, 0.050f),
                    intensity = 1f
                },
                fogColor = C(0.460f, 0.280f, 0.320f),
                fogDensity = 0.0062f,
                facadeGlowScale = 1.00f,
                emissiveScale = 1.00f,
                lampEmissionScale = 1.00f,
                reflectionScale = 1.00f,
                post = new PostSettings
                {
                    bloomThreshold = 1.95f,
                    bloomIntensity = 0.52f,
                    bloomRadius = 1.5f,
                    bloomTint = C(1.00f, 0.80f, 0.70f),
                    exposure = 0.96f,
                    contrast = 1.12f,
                    saturation = 1.20f,
                    vignette = 0.78f,
                    warmth = 0.06f
                }
            };
        }

        static TimeOfDayPreset CreateNight()
        {
            return new TimeOfDayPreset
            {
                name = "STARRY NIGHT",
                hour = 23f,
                sky = new SkySettings
                {
                    top = C(0.008f, 0.014f, 0.050f),
                    mid = C(0.018f, 0.032f, 0.082f),
                    low = C(0.038f, 0.058f, 0.120f),
                    horizon = C(0.075f, 0.095f, 0.165f),
                    ground = C(0.008f, 0.010f, 0.018f),
                    glow = C(0.070f, 0.110f, 0.240f),
                    sunColor = C(1.00f, 0.82f, 0.52f),
                    sunSize = 0.99974f,
                    sunDisc = 0f,
                    sunHalo = 0f,
                    moonColor = C(0.88f, 0.93f, 1.00f),
                    moonGlowColor = C(0.42f, 0.60f, 1.00f),
                    moonSize = 0.99960f,
                    moonDisc = 1.00f,
                    moonGlow = 0.45f,
                    starColor = C(0.88f, 0.93f, 1.00f),
                    starIntensity = 1.80f,
                    starDensity = 0.14f,
                    starSize = 0.14f,
                    starTwinkle = 0.30f,
                    milkyWay = 0.045f,
                    cloudLit = C(0.085f, 0.110f, 0.175f),
                    cloudDark = C(0.012f, 0.016f, 0.030f),
                    cloudCoverage = 0.22f,
                    cloudContrast = 1.45f,
                    cloudScale = 1.10f,
                    skyIntensity = 0.90f
                },
                sunLight = new LightSettings { color = C(1.00f, 0.62f, 0.36f), intensity = 0f },
                moonLight = new LightSettings { color = C(0.45f, 0.58f, 1.00f), intensity = 0.30f, shadowStrength = 0.55f },
                windowSpill = new LightSettings { color = C(1.00f, 0.55f, 0.28f), intensity = 1.55f },
                fluoro = new LightSettings { color = C(1.00f, 0.94f, 0.86f), intensity = 2.10f },
                streetLamp = new LightSettings { color = C(1.00f, 0.72f, 0.42f), intensity = 3.40f },
                streetLampGlow = new LightSettings { color = C(1.00f, 0.80f, 0.55f), intensity = 1.50f },
                ambient = new AmbientSettings
                {
                    sky = C(0.035f, 0.050f, 0.100f),
                    equator = C(0.055f, 0.065f, 0.090f),
                    ground = C(0.008f, 0.009f, 0.014f),
                    intensity = 1f
                },
                fogColor = C(0.035f, 0.050f, 0.100f),
                fogDensity = 0.0075f,
                facadeGlowScale = 1.15f,
                emissiveScale = 1.05f,
                lampEmissionScale = 1.80f,
                reflectionScale = 0.65f,
                post = new PostSettings
                {
                    bloomThreshold = 1.35f,
                    bloomIntensity = 0.78f,
                    bloomRadius = 1.6f,
                    bloomTint = C(1.00f, 0.78f, 0.60f),
                    exposure = 0.98f,
                    contrast = 1.10f,
                    saturation = 1.12f,
                    vignette = 0.82f,
                    warmth = 0.05f
                }
            };
        }
    }
}
