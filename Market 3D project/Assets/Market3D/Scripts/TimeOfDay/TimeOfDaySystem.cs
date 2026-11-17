using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace TenPercent2D.Market3D
{
    /// <summary>
    /// 一条 24 小时时间轴：白天(10:00) → 黄昏(18:20) → 星空夜(23:00)。
    /// 天空盒、太阳/月亮、环境光、雾、店内灯、路灯、发光材质、后期全部按预设插值。
    ///
    /// 运行方式（Play 模式）：
    ///   1 / 2 / 3  平滑切到 白天 / 黄昏 / 星空夜
    ///   T          开关自动时间流逝（默认关，方便定住看）
    ///   Space      切到下一个时段
    ///   ← / →      时间 ±15 分钟
    ///   H          显示 / 隐藏左下角 HUD
    /// </summary>
    [DisallowMultipleComponent]
    public class TimeOfDaySystem : MonoBehaviour
    {
        public const float HoursPerDay = 24f;
        const string LampGlassMaterialName = "M_LampGlass";

        [Serializable]
        public struct Keyframe
        {
            [Range(0f, 24f)] public float hour;
            public int preset;
        }

        [Header("时间轴")]
        [Range(0f, 24f)] public float startHour = 18.34f;
        [Tooltip("打开后时钟自动走，场景随时间连续变化")]
        public bool autoAdvance = false;
        [Tooltip("自动走完一整天需要的秒数")]
        public float dayLengthSeconds = 120f;
        [Tooltip("手动切时段时的过渡时长（秒）")]
        public float manualBlendSeconds = 3.5f;

        [Tooltip("三个预设：0=白天，1=黄昏，2=星空夜")]
        public TimeOfDayPreset[] presets;
        [Tooltip("时间轴关键帧（小时 → 预设），按小时升序；两端会自动环绕")]
        public Keyframe[] keyframes;
        public bool showHud = true;

        [Header("场景引用")]
        public Material skyMaterial;
        public Light sunLight;
        public Light moonLight;
        public Light windowSpillLight;
        public Light[] fluoroLights = new Light[0];
        public Light[] streetLampSpots = new Light[0];
        public Light[] streetLampGlows = new Light[0];
        public Material[] facadeMaterials = new Material[0];
        [Tooltip("需要随昼夜调自发光强度的材质名（灯罩 M_LampGlass 单独处理）")]
        public string[] emissiveMaterialNames = new string[0];
        public MarketPostFX postFX;

        // ---- 运行时 ----
        Material m_SkyInstance;
        Material m_OriginalSkybox;
        bool m_Ready;
        bool m_Blending;
        float m_BlendTime;
        float m_BlendDuration;
        float m_Clock;
        float m_DisplayHour;
        float m_BlendStartHour;
        float m_BlendTargetHour;
        float m_BlendDelta;
        int m_TargetPreset;

        readonly List<FacadeTarget> m_Facades = new List<FacadeTarget>();
        readonly List<EmissionTarget> m_Emissives = new List<EmissionTarget>();
        readonly List<FloatTarget> m_Reflections = new List<FloatTarget>();
        EmissionTarget m_LampGlass;

        // ================= 公共属性（HUD 用） =================
        public float DisplayHour => m_DisplayHour;
        public bool IsAuto => autoAdvance;
        public bool IsBlending => m_Blending;
        public int CurrentPresetIndex
        {
            get
            {
                if (m_Blending && presets != null && presets.Length > 0) return Mathf.Clamp(m_TargetPreset, 0, presets.Length - 1);
                return NearestPresetIndex(m_DisplayHour);
            }
        }
        public string CurrentPresetName
        {
            get
            {
                if (presets == null || presets.Length == 0) return "UNKNOWN";
                return presets[Mathf.Clamp(CurrentPresetIndex, 0, presets.Length - 1)].name;
            }
        }

        // ================= 默认数据 =================
        public static readonly string[] DefaultEmissiveMaterials =
        {
            "M_FluoroTube", "M_CoolerGlow", "M_AppSign", "M_POSScreen",
            "M_VendingPanel_VendingBlue", "M_VendingPanel_VendingRed",
            "M_LawsonBlue", "M_Poster", "M_PriceTag", "M_PaperSticker"
        };

        public static Keyframe[] CreateDefaultKeyframes()
        {
            // 0-6 深夜 / 6-8 天亮 / 8-17 白天 / 17-18:20 日落 / 18:20-19:30 黄昏 / 19:30-21 入夜 / 21-24 深夜
            return new[]
            {
                new Keyframe { hour = 0f, preset = 2 },
                new Keyframe { hour = 4f, preset = 2 },
                new Keyframe { hour = 6f, preset = 2 },
                new Keyframe { hour = 8f, preset = 0 },
                new Keyframe { hour = 17f, preset = 0 },
                new Keyframe { hour = 18.34f, preset = 1 },
                new Keyframe { hour = 19.5f, preset = 1 },
                new Keyframe { hour = 21f, preset = 2 }
            };
        }

        // ================= 生命周期 =================
        void Awake()
        {
            if (!Application.isPlaying) return;
            Initialize();
        }

        void OnDestroy()
        {
            if (m_SkyInstance != null)
            {
                if (RenderSettings.skybox == m_SkyInstance) RenderSettings.skybox = m_OriginalSkybox;
                Destroy(m_SkyInstance);
                m_SkyInstance = null;
            }
        }

        void Initialize()
        {
            if (presets == null || presets.Length == 0) presets = TimeOfDayPreset.CreateDefaults();
            if (keyframes == null || keyframes.Length < 2) keyframes = CreateDefaultKeyframes();
            if (emissiveMaterialNames == null || emissiveMaterialNames.Length == 0)
                emissiveMaterialNames = (string[])DefaultEmissiveMaterials.Clone();
            if (postFX == null && Camera.main != null) postFX = Camera.main.GetComponent<MarketPostFX>();
            if (skyMaterial == null) skyMaterial = RenderSettings.skybox;

            m_OriginalSkybox = RenderSettings.skybox;
            if (skyMaterial != null)
            {
                m_SkyInstance = new Material(skyMaterial) { hideFlags = HideFlags.DontSave };
                RenderSettings.skybox = m_SkyInstance;
            }

            CacheTargets();

            m_Clock = Mathf.Repeat(startHour, HoursPerDay);
            m_DisplayHour = m_Clock;
            ApplyState(EvaluateAtHour(m_Clock));
            m_Ready = true;
        }

        // ================= 每帧 =================
        void Update()
        {
            if (!Application.isPlaying || !m_Ready) return;

            HandleInput();

            if (m_Blending)
            {
                m_BlendTime += Time.deltaTime;
                float t = m_BlendDuration > 0.0001f ? Mathf.Clamp01(m_BlendTime / m_BlendDuration) : 1f;
                float st = Smooth(t);

                // 手动切时段 = 把时钟平滑推到目标时刻，画面始终按时间轴求值。
                // 这样白天→星空会先经过黄昏，而不是两套光直接对撞。
                m_DisplayHour = Mathf.Repeat(m_BlendStartHour + m_BlendDelta * st, HoursPerDay);
                ApplyState(EvaluateAtHour(m_DisplayHour));

                if (t >= 1f)
                {
                    m_Blending = false;
                    m_Clock = Mathf.Repeat(m_BlendTargetHour, HoursPerDay);
                    m_DisplayHour = m_Clock;
                }
            }
            else if (autoAdvance)
            {
                m_Clock = Mathf.Repeat(m_Clock + Time.deltaTime * HoursPerDay / Mathf.Max(10f, dayLengthSeconds), HoursPerDay);
                m_DisplayHour = m_Clock;
                ApplyState(EvaluateAtHour(m_Clock));
            }
        }

        void HandleInput()
        {
            if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) JumpToPreset(0);
            if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) JumpToPreset(1);
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) JumpToPreset(2);
            if (Input.GetKeyDown(KeyCode.Space) && presets != null && presets.Length > 0)
                JumpToPreset((CurrentPresetIndex + 1) % presets.Length);
            if (Input.GetKeyDown(KeyCode.T)) ToggleAuto();
            if (Input.GetKeyDown(KeyCode.H)) showHud = !showHud;
            if (Input.GetKeyDown(KeyCode.LeftArrow)) NudgeHour(-0.25f);
            if (Input.GetKeyDown(KeyCode.RightArrow)) NudgeHour(0.25f);
        }

        // ================= 控制 =================
        public void JumpToPreset(int index)
        {
            if (presets == null || presets.Length == 0) return;
            index = Mathf.Clamp(index, 0, presets.Length - 1);

            autoAdvance = false;
            m_TargetPreset = index;
            m_BlendStartHour = m_DisplayHour;
            m_BlendTargetHour = Mathf.Repeat(presets[index].hour, HoursPerDay);
            m_BlendDelta = ChooseBlendDelta(m_BlendStartHour, m_BlendTargetHour, index);
            m_BlendDuration = Mathf.Max(0.01f, manualBlendSeconds);
            m_BlendTime = 0f;
            m_Blending = true;
        }

        public void ToggleAuto()
        {
            autoAdvance = !autoAdvance;
            if (autoAdvance)
            {
                m_Blending = false;
                m_Clock = m_DisplayHour;
            }
        }

        public void NudgeHour(float delta)
        {
            autoAdvance = false;
            m_Blending = false;
            m_Clock = Mathf.Repeat(m_Clock + delta, HoursPerDay);
            m_DisplayHour = m_Clock;
            ApplyState(EvaluateAtHour(m_Clock));
        }

        /// <summary>直接把时钟拨到某个小时（HUD 上拖动时间条用）。</summary>
        public void SetHour(float hour)
        {
            autoAdvance = false;
            m_Blending = false;
            m_Clock = Mathf.Repeat(hour, HoursPerDay);
            m_DisplayHour = m_Clock;
            ApplyState(EvaluateAtHour(m_Clock));
        }

        /// <summary>编辑模式下立即套用某个预设（用于生成三时段预览图）。</summary>
        public void ApplyPresetImmediate(int index)
        {
            if (presets == null || presets.Length == 0)
            {
                Debug.LogWarning("[TimeOfDay] 没有预设，先调用 Configure 或赋值 presets。");
                return;
            }
            index = Mathf.Clamp(index, 0, presets.Length - 1);
            if (emissiveMaterialNames == null || emissiveMaterialNames.Length == 0)
                emissiveMaterialNames = (string[])DefaultEmissiveMaterials.Clone();
            if (skyMaterial == null) skyMaterial = RenderSettings.skybox;

            CacheTargets();
            TodState state = FromPreset(presets[index]);
            ApplyCelestialPaths(ref state, presets[index].hour);
            m_Clock = Mathf.Repeat(presets[index].hour, HoursPerDay);
            m_DisplayHour = m_Clock;
            ApplyState(state);
        }

        // ================= 太阳 / 月亮轨迹 =================
        // 把街道朝向当成“西”：+z 是西（落日方向），+x 是北，+y 是天顶。
        // 太阳绕天极 P 转（P 在正北、高度 45°），18:20 恰好经过原来黄昏的太阳位置；
        // 月亮同样绕 P 转，23:00 恰好经过原来星空夜的月亮位置。
        // 这样 10:00 / 15:00 / 17:00 / 19:00 的太阳高度都按真实日轨变化，
        // 不再是三套预设的太阳位置硬插值。
        public const float DuskSunHour = 18.34f;
        public const float NightMoonHour = 23f;
        public const float CelestialDegreesPerHour = 15f;
        public const float CelestialPoleAltitude = 45f;

        static readonly Vector3 CelestialPole = new Vector3(
            Mathf.Cos(CelestialPoleAltitude * Mathf.Deg2Rad),
            Mathf.Sin(CelestialPoleAltitude * Mathf.Deg2Rad),
            0f).normalized;

        static readonly Vector3 DuskSunDir = new Vector3(-0.055f, 0.078f, 1f).normalized;
        static readonly Vector3 NightMoonDir = new Vector3(-0.12f, 0.25f, 1f).normalized;

        public static Vector3 SunDirection(float hour)
        {
            return (Quaternion.AngleAxis((hour - DuskSunHour) * CelestialDegreesPerHour, CelestialPole) * DuskSunDir).normalized;
        }

        public static Vector3 MoonDirection(float hour)
        {
            return (Quaternion.AngleAxis((hour - NightMoonHour) * CelestialDegreesPerHour, CelestialPole) * NightMoonDir).normalized;
        }

        static float HorizonVisibility(Vector3 dir)
        {
            // 太阳/月亮落到地平线以下就收掉本体和光照，避免“地下月亮”继续打光。
            return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.06f, 0.03f, dir.y));
        }

        static void ApplyCelestialPaths(ref TodState s, float hour)
        {
            s.sunDir = SunDirection(hour);
            s.moonDir = MoonDirection(hour);

            float sunVis = HorizonVisibility(s.sunDir);
            float moonVis = HorizonVisibility(s.moonDir);

            s.sunLightIntensity *= sunVis;
            s.sunDisc *= sunVis;
            s.sunHalo *= sunVis;

            s.moonLightIntensity *= moonVis;
            s.moonDisc *= moonVis;
            s.moonGlow *= moonVis;
        }

        // ================= 时间轴求值 =================
        public TodState EvaluateAtHour(float hour)
        {
            if (presets == null || presets.Length == 0) return default;
            if (keyframes == null || keyframes.Length < 2)
            {
                TodState fallback = FromPreset(presets[0]);
                ApplyCelestialPaths(ref fallback, hour);
                return fallback;
            }

            float h = Mathf.Repeat(hour, HoursPerDay);
            int n = keyframes.Length;

            for (int i = 0; i < n; i++)
            {
                Keyframe a = keyframes[i];
                Keyframe b = keyframes[(i + 1) % n];

                float ah = a.hour;
                float bh = b.hour;
                if (bh <= ah) bh += HoursPerDay;

                float hh = h;
                if (hh < ah) hh += HoursPerDay;

                if (hh < ah || hh > bh) continue;

                float t = Mathf.InverseLerp(ah, bh, hh);
                TimeOfDayPreset pa = presets[Mathf.Clamp(a.preset, 0, presets.Length - 1)];
                TimeOfDayPreset pb = presets[Mathf.Clamp(b.preset, 0, presets.Length - 1)];
                TodState state = Blend(FromPreset(pa), FromPreset(pb), Smooth(t));
                ApplyCelestialPaths(ref state, h);
                return state;
            }

            TodState fallbackState = FromPreset(presets[0]);
            ApplyCelestialPaths(ref fallbackState, h);
            return fallbackState;
        }

        int NearestPresetIndex(float hour)
        {
            if (presets == null || presets.Length == 0) return 0;

            // 先按时间轴关键帧找“当前更接近哪一端”，这样 16:00 不会被标成 DUSK。
            if (keyframes != null && keyframes.Length > 1)
            {
                float h = Mathf.Repeat(hour, HoursPerDay);
                int n = keyframes.Length;
                for (int i = 0; i < n; i++)
                {
                    Keyframe a = keyframes[i];
                    Keyframe b = keyframes[(i + 1) % n];
                    float ah = a.hour;
                    float bh = b.hour;
                    if (bh <= ah) bh += HoursPerDay;
                    float hh = h < ah ? h + HoursPerDay : h;
                    if (hh < ah || hh > bh) continue;

                    float t = Mathf.InverseLerp(ah, bh, hh);
                    int index = t < 0.5f ? a.preset : b.preset;
                    return Mathf.Clamp(index, 0, presets.Length - 1);
                }
            }

            float best = float.MaxValue;
            int bestIndex = 0;
            for (int i = 0; i < presets.Length; i++)
            {
                float delta = Mathf.Abs(Mathf.DeltaAngle(hour * 15f, presets[i].hour * 15f)) / 15f;
                if (delta < best)
                {
                    best = delta;
                    bestIndex = i;
                }
            }
            return bestIndex;
        }

        static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        static float ChooseBlendDelta(float from, float to, int targetPreset)
        {
            float forward = Mathf.Repeat(to - from, HoursPerDay);
            float backward = forward - HoursPerDay;

            // 白天 <-> 星空夜：强制走“日落/黎明”那条路，不要把时间倒着拨回清晨。
            if (targetPreset == 0 && (from >= 19f || from < 6f)) return forward;
            if (targetPreset == 2 && from >= 6f && from < 21f) return forward;

            return Mathf.Abs(forward) <= Mathf.Abs(backward) ? forward : backward;
        }

        // ================= 预设 → 状态 =================
        public struct TodState
        {
            public Color skyTop, skyMid, skyLow, skyHorizon, skyGround, skyGlow;
            public Color skySun, skyMoon, skyMoonGlow, skyStar, skyCloudLit, skyCloudDark;
            public Vector3 sunDir, moonDir;
            public float sunSize, sunDisc, sunHalo;
            public float moonSize, moonDisc, moonGlow;
            public float starIntensity, starDensity, starSize, starTwinkle, milkyWay;
            public float cloudCoverage, cloudContrast, cloudScale, skyIntensity;

            public Color sunLightColor, moonLightColor;
            public float sunLightIntensity, moonLightIntensity;
            public float sunShadowStrength, moonShadowStrength;

            public Color windowSpillColor;
            public float windowSpillIntensity;
            public Color fluoroColor;
            public float fluoroIntensity;
            public Color streetLampColor;
            public float streetLampIntensity, streetLampGlowIntensity;

            public Color ambientSky, ambientEquator, ambientGround;
            public float ambientIntensity;
            public Color fogColor;
            public float fogDensity;

            public float facadeGlowScale, emissiveScale, lampEmissionScale, reflectionScale;

            public float bloomThreshold, bloomIntensity, bloomRadius;
            public Color bloomTint;
            public float exposure, contrast, saturation, vignette, warmth;
        }

        static TodState FromPreset(TimeOfDayPreset p)
        {
            var s = new TodState();
            TimeOfDayPreset.SkySettings k = p.sky ?? new TimeOfDayPreset.SkySettings();

            s.skyTop = k.top;
            s.skyMid = k.mid;
            s.skyLow = k.low;
            s.skyHorizon = k.horizon;
            s.skyGround = k.ground;
            s.skyGlow = k.glow;
            s.skySun = k.sunColor;
            s.skyMoon = k.moonColor;
            s.skyMoonGlow = k.moonGlowColor;
            s.skyStar = k.starColor;
            s.skyCloudLit = k.cloudLit;
            s.skyCloudDark = k.cloudDark;

            s.sunSize = k.sunSize;
            s.sunDisc = k.sunDisc;
            s.sunHalo = k.sunHalo;
            s.moonSize = k.moonSize;
            s.moonDisc = k.moonDisc;
            s.moonGlow = k.moonGlow;
            s.starIntensity = k.starIntensity;
            s.starDensity = k.starDensity;
            s.starSize = k.starSize;
            s.starTwinkle = k.starTwinkle;
            s.milkyWay = k.milkyWay;
            s.cloudCoverage = k.cloudCoverage;
            s.cloudContrast = k.cloudContrast;
            s.cloudScale = k.cloudScale;
            s.skyIntensity = k.skyIntensity;

            s.sunLightColor = p.sunLight.color;
            s.sunLightIntensity = p.sunLight.intensity;
            s.sunShadowStrength = p.sunLight.shadowStrength;
            s.moonLightColor = p.moonLight.color;
            s.moonLightIntensity = p.moonLight.intensity;
            s.moonShadowStrength = p.moonLight.shadowStrength;

            s.windowSpillColor = p.windowSpill.color;
            s.windowSpillIntensity = p.windowSpill.intensity;
            s.fluoroColor = p.fluoro.color;
            s.fluoroIntensity = p.fluoro.intensity;
            s.streetLampColor = p.streetLamp.color;
            s.streetLampIntensity = p.streetLamp.intensity;
            s.streetLampGlowIntensity = p.streetLampGlow.intensity;

            s.ambientSky = p.ambient.sky;
            s.ambientEquator = p.ambient.equator;
            s.ambientGround = p.ambient.ground;
            s.ambientIntensity = p.ambient.intensity;
            s.fogColor = p.fogColor;
            s.fogDensity = p.fogDensity;

            s.facadeGlowScale = p.facadeGlowScale;
            s.emissiveScale = p.emissiveScale;
            s.lampEmissionScale = p.lampEmissionScale;
            s.reflectionScale = p.reflectionScale;

            s.bloomThreshold = p.post.bloomThreshold;
            s.bloomIntensity = p.post.bloomIntensity;
            s.bloomRadius = p.post.bloomRadius;
            s.bloomTint = p.post.bloomTint;
            s.exposure = p.post.exposure;
            s.contrast = p.post.contrast;
            s.saturation = p.post.saturation;
            s.vignette = p.post.vignette;
            s.warmth = p.post.warmth;
            return s;
        }

        static TodState Blend(TodState a, TodState b, float t)
        {
            t = Smooth(t);
            var s = new TodState();

            s.skyTop = Color.Lerp(a.skyTop, b.skyTop, t);
            s.skyMid = Color.Lerp(a.skyMid, b.skyMid, t);
            s.skyLow = Color.Lerp(a.skyLow, b.skyLow, t);
            s.skyHorizon = Color.Lerp(a.skyHorizon, b.skyHorizon, t);
            s.skyGround = Color.Lerp(a.skyGround, b.skyGround, t);
            s.skyGlow = Color.Lerp(a.skyGlow, b.skyGlow, t);
            s.skySun = Color.Lerp(a.skySun, b.skySun, t);
            s.skyMoon = Color.Lerp(a.skyMoon, b.skyMoon, t);
            s.skyMoonGlow = Color.Lerp(a.skyMoonGlow, b.skyMoonGlow, t);
            s.skyStar = Color.Lerp(a.skyStar, b.skyStar, t);
            s.skyCloudLit = Color.Lerp(a.skyCloudLit, b.skyCloudLit, t);
            s.skyCloudDark = Color.Lerp(a.skyCloudDark, b.skyCloudDark, t);

            s.sunSize = Mathf.Lerp(a.sunSize, b.sunSize, t);
            s.sunDisc = Mathf.Lerp(a.sunDisc, b.sunDisc, t);
            s.sunHalo = Mathf.Lerp(a.sunHalo, b.sunHalo, t);
            s.moonSize = Mathf.Lerp(a.moonSize, b.moonSize, t);
            s.moonDisc = Mathf.Lerp(a.moonDisc, b.moonDisc, t);
            s.moonGlow = Mathf.Lerp(a.moonGlow, b.moonGlow, t);
            s.starIntensity = Mathf.Lerp(a.starIntensity, b.starIntensity, t);
            s.starDensity = Mathf.Lerp(a.starDensity, b.starDensity, t);
            s.starSize = Mathf.Lerp(a.starSize, b.starSize, t);
            s.starTwinkle = Mathf.Lerp(a.starTwinkle, b.starTwinkle, t);
            s.milkyWay = Mathf.Lerp(a.milkyWay, b.milkyWay, t);
            s.cloudCoverage = Mathf.Lerp(a.cloudCoverage, b.cloudCoverage, t);
            s.cloudContrast = Mathf.Lerp(a.cloudContrast, b.cloudContrast, t);
            s.cloudScale = Mathf.Lerp(a.cloudScale, b.cloudScale, t);
            s.skyIntensity = Mathf.Lerp(a.skyIntensity, b.skyIntensity, t);

            s.sunLightColor = Color.Lerp(a.sunLightColor, b.sunLightColor, t);
            s.sunLightIntensity = Mathf.Lerp(a.sunLightIntensity, b.sunLightIntensity, t);
            s.sunShadowStrength = Mathf.Lerp(a.sunShadowStrength, b.sunShadowStrength, t);
            s.moonLightColor = Color.Lerp(a.moonLightColor, b.moonLightColor, t);
            s.moonLightIntensity = Mathf.Lerp(a.moonLightIntensity, b.moonLightIntensity, t);
            s.moonShadowStrength = Mathf.Lerp(a.moonShadowStrength, b.moonShadowStrength, t);

            s.windowSpillColor = Color.Lerp(a.windowSpillColor, b.windowSpillColor, t);
            s.windowSpillIntensity = Mathf.Lerp(a.windowSpillIntensity, b.windowSpillIntensity, t);
            s.fluoroColor = Color.Lerp(a.fluoroColor, b.fluoroColor, t);
            s.fluoroIntensity = Mathf.Lerp(a.fluoroIntensity, b.fluoroIntensity, t);
            s.streetLampColor = Color.Lerp(a.streetLampColor, b.streetLampColor, t);
            s.streetLampIntensity = Mathf.Lerp(a.streetLampIntensity, b.streetLampIntensity, t);
            s.streetLampGlowIntensity = Mathf.Lerp(a.streetLampGlowIntensity, b.streetLampGlowIntensity, t);

            s.ambientSky = Color.Lerp(a.ambientSky, b.ambientSky, t);
            s.ambientEquator = Color.Lerp(a.ambientEquator, b.ambientEquator, t);
            s.ambientGround = Color.Lerp(a.ambientGround, b.ambientGround, t);
            s.ambientIntensity = Mathf.Lerp(a.ambientIntensity, b.ambientIntensity, t);
            s.fogColor = Color.Lerp(a.fogColor, b.fogColor, t);
            s.fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, t);

            s.facadeGlowScale = Mathf.Lerp(a.facadeGlowScale, b.facadeGlowScale, t);
            s.emissiveScale = Mathf.Lerp(a.emissiveScale, b.emissiveScale, t);
            s.lampEmissionScale = Mathf.Lerp(a.lampEmissionScale, b.lampEmissionScale, t);
            s.reflectionScale = Mathf.Lerp(a.reflectionScale, b.reflectionScale, t);

            s.bloomThreshold = Mathf.Lerp(a.bloomThreshold, b.bloomThreshold, t);
            s.bloomIntensity = Mathf.Lerp(a.bloomIntensity, b.bloomIntensity, t);
            s.bloomRadius = Mathf.Lerp(a.bloomRadius, b.bloomRadius, t);
            s.bloomTint = Color.Lerp(a.bloomTint, b.bloomTint, t);
            s.exposure = Mathf.Lerp(a.exposure, b.exposure, t);
            s.contrast = Mathf.Lerp(a.contrast, b.contrast, t);
            s.saturation = Mathf.Lerp(a.saturation, b.saturation, t);
            s.vignette = Mathf.Lerp(a.vignette, b.vignette, t);
            s.warmth = Mathf.Lerp(a.warmth, b.warmth, t);
            return s;
        }

        // ================= 应用 =================
        void ApplyState(TodState s)
        {
            // ---- 天空盒 ----
            Material sky = Application.isPlaying ? m_SkyInstance : skyMaterial;
            if (sky == null) sky = RenderSettings.skybox;
            if (sky != null)
            {
                if (RenderSettings.skybox != sky) RenderSettings.skybox = sky;
                sky.SetColor("_TopColor", s.skyTop);
                sky.SetColor("_MidColor", s.skyMid);
                sky.SetColor("_LowColor", s.skyLow);
                sky.SetColor("_HorizonColor", s.skyHorizon);
                sky.SetColor("_GroundColor", s.skyGround);
                sky.SetColor("_GlowColor", s.skyGlow);
                sky.SetColor("_SunColor", s.skySun);
                sky.SetColor("_MoonColor", s.skyMoon);
                sky.SetColor("_MoonGlowColor", s.skyMoonGlow);
                sky.SetColor("_StarColor", s.skyStar);
                sky.SetColor("_CloudLit", s.skyCloudLit);
                sky.SetColor("_CloudDark", s.skyCloudDark);
                sky.SetVector("_SunDir", s.sunDir);
                sky.SetVector("_MoonDir", s.moonDir);
                sky.SetFloat("_SunSize", s.sunSize);
                sky.SetFloat("_SunDisc", s.sunDisc);
                sky.SetFloat("_SunHalo", s.sunHalo);
                sky.SetFloat("_MoonSize", s.moonSize);
                sky.SetFloat("_MoonDisc", s.moonDisc);
                sky.SetFloat("_MoonGlow", s.moonGlow);
                sky.SetFloat("_StarIntensity", s.starIntensity);
                sky.SetFloat("_StarDensity", s.starDensity);
                sky.SetFloat("_StarSize", s.starSize);
                sky.SetFloat("_StarTwinkle", s.starTwinkle);
                sky.SetFloat("_MilkyWay", s.milkyWay);
                sky.SetFloat("_CloudCoverage", s.cloudCoverage);
                sky.SetFloat("_CloudContrast", s.cloudContrast);
                sky.SetFloat("_CloudScale", s.cloudScale);
                sky.SetFloat("_SkyIntensity", s.skyIntensity);
            }

            // ---- 太阳 / 月亮 ----
            ApplyDirectional(sunLight, s.sunLightColor, s.sunLightIntensity, s.sunDir, s.sunShadowStrength);
            ApplyDirectional(moonLight, s.moonLightColor, s.moonLightIntensity, s.moonDir, s.moonShadowStrength);
            RenderSettings.sun = sunLight;

            // ---- 环境光 / 雾 ----
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = s.ambientSky;
            RenderSettings.ambientEquatorColor = s.ambientEquator;
            RenderSettings.ambientGroundColor = s.ambientGround;
            RenderSettings.ambientIntensity = s.ambientIntensity;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = s.fogColor;
            RenderSettings.fogDensity = s.fogDensity;

            // ---- 店内灯 ----
            ApplyPointLike(windowSpillLight, s.windowSpillColor, s.windowSpillIntensity);
            if (fluoroLights != null)
                for (int i = 0; i < fluoroLights.Length; i++)
                    ApplyPointLike(fluoroLights[i], s.fluoroColor, s.fluoroIntensity);

            // ---- 路灯 ----
            if (streetLampSpots != null)
                for (int i = 0; i < streetLampSpots.Length; i++)
                    ApplyPointLike(streetLampSpots[i], s.streetLampColor, s.streetLampIntensity);
            if (streetLampGlows != null)
                for (int i = 0; i < streetLampGlows.Length; i++)
                    ApplyPointLike(streetLampGlows[i], s.streetLampColor, s.streetLampGlowIntensity);

            // ---- 材质自发光 ----
            ApplyMaterials(s);

            // ---- 后期 ----
            if (postFX != null)
            {
                postFX.bloomThreshold = s.bloomThreshold;
                postFX.bloomIntensity = s.bloomIntensity;
                postFX.bloomRadius = s.bloomRadius;
                postFX.bloomTint = s.bloomTint;
                postFX.exposure = s.exposure;
                postFX.contrast = s.contrast;
                postFX.saturation = s.saturation;
                postFX.vignette = s.vignette;
                postFX.warmth = s.warmth;
            }
        }

        static void ApplyDirectional(Light light, Color color, float intensity, Vector3 dir, float shadowStrength)
        {
            if (light == null) return;
            light.color = color;
            light.intensity = intensity;
            light.shadowStrength = Mathf.Clamp01(shadowStrength);
            light.enabled = intensity > 0.002f;
            if (dir.sqrMagnitude > 0.0001f)
                light.transform.rotation = Quaternion.LookRotation(-dir.normalized, Vector3.up);
        }

        static void ApplyPointLike(Light light, Color color, float intensity)
        {
            if (light == null) return;
            light.color = color;
            light.intensity = intensity;
            light.enabled = intensity > 0.002f;
        }

        // ================= 材质（MPB，不污染资源） =================
        void ApplyMaterials(TodState s)
        {
            for (int i = 0; i < m_Facades.Count; i++)
            {
                FacadeTarget f = m_Facades[i];
                if (f.material == null) continue;
                f.block.Clear();
                f.block.SetFloat("_Glow", f.baseGlow * s.facadeGlowScale);
                for (int r = 0; r < f.renderers.Count; r++)
                    if (f.renderers[r] != null) f.renderers[r].SetPropertyBlock(f.block);
            }

            for (int i = 0; i < m_Emissives.Count; i++)
            {
                EmissionTarget e = m_Emissives[i];
                if (e.material == null) continue;
                e.block.Clear();
                e.block.SetColor("_EmissionColor", e.baseEmission * s.emissiveScale);
                for (int r = 0; r < e.renderers.Count; r++)
                    if (e.renderers[r] != null) e.renderers[r].SetPropertyBlock(e.block);
            }

            if (m_LampGlass != null && m_LampGlass.material != null)
            {
                m_LampGlass.block.Clear();
                m_LampGlass.block.SetColor("_EmissionColor", m_LampGlass.baseEmission * s.lampEmissionScale);
                for (int r = 0; r < m_LampGlass.renderers.Count; r++)
                    if (m_LampGlass.renderers[r] != null) m_LampGlass.renderers[r].SetPropertyBlock(m_LampGlass.block);
            }

            for (int i = 0; i < m_Reflections.Count; i++)
            {
                FloatTarget f = m_Reflections[i];
                if (f.material == null) continue;
                f.block.Clear();
                f.block.SetFloat(f.property, f.baseValue * s.reflectionScale);
                for (int r = 0; r < f.renderers.Count; r++)
                    if (f.renderers[r] != null) f.renderers[r].SetPropertyBlock(f.block);
            }
        }

        void CacheTargets()
        {
            m_Facades.Clear();
            m_Emissives.Clear();
            m_Reflections.Clear();
            m_LampGlass = null;

            var watched = new HashSet<string>();
            if (emissiveMaterialNames != null)
                for (int i = 0; i < emissiveMaterialNames.Length; i++)
                    if (!string.IsNullOrEmpty(emissiveMaterialNames[i])) watched.Add(emissiveMaterialNames[i]);
            if (watched.Count == 0)
                for (int i = 0; i < DefaultEmissiveMaterials.Length; i++) watched.Add(DefaultEmissiveMaterials[i]);

            var reflectionNames = new HashSet<string> { "M_WetRoad", "M_WetSidewalk", "M_StoreFloor" };
            var facadeMap = new Dictionary<Material, FacadeTarget>();
            var emissiveMap = new Dictionary<Material, EmissionTarget>();
            var reflectionMap = new Dictionary<Material, FloatTarget>();
            var lampRenderers = new List<Renderer>();
            Material lampMaterial = null;

            Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null) continue;
                Material[] mats = r.sharedMaterials;
                if (mats == null) continue;

                for (int m = 0; m < mats.Length; m++)
                {
                    Material mat = mats[m];
                    if (mat == null) continue;

                    if (mat.shader != null && mat.shader.name == "Market/Facade")
                    {
                        if (!facadeMap.TryGetValue(mat, out FacadeTarget ft))
                        {
                            ft = new FacadeTarget
                            {
                                material = mat,
                                baseGlow = mat.HasProperty("_Glow") ? mat.GetFloat("_Glow") : 1f,
                                renderers = new List<Renderer>(),
                                block = new MaterialPropertyBlock()
                            };
                            facadeMap.Add(mat, ft);
                            m_Facades.Add(ft);
                        }
                        if (!ft.renderers.Contains(r)) ft.renderers.Add(r);
                    }
                    else if (mat.name == LampGlassMaterialName)
                    {
                        if (lampMaterial == null) lampMaterial = mat;
                        if (lampMaterial == mat && !lampRenderers.Contains(r)) lampRenderers.Add(r);
                    }
                    else if (watched.Contains(mat.name))
                    {
                        if (!emissiveMap.TryGetValue(mat, out EmissionTarget et))
                        {
                            et = new EmissionTarget
                            {
                                material = mat,
                                baseEmission = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black,
                                renderers = new List<Renderer>(),
                                block = new MaterialPropertyBlock()
                            };
                            emissiveMap.Add(mat, et);
                            m_Emissives.Add(et);
                        }
                        if (!et.renderers.Contains(r)) et.renderers.Add(r);
                    }
                    else if (reflectionNames.Contains(mat.name))
                    {
                        if (!reflectionMap.TryGetValue(mat, out FloatTarget ft))
                        {
                            ft = new FloatTarget
                            {
                                material = mat,
                                property = "_ReflectionStrength",
                                baseValue = mat.HasProperty("_ReflectionStrength") ? mat.GetFloat("_ReflectionStrength") : 1f,
                                renderers = new List<Renderer>(),
                                block = new MaterialPropertyBlock()
                            };
                            reflectionMap.Add(mat, ft);
                            m_Reflections.Add(ft);
                        }
                        if (!ft.renderers.Contains(r)) ft.renderers.Add(r);
                    }
                }
            }

            if (lampMaterial != null)
            {
                m_LampGlass = new EmissionTarget
                {
                    material = lampMaterial,
                    baseEmission = lampMaterial.HasProperty("_EmissionColor") ? lampMaterial.GetColor("_EmissionColor") : Color.white,
                    renderers = lampRenderers,
                    block = new MaterialPropertyBlock()
                };
            }
        }

        class FacadeTarget
        {
            public Material material;
            public float baseGlow;
            public List<Renderer> renderers;
            public MaterialPropertyBlock block;
        }

        class EmissionTarget
        {
            public Material material;
            public Color baseEmission;
            public List<Renderer> renderers;
            public MaterialPropertyBlock block;
        }

        class FloatTarget
        {
            public Material material;
            public string property;
            public float baseValue;
            public List<Renderer> renderers;
            public MaterialPropertyBlock block;
        }
    }
}
