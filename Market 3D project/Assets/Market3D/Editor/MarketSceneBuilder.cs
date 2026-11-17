using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace TenPercent2D.Market3D
{
    /// <summary>
    /// Market 场景生成器（预研用）。
    /// 参考图：黄昏暴雨刚停，从 Lawson 式便利店店内透过玻璃看向湿漉漉的街道。
    /// 全部用程序化几何 + 少量程序化贴图拼出来，不依赖任何外部美术资源。
    ///
    /// 菜单：Market3D > 生成 Market 场景
    /// </summary>
    public static class MarketSceneBuilder
    {
        const string RootFolder = "Assets/Market3D";
        const string MaterialFolder = RootFolder + "/Materials";
        const string GeneratedFolder = RootFolder + "/Generated";
        const string ScenePath = "Assets/Scenes/Market.unity";
        const string PreviewPath = "Docs/Previews/market3d_preview.png";

        // ================= 构图参数（改这里就能调整取景） =================
        readonly static Vector3 SunDir = new Vector3(-0.055f, 0.078f, 1f).normalized;
        readonly static Vector3 MoonDir = new Vector3(-0.12f, 0.25f, 1f).normalized;
        readonly static Vector3 CameraPos = new Vector3(0.75f, 1.55f, -3.00f);
        const float CameraPitch = -1.5f;
        const float CameraFov = 55f;
        const float CameraYaw = 0f;

        const float StoreMinX = -6.2f;
        const float StoreMaxX = 6.2f;
        const float StoreMinZ = -9.1f;
        const float StoreMaxZ = 0.0f;
        const float CeilingY = 2.95f;

        // 收银台
        const float CounterX0 = 1.20f;
        const float CounterX1 = 2.40f;
        const float CounterZ0 = -1.60f;
        const float CounterZ1 = 0.00f;
        const float CounterTopY = 0.96f;

        // 右侧货架
        const float ShelfFrontX = 2.55f;
        const float ShelfBackX = 3.95f;
        const float ShelfMinZ = -6.60f;
        const float ShelfMaxZ = 0.10f;

        static readonly Dictionary<string, Material> s_Materials = new Dictionary<string, Material>();
        static Transform s_Root;
        static System.Random s_Rng;

        // 时间系统要接管的灯（BuildLighting / AddStreetLight 里收集）
        static Light s_SunLight;
        static Light s_WindowSpillLight;
        static readonly List<Light> s_FluoroLights = new List<Light>();
        static readonly List<Light> s_StreetLampSpots = new List<Light>();
        static readonly List<Light> s_StreetLampGlows = new List<Light>();

        // ================= 入口 =================
        [MenuItem("Market3D/生成 Market 场景", false, 20)]
        public static void BuildFromMenu()
        {
            Build();
        }

        [MenuItem("Market3D/导出当前视角预览图", false, 21)]
        public static void CaptureFromMenu()
        {
            CapturePreview(PreviewPath);
            AssetDatabase.Refresh();
            Debug.Log("[Market3D] 预览图已导出：" + PreviewPath);
        }

        public static void Build()
        {
            EnsureFolders();
            s_Materials.Clear();
            s_FluoroLights.Clear();
            s_StreetLampSpots.Clear();
            s_StreetLampGlows.Clear();
            s_Rng = new System.Random(20261117);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            s_Root = new GameObject("Market").transform;

            BuildRenderSettings();
            BuildSky();
            var cam = BuildCamera();
            BuildSunLight();
            BuildStreet();
            BuildStoreShell();
            BuildStorefront();
            BuildInterior();
            BuildLighting();
            BuildTimeOfDay(cam);
            BuildReflections(cam);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("[Market3D] Market 场景已生成：" + ScenePath);
        }

        static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder(RootFolder)) AssetDatabase.CreateFolder("Assets", "Market3D");
            if (!AssetDatabase.IsValidFolder(MaterialFolder)) AssetDatabase.CreateFolder(RootFolder, "Materials");
            if (!AssetDatabase.IsValidFolder(GeneratedFolder)) AssetDatabase.CreateFolder(RootFolder, "Generated");
        }

        // ================= 通用工具 =================
        static Transform Group(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent == null ? s_Root : parent, false);
            return go.transform;
        }

        static Material Mat(string name, string shaderName, System.Action<Material> setup)
        {
            if (s_Materials.TryGetValue(name, out Material cached) && cached != null) return cached;

            string path = MaterialFolder + "/" + name + ".mat";
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                Debug.LogError("[Market3D] 找不到 Shader: " + shaderName + "（材质 " + name + " 退回 Standard）");
                shader = Shader.Find("Standard");
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else if (mat.shader != shader)
            {
                mat.shader = shader;
            }

            setup?.Invoke(mat);
            EditorUtility.SetDirty(mat);
            s_Materials[name] = mat;
            return mat;
        }

        static Material Std(string name, Color color, float smoothness = 0.2f, float metallic = 0f,
            Color? emission = null, float emissionIntensity = 1f)
        {
            return Mat(name, "Standard", m =>
            {
                m.SetColor("_Color", color);
                m.SetFloat("_Glossiness", smoothness);
                m.SetFloat("_Metallic", metallic);
                if (emission.HasValue)
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", emission.Value * emissionIntensity);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                }
                else
                {
                    m.DisableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", Color.black);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                }
            });
        }

        static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material mat,
            Vector3? euler = null, bool castShadows = true)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Collider col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = center;
            t.localScale = size;
            if (euler.HasValue) t.localEulerAngles = euler.Value;

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

            return go;
        }

        static GameObject Cyl(Transform parent, string name, Vector3 basePoint, float radius, float height,
            Material mat, bool castShadows = true)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            Collider col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = basePoint + Vector3.up * (height * 0.5f);
            t.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

            return go;
        }

        static GameObject Ball(Transform parent, string name, Vector3 center, Vector3 size, Material mat)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Collider col = go.GetComponent<Collider>();
            if (col != null) Object.DestroyImmediate(col);

            Transform t = go.transform;
            t.SetParent(parent, false);
            t.localPosition = center;
            t.localScale = size;

            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.On;
            return go;
        }

        static float Rand(float min, float max)
        {
            return min + (float)s_Rng.NextDouble() * (max - min);
        }

        // ================= 渲染设置 =================
        static void BuildRenderSettings()
        {
            RenderSettings.skybox = Mat("M_SunsetSky", "Market/SunsetSky", m =>
            {
                m.SetColor("_TopColor", new Color(0.090f, 0.050f, 0.210f));
                m.SetColor("_MidColor", new Color(0.460f, 0.155f, 0.415f));
                m.SetColor("_LowColor", new Color(0.980f, 0.400f, 0.380f));
                m.SetColor("_HorizonColor", new Color(1.00f, 0.50f, 0.19f));
                m.SetColor("_GroundColor", new Color(0.20f, 0.10f, 0.13f));
                m.SetColor("_GlowColor", new Color(1.00f, 0.46f, 0.15f));
                m.SetColor("_SunColor", new Color(1.00f, 0.82f, 0.52f));
                m.SetVector("_SunDir", new Vector4(SunDir.x, SunDir.y, SunDir.z, 0f));
                m.SetFloat("_SunSize", 0.99974f);
                m.SetColor("_CloudLit", new Color(1.00f, 0.52f, 0.44f));
                m.SetColor("_CloudDark", new Color(0.150f, 0.070f, 0.225f));
                m.SetFloat("_CloudCoverage", 0.80f);
                m.SetFloat("_CloudContrast", 1.30f);
                m.SetFloat("_CloudScale", 1.05f);
                m.SetFloat("_SkyIntensity", 1.0f);
            });

            // 时间系统统一用带星空/月亮的天空盒；材质默认值保持“黄昏”原样，
            // 这样即使没进 Play 模式，打开场景看到的还是原来那张图。
            Material todSky = Mat("M_TimeOfDaySky", "Market/TimeOfDaySky", m =>
            {
                m.SetColor("_TopColor", new Color(0.090f, 0.050f, 0.210f));
                m.SetColor("_MidColor", new Color(0.460f, 0.155f, 0.415f));
                m.SetColor("_LowColor", new Color(0.980f, 0.400f, 0.380f));
                m.SetColor("_HorizonColor", new Color(1.00f, 0.50f, 0.19f));
                m.SetColor("_GroundColor", new Color(0.20f, 0.10f, 0.13f));
                m.SetColor("_GlowColor", new Color(1.00f, 0.46f, 0.15f));
                m.SetColor("_SunColor", new Color(1.00f, 0.82f, 0.52f));
                m.SetVector("_SunDir", new Vector4(SunDir.x, SunDir.y, SunDir.z, 0f));
                m.SetFloat("_SunSize", 0.99974f);
                m.SetFloat("_SunDisc", 1f);
                m.SetFloat("_SunHalo", 1f);

                m.SetColor("_MoonColor", new Color(0.90f, 0.94f, 1.00f));
                m.SetColor("_MoonGlowColor", new Color(0.45f, 0.62f, 1.00f));
                m.SetVector("_MoonDir", new Vector4(MoonDir.x, MoonDir.y, MoonDir.z, 0f));
                m.SetFloat("_MoonSize", 0.99962f);
                m.SetFloat("_MoonDisc", 0f);
                m.SetFloat("_MoonGlow", 0f);

                m.SetColor("_StarColor", new Color(0.88f, 0.93f, 1.00f));
                m.SetFloat("_StarIntensity", 0f);
                m.SetFloat("_StarDensity", 0f);
                m.SetFloat("_StarSize", 0.14f);
                m.SetFloat("_StarTwinkle", 0.3f);
                m.SetFloat("_MilkyWay", 0f);

                m.SetColor("_CloudLit", new Color(1.00f, 0.52f, 0.44f));
                m.SetColor("_CloudDark", new Color(0.150f, 0.070f, 0.225f));
                m.SetFloat("_CloudCoverage", 0.80f);
                m.SetFloat("_CloudContrast", 1.30f);
                m.SetFloat("_CloudScale", 1.05f);
                m.SetFloat("_CloudSpeed", 1f);
                m.SetFloat("_SkyIntensity", 1.0f);
            });
            RenderSettings.skybox = todSky;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.130f, 0.100f, 0.170f);
            RenderSettings.ambientEquatorColor = new Color(0.320f, 0.190f, 0.170f);
            RenderSettings.ambientGroundColor = new Color(0.055f, 0.044f, 0.050f);

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogDensity = 0.0062f;
            RenderSettings.fogColor = new Color(0.46f, 0.28f, 0.32f);
        }

        static void BuildSky()
        {
            // 天空靠 skybox 材质，这里只额外放一块很远的“地面雾带”，让远处街道不至于悬空
            Std("M_DistantHaze", new Color(0.28f, 0.18f, 0.22f), 0f);
        }

        // ================= 灯光 =================
        static void BuildSunLight()
        {
            var go = new GameObject("SunLight");
            go.transform.SetParent(s_Root, false);
            go.transform.rotation = Quaternion.LookRotation(-SunDir);

            Light light = go.AddComponent<Light>();
            light.type = LightType.Directional;
            light.color = new Color(1.00f, 0.62f, 0.36f);
            light.intensity = 1.75f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.85f;
            light.shadowBias = 0.03f;
            light.shadowNormalBias = 0.35f;
            light.shadowNearPlane = 0.2f;
            s_SunLight = light;
        }

        static void BuildLighting()
        {
            // 窗外阳光“照进店内”的反弹光（模拟间接光，Built-in 没有实时 GI）
            var spillGo = new GameObject("WindowSpillLight");
            spillGo.transform.SetParent(s_Root, false);
            spillGo.transform.position = new Vector3(0.9f, 2.55f, 1.4f);
            spillGo.transform.rotation = Quaternion.LookRotation(new Vector3(0.1f, -1.35f, -5.6f));

            Light spill = spillGo.AddComponent<Light>();
            spill.type = LightType.Spot;
            spill.color = new Color(1.00f, 0.54f, 0.26f);
            spill.intensity = 2.8f;
            spill.range = 16f;
            spill.spotAngle = 100f;
            spill.innerSpotAngle = 22f;
            spill.shadows = LightShadows.Soft;
            spill.shadowStrength = 0.6f;
            s_WindowSpillLight = spill;

            // 店内荧光灯
            Vector3[] lampPositions =
            {
                new Vector3(-0.9f, 2.55f, -1.2f),
                new Vector3(-3.4f, 2.55f, -1.9f),
                new Vector3(-4.3f, 2.30f, -4.2f),
                new Vector3(2.4f, 2.55f, -2.6f),
                new Vector3(-1.2f, 2.55f, -4.6f)
            };
            for (int i = 0; i < lampPositions.Length; i++)
            {
                var go = new GameObject("FluoroLight_" + i);
                go.transform.SetParent(s_Root, false);
                go.transform.position = lampPositions[i];
                Light l = go.AddComponent<Light>();
                l.type = LightType.Point;
                l.color = new Color(1.00f, 0.94f, 0.86f);
                l.intensity = 1.85f;
                l.range = 9f;
                l.shadows = LightShadows.None;
                s_FluoroLights.Add(l);
            }
        }

        // ================= 时间系统 =================
        static void BuildTimeOfDay(Camera cam)
        {
            // 月光：和太阳分开一盏，入夜时交叉淡化，避免主光方向突然甩到另一边。
            var moonGo = new GameObject("MoonLight");
            moonGo.transform.SetParent(s_Root, false);
            moonGo.transform.rotation = Quaternion.LookRotation(-MoonDir);
            Light moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(0.45f, 0.58f, 1.00f);
            moon.intensity = 0f;
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.55f;
            moon.shadowBias = 0.04f;
            moon.shadowNormalBias = 0.4f;
            moon.shadowNearPlane = 0.2f;

            // 时间系统总控
            var todGo = new GameObject("TimeOfDay");
            todGo.transform.SetParent(s_Root, false);
            TimeOfDaySystem system = todGo.AddComponent<TimeOfDaySystem>();
            todGo.AddComponent<TimeOfDayHUD>();

            system.skyMaterial = AssetDatabase.LoadAssetAtPath<Material>(MaterialFolder + "/M_TimeOfDaySky.mat");
            system.presets = TimeOfDayPreset.CreateDefaults();
            system.keyframes = TimeOfDaySystem.CreateDefaultKeyframes();
            system.startHour = 18.34f;
            system.autoAdvance = false;
            system.dayLengthSeconds = 120f;
            system.manualBlendSeconds = 3.5f;
            system.showHud = true;

            system.sunLight = s_SunLight;
            system.moonLight = moon;
            system.windowSpillLight = s_WindowSpillLight;
            system.fluoroLights = s_FluoroLights.ToArray();
            system.streetLampSpots = s_StreetLampSpots.ToArray();
            system.streetLampGlows = s_StreetLampGlows.ToArray();
            system.facadeMaterials = new[]
            {
                s_Materials["M_FacadeA"],
                s_Materials["M_FacadeB"],
                s_Materials["M_FacadeC"]
            };
            system.emissiveMaterialNames = (string[])TimeOfDaySystem.DefaultEmissiveMaterials.Clone();
            system.postFX = cam.GetComponent<MarketPostFX>();

            // 存盘时保持“黄昏”作为打开场景的第一眼
            system.ApplyPresetImmediate(1);
            EditorUtility.SetDirty(system);

            RenderSettings.sun = s_SunLight;
        }

        // ================= 相机与后期 =================
        static Camera BuildCamera()
        {
            var go = new GameObject("MainCamera");
            go.transform.SetParent(s_Root, false);
            go.transform.position = CameraPos;
            go.transform.rotation = Quaternion.Euler(CameraPitch, CameraYaw, 0f);
            go.tag = "MainCamera";

            Camera cam = go.AddComponent<Camera>();
            cam.fieldOfView = CameraFov;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 500f;
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            cam.renderingPath = RenderingPath.Forward;

            go.AddComponent<AudioListener>();
            go.AddComponent<MarketCameraDrift>();

            MarketPostFX fx = go.AddComponent<MarketPostFX>();
            fx.shader = Shader.Find("Market/PostFX");
            fx.bloomThreshold = 1.95f;
            fx.bloomIntensity = 0.52f;
            fx.bloomRadius = 1.5f;
            fx.bloomTint = new Color(1.0f, 0.80f, 0.70f);
            fx.exposure = 0.96f;
            fx.contrast = 1.12f;
            fx.saturation = 1.20f;
            fx.vignette = 0.78f;
            fx.warmth = 0.06f;

            return cam;
        }

        static void BuildReflections(Camera cam)
        {
            Material roadMat = s_Materials["M_WetRoad"];
            Material sideMat = s_Materials["M_WetSidewalk"];
            Material tileMat = s_Materials["M_StoreFloor"];
            Material glassMat = s_Materials["M_Glass"];

            var groundGo = new GameObject("Reflection_Horizontal");
            groundGo.transform.SetParent(s_Root, false);
            groundGo.transform.position = new Vector3(0f, 0f, 0f);
            PlanarReflection ground = groundGo.AddComponent<PlanarReflection>();
            ground.planeNormalLocal = Vector3.up;
            ground.textureWidth = 1280;
            ground.textureHeight = 720;
            ground.targetMaterials = new[] { roadMat, sideMat, tileMat };

            var glassGo = new GameObject("Reflection_Vertical");
            glassGo.transform.SetParent(s_Root, false);
            glassGo.transform.position = new Vector3(0f, 0f, 0f);
            PlanarReflection glass = glassGo.AddComponent<PlanarReflection>();
            glass.planeNormalLocal = new Vector3(0f, 0f, -1f);
            glass.textureWidth = 1024;
            glass.textureHeight = 576;
            glass.targetMaterials = new[] { glassMat };

            ground.RenderReflection(cam);
            glass.RenderReflection(cam);
        }

        // ================= 街道 =================
        static void BuildStreet()
        {
            Transform root = Group("Street");

            Material wetRoad = Mat("M_WetRoad", "Market/WetAsphalt", m =>
            {
                m.SetColor("_Color", new Color(0.036f, 0.033f, 0.040f));
                m.SetColor("_WetColor", new Color(0.98f, 0.92f, 0.94f));
                m.SetFloat("_ReflectionStrength", 0.92f);
                m.SetFloat("_Wetness", 0.82f);
                m.SetFloat("_NoiseScale", 2.0f);
                m.SetFloat("_PuddleScale", 0.75f);
                m.SetFloat("_RippleStrength", 0.16f);
                m.SetFloat("_Smoothness", 0.72f);
                m.SetFloat("_DiffuseGain", 0.8f);
            });

            Material wetSidewalk = Mat("M_WetSidewalk", "Market/WetAsphalt", m =>
            {
                m.SetColor("_Color", new Color(0.085f, 0.078f, 0.080f));
                m.SetColor("_WetColor", new Color(0.95f, 0.90f, 0.90f));
                m.SetFloat("_ReflectionStrength", 0.70f);
                m.SetFloat("_Wetness", 0.68f);
                m.SetFloat("_NoiseScale", 6.0f);
                m.SetFloat("_PuddleScale", 1.6f);
                m.SetFloat("_RippleStrength", 0.35f);
                m.SetFloat("_Smoothness", 0.70f);
                m.SetFloat("_DiffuseGain", 1.0f);
            });

            Material concrete = Std("M_Concrete", new Color(0.30f, 0.29f, 0.30f), 0.12f);
            Material curbMat = Std("M_Curb", new Color(0.42f, 0.42f, 0.43f), 0.18f);
            Material paint = Std("M_RoadPaint", new Color(0.55f, 0.55f, 0.52f), 0.30f);
            Material tactile = Std("M_TactileYellow", new Color(0.85f, 0.62f, 0.08f), 0.3f);

            // 大地面兜底
            Box(root, "GroundBase", new Vector3(0f, -0.35f, 30f), new Vector3(220f, 0.6f, 220f), concrete, null, false);

            // 店前人行道
            Box(root, "SidewalkFront", new Vector3(0f, 0.05f, 1.45f), new Vector3(34f, 0.10f, 2.9f), wetSidewalk, null, false);
            // 盲道
            Box(root, "TactileStrip", new Vector3(-0.8f, 0.105f, 1.18f), new Vector3(9.6f, 0.012f, 0.55f), tactile, null, false);
            // 路缘石
            Box(root, "CurbFront", new Vector3(0f, 0.055f, 2.98f), new Vector3(34f, 0.11f, 0.24f), curbMat, null, false);

            // 车行道
            Box(root, "Road", new Vector3(0.4f, -0.012f, 55f), new Vector3(8.8f, 0.03f, 106f), wetRoad, null, false);

            // 左右人行道
            Box(root, "SidewalkLeft", new Vector3(-6.1f, 0.05f, 55f), new Vector3(3.8f, 0.10f, 106f), wetSidewalk, null, false);
            Box(root, "SidewalkRight", new Vector3(6.3f, 0.05f, 55f), new Vector3(2.6f, 0.10f, 106f), wetSidewalk, null, false);
            Box(root, "CurbLeft", new Vector3(-4.12f, 0.055f, 55f), new Vector3(0.22f, 0.11f, 106f), curbMat, null, false);
            Box(root, "CurbRight", new Vector3(4.92f, 0.055f, 55f), new Vector3(0.22f, 0.11f, 106f), curbMat, null, false);

            // 中心虚线
            for (int i = 0; i < 14; i++)
            {
                Box(root, "LaneDash_" + i, new Vector3(0.55f, 0.008f, 6f + i * 2.9f), new Vector3(0.14f, 0.008f, 1.5f), paint, null, false);
            }

            // 停止线 + 斑马线
            Box(root, "StopLine", new Vector3(0.4f, 0.008f, 4.1f), new Vector3(8.4f, 0.008f, 0.26f), paint, null, false);
            for (int i = 0; i < 7; i++)
            {
                Box(root, "Crosswalk_" + i, new Vector3(0.4f, 0.008f, 5.2f + i * 0.76f), new Vector3(7.9f, 0.008f, 0.38f), paint, null, false);
            }

            BuildBuildings(root);
            BuildStreetFurniture(root);
            BuildPowerLines(root);
        }

        static void BuildBuildings(Transform root)
        {
            Transform group = Group("Buildings", root);

            Material facadeA = Mat("M_FacadeA", "Market/Facade", m =>
            {
                m.SetColor("_WallColor", new Color(0.235f, 0.225f, 0.225f));
                m.SetColor("_WinColor", new Color(0.055f, 0.065f, 0.10f));
                m.SetColor("_LitColor", new Color(1.0f, 0.72f, 0.42f));
                m.SetFloat("_LitRatio", 0.22f);
                m.SetFloat("_WinPerMeterX", 0.62f);
                m.SetFloat("_WinPerMeterY", 0.52f);
                m.SetFloat("_Glow", 1.5f);
                m.SetFloat("_Seed", 2.3f);
            });

            Material facadeB = Mat("M_FacadeB", "Market/Facade", m =>
            {
                m.SetColor("_WallColor", new Color(0.325f, 0.295f, 0.275f));
                m.SetColor("_WinColor", new Color(0.050f, 0.060f, 0.095f));
                m.SetColor("_LitColor", new Color(1.0f, 0.80f, 0.55f));
                m.SetFloat("_LitRatio", 0.16f);
                m.SetFloat("_WinPerMeterX", 0.48f);
                m.SetFloat("_WinPerMeterY", 0.45f);
                m.SetFloat("_Glow", 1.2f);
                m.SetFloat("_Seed", 9.1f);
            });

            Material facadeC = Mat("M_FacadeC", "Market/Facade", m =>
            {
                m.SetColor("_WallColor", new Color(0.18f, 0.175f, 0.19f));
                m.SetColor("_WinColor", new Color(0.045f, 0.055f, 0.085f));
                m.SetColor("_LitColor", new Color(0.95f, 0.70f, 0.45f));
                m.SetFloat("_LitRatio", 0.10f);
                m.SetFloat("_WinPerMeterX", 0.55f);
                m.SetFloat("_WinPerMeterY", 0.55f);
                m.SetFloat("_Glow", 1.0f);
                m.SetFloat("_Seed", 5.7f);
            });

            Material roof = Std("M_RoofTile", new Color(0.14f, 0.135f, 0.165f), 0.30f);
            Material silhouette = Std("M_Silhouette", new Color(0.085f, 0.070f, 0.100f), 0.05f);

            // ---- 左侧沿街建筑（前面紧贴人行道 x=-8）----
            AddHouse(group, "HouseLeftA", new Vector3(-11.5f, 0f, 18.0f), 7.0f, 5.2f, 8.0f, facadeB, roof);
            AddHouse(group, "HouseLeftB", new Vector3(-11.8f, 0f, 27.5f), 6.4f, 4.4f, 8.0f, facadeB, roof);
            Box(group, "ShopLeftA", new Vector3(-11.0f, 2.0f, 37.0f), new Vector3(6.0f, 4.0f, 8.0f), facadeA);
            Box(group, "AptLeftMid", new Vector3(-12.0f, 6.5f, 48.0f), new Vector3(8.0f, 13.0f, 10.0f), facadeC);
            AddHouse(group, "HouseLeftC", new Vector3(-11.5f, 0f, 60.0f), 7.0f, 5.0f, 9.0f, facadeB, roof);

            // ---- 右侧沿街建筑（前面 x=7.6）----
            AddHouse(group, "HouseRightA", new Vector3(10.8f, 0f, 16.0f), 6.4f, 5.0f, 7.0f, facadeA, roof);
            Box(group, "ShopRightA", new Vector3(11.0f, 1.9f, 25.0f), new Vector3(6.8f, 3.8f, 7.0f), facadeB);
            Box(group, "AptRightMid", new Vector3(12.0f, 7.0f, 36.0f), new Vector3(8.8f, 14.0f, 9.5f), facadeA);
            AddHouse(group, "HouseRightB", new Vector3(10.6f, 0f, 47.0f), 6.0f, 4.6f, 8.0f, facadeB, roof);
            Box(group, "AptRightFar", new Vector3(12.5f, 9.0f, 60.0f), new Vector3(10.0f, 18.0f, 10.0f), facadeC);

            // ---- 左侧更深处的一排（制造层层叠叠的屋顶）----
            Box(group, "BackBlockA", new Vector3(-19.0f, 3.2f, 22.0f), new Vector3(8.0f, 6.4f, 14.0f), facadeC);
            Box(group, "BackBlockB", new Vector3(-20.0f, 6.0f, 40.0f), new Vector3(9.0f, 12.0f, 16.0f), facadeC);

            // ---- 远处地平线：只在两侧堆剪影，正前方留出太阳 ----
            for (int i = 0; i < 10; i++)
            {
                float side = i < 5 ? -1f : 1f;
                float d = 16f + (i % 5) * 8.0f;
                float x = side * d;
                float h = 1.6f + (i % 4) * 1.3f;
                float z = 78f + (i % 3) * 9f;
                Box(group, "DistantBlock_" + i, new Vector3(x, h * 0.5f, z), new Vector3(9f, h, 9f), silhouette, null, false);
            }

            // 路尽头的护栏（低矮，不挡太阳）
            Box(group, "FarRail", new Vector3(0.4f, 0.62f, 96f), new Vector3(8.6f, 0.10f, 0.10f), silhouette, null, false);
        }

        static void AddHouse(Transform parent, string name, Vector3 basePos, float width, float height, float depth,
            Material wall, Material roof)
        {
            Transform g = Group(name, parent);
            g.position = basePos;

            Box(g, "Body", new Vector3(0f, height * 0.5f, 0f), new Vector3(width, height, depth), wall);

            // 坡屋顶：两块斜板
            float overhang = 0.45f;
            float slabLen = Mathf.Sqrt((width * 0.5f + overhang) * (width * 0.5f + overhang) + 0.9f * 0.9f);
            float slope = Mathf.Atan2(0.9f, width * 0.5f + overhang) * Mathf.Rad2Deg;

            Box(g, "RoofL", new Vector3(-width * 0.25f, height + 0.42f, 0f),
                new Vector3(slabLen, 0.14f, depth + overhang * 2f), roof, new Vector3(0f, 0f, slope));
            Box(g, "RoofR", new Vector3(width * 0.25f, height + 0.42f, 0f),
                new Vector3(slabLen, 0.14f, depth + overhang * 2f), roof, new Vector3(0f, 0f, -slope));

            // 一层店面/围墙压边
            Box(g, "BaseBand", new Vector3(0f, 0.55f, 0f), new Vector3(width + 0.06f, 1.1f, depth + 0.06f),
                Std("M_ConcreteBase", new Color(0.22f, 0.21f, 0.22f), 0.15f));
        }

        static void BuildStreetFurniture(Transform root)
        {
            Transform group = Group("Props", root);

            Material poleMat = Std("M_Pole", new Color(0.26f, 0.25f, 0.26f), 0.12f);
            Material metalDark = Std("M_MetalDark", new Color(0.16f, 0.16f, 0.17f), 0.4f, 0.6f);
            Material lampMat = Std("M_LampGlass", new Color(0.95f, 0.9f, 0.75f), 0.5f, 0f, new Color(1.0f, 0.82f, 0.55f), 3.2f);

            // 电线杆（左侧一排，正对窗口）
            float[] poleZ = { 5.4f, 12.2f, 19.5f, 27.5f, 36.5f, 46f };
            for (int i = 0; i < poleZ.Length; i++)
            {
                Vector3 p = new Vector3(-4.55f + i * 0.02f, 0f, poleZ[i]);
                Cyl(group, "PoleLeft_" + i, p, 0.13f, 9.4f, poleMat);
                Box(group, "PoleArmA_" + i, p + new Vector3(0f, 7.9f, 0f), new Vector3(0.10f, 0.10f, 2.0f), metalDark);
                Box(group, "PoleArmB_" + i, p + new Vector3(0f, 8.55f, 0f), new Vector3(0.10f, 0.10f, 1.6f), metalDark);
                if (i % 2 == 0)
                {
                    Cyl(group, "Transformer_" + i, p + new Vector3(0.35f, 6.7f, 0f), 0.28f, 0.85f, metalDark);
                }
            }

            // 右侧电线杆
            float[] poleRightZ = { 8.6f, 18.0f, 29f, 39f };
            for (int i = 0; i < poleRightZ.Length; i++)
            {
                Vector3 p = new Vector3(5.55f, 0f, poleRightZ[i]);
                Cyl(group, "PoleRight_" + i, p, 0.12f, 8.8f, poleMat);
                Box(group, "PoleRightArm_" + i, p + new Vector3(0f, 7.5f, 0f), new Vector3(0.09f, 0.09f, 1.6f), metalDark);
            }

            // 路灯
            AddStreetLight(group, new Vector3(-4.65f, 0f, 9.4f), poleMat, metalDark, lampMat);
            AddStreetLight(group, new Vector3(5.65f, 0f, 24f), poleMat, metalDark, lampMat);

            // 自动贩卖机（店门口右侧，亮着的）
            AddVendingMachine(group, "VendingRed", new Vector3(6.15f, 0f, 3.4f), new Color(0.62f, 0.09f, 0.11f), new Color(0.85f, 0.95f, 1.0f));
            AddVendingMachine(group, "VendingBlue", new Vector3(6.15f, 0f, 4.55f), new Color(0.10f, 0.22f, 0.55f), new Color(1.0f, 0.95f, 0.8f));

            // 垃圾桶 / 电线井盖 / A 字立牌
            Box(group, "BinA", new Vector3(-3.4f, 0.42f, 2.2f), new Vector3(0.55f, 0.84f, 0.55f), Std("M_BinGray", new Color(0.42f, 0.44f, 0.45f), 0.4f));
            Box(group, "BinB", new Vector3(-2.75f, 0.42f, 2.2f), new Vector3(0.55f, 0.84f, 0.55f), Std("M_BinBlue", new Color(0.12f, 0.28f, 0.55f), 0.4f));
            Box(group, "UtilityBox", new Vector3(-4.55f, 0.6f, 3.4f), new Vector3(0.4f, 1.2f, 0.3f), Std("M_UtilityGray", new Color(0.30f, 0.31f, 0.30f), 0.2f, 0.2f));

            // 行道树
            AddTree(group, new Vector3(6.55f, 0f, 2.4f), 1.05f);
            AddTree(group, new Vector3(6.6f, 0f, 14.5f), 1.2f);
            AddTree(group, new Vector3(-6.2f, 0f, 22.5f), 1.0f);
            AddTree(group, new Vector3(-6.3f, 0f, 34f), 1.1f);

            // 路边护栏
            Material guard = Std("M_GuardRail", new Color(0.50f, 0.46f, 0.42f), 0.3f, 0.3f);
            for (int i = 0; i < 7; i++)
            {
                Box(group, "Rail_" + i, new Vector3(5.35f, 0.55f, 6.0f + i * 3.2f), new Vector3(0.07f, 0.07f, 3.0f), guard);
            }

            // ---- 近处：左侧矮墙 + 绿篱，右侧金属围栏（挡住建筑根部，制造层次）----
            Material lowWall = Std("M_LowWall", new Color(0.34f, 0.32f, 0.30f), 0.15f);
            Material hedge = Std("M_Hedge", new Color(0.075f, 0.11f, 0.070f), 0.1f);
            Material fencePost = Std("M_FencePost", new Color(0.30f, 0.31f, 0.33f), 0.4f, 0.5f);

            Box(group, "LeftWall", new Vector3(-8.15f, 0.55f, 9.6f), new Vector3(0.3f, 1.1f, 13.5f), lowWall);
            Box(group, "LeftHedge", new Vector3(-8.15f, 1.28f, 9.6f), new Vector3(0.55f, 0.36f, 13.0f), hedge);
            for (int i = 0; i < 12; i++)
            {
                Box(group, "RightFencePost_" + i, new Vector3(7.62f, 0.65f, 3.4f + i * 0.95f), new Vector3(0.055f, 1.3f, 0.055f), fencePost);
            }
            Box(group, "RightFenceRail", new Vector3(7.62f, 1.12f, 8.6f), new Vector3(0.04f, 0.05f, 11.0f), fencePost);
            Box(group, "RightFenceRailLow", new Vector3(7.62f, 0.62f, 8.6f), new Vector3(0.04f, 0.05f, 11.0f), fencePost);
            for (int i = 0; i < 6; i++)
            {
                Ball(group, "Bush_" + i, new Vector3(7.6f, 0.55f, 4.2f + i * 1.7f), new Vector3(1.1f, 0.9f, 1.4f), hedge);
            }
        }

        static void AddStreetLight(Transform parent, Vector3 basePos, Material pole, Material metal, Material lamp)
        {
            Transform g = Group("StreetLight", parent);
            g.position = basePos;
            float dir = basePos.x < 0f ? 1f : -1f;

            Cyl(g, "Post", Vector3.zero, 0.09f, 6.2f, pole);
            Box(g, "Arm", new Vector3(dir * 0.85f, 6.15f, 0f), new Vector3(1.75f, 0.09f, 0.09f), metal);
            Box(g, "Head", new Vector3(dir * 1.62f, 6.05f, 0f), new Vector3(0.62f, 0.16f, 0.30f), lamp);

            // 真正的路灯照明：灯头下的一盏聚光打出地面暖光斑 + 灯头处一盏点光当灯泡辉光。
            Vector3 headPos = new Vector3(dir * 1.62f, 0f, 0f);

            var spotGo = new GameObject("LampSpot");
            spotGo.transform.SetParent(g, false);
            spotGo.transform.localPosition = headPos + new Vector3(0f, 5.90f, 0f);
            spotGo.transform.rotation = Quaternion.LookRotation(new Vector3(dir * 0.18f, -1f, 0f).normalized, Vector3.up);
            Light spot = spotGo.AddComponent<Light>();
            spot.type = LightType.Spot;
            spot.color = new Color(1.00f, 0.72f, 0.42f);
            spot.intensity = 0f;
            spot.range = 18f;
            spot.spotAngle = 78f;
            spot.innerSpotAngle = 42f;
            spot.shadows = LightShadows.Soft;
            spot.shadowStrength = 0.75f;
            spot.shadowBias = 0.05f;
            spot.shadowNormalBias = 0.35f;
            s_StreetLampSpots.Add(spot);

            var glowGo = new GameObject("LampGlow");
            glowGo.transform.SetParent(g, false);
            glowGo.transform.localPosition = headPos + new Vector3(0f, 6.16f, 0f);
            Light glow = glowGo.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = new Color(1.00f, 0.80f, 0.55f);
            glow.intensity = 0f;
            glow.range = 7f;
            glow.shadows = LightShadows.None;
            s_StreetLampGlows.Add(glow);
        }

        static void AddVendingMachine(Transform parent, string id, Vector3 basePos, Color bodyColor, Color glowColor)
        {
            Transform g = Group("VendingMachine_" + id, parent);
            g.position = basePos;

            Material body = Std("M_VendingBody_" + id, bodyColor, 0.45f, 0.1f);
            Material panel = Std("M_VendingPanel_" + id, glowColor, 0.6f, 0f, glowColor, 1.1f);

            Box(g, "Body", new Vector3(0f, 0.95f, 0f), new Vector3(1.05f, 1.9f, 0.78f), body);
            Box(g, "Display", new Vector3(0f, 1.30f, -0.40f), new Vector3(0.92f, 0.95f, 0.03f), panel);
            Box(g, "SignTop", new Vector3(0f, 1.95f, -0.40f), new Vector3(0.92f, 0.26f, 0.03f), panel);
            Box(g, "Tray", new Vector3(0f, 0.32f, -0.40f), new Vector3(0.86f, 0.22f, 0.03f),
                Std("M_VendingTray", new Color(0.10f, 0.10f, 0.11f), 0.4f));
        }

        static void AddTree(Transform parent, Vector3 basePos, float scale)
        {
            Transform g = Group("Tree", parent);
            g.position = basePos;
            g.localScale = Vector3.one * scale;

            Material trunk = Std("M_TreeTrunk", new Color(0.16f, 0.13f, 0.11f), 0.1f);
            Material leaf = Std("M_TreeLeaf", new Color(0.085f, 0.135f, 0.085f), 0.1f);

            Cyl(g, "Trunk", Vector3.zero, 0.13f, 2.2f, trunk);
            Ball(g, "CrownA", new Vector3(0f, 2.9f, 0f), new Vector3(2.3f, 2.0f, 2.2f), leaf);
            Ball(g, "CrownB", new Vector3(0.5f, 3.6f, -0.3f), new Vector3(1.7f, 1.6f, 1.7f), leaf);
            Ball(g, "CrownC", new Vector3(-0.55f, 3.3f, 0.25f), new Vector3(1.5f, 1.5f, 1.5f), leaf);
        }

        static void BuildPowerLines(Transform root)
        {
            Transform group = Group("PowerLines", root);
            Material wire = Std("M_Wire", new Color(0.045f, 0.040f, 0.045f), 0.25f);

            float[] zs = { 5.4f, 12.2f, 19.5f, 27.5f, 36.5f, 46f };
            float[] offsetsX = { -0.90f, -0.32f, 0.32f, 0.90f };

            for (int line = 0; line < offsetsX.Length; line++)
            {
                for (int i = 0; i < zs.Length - 1; i++)
                {
                    Vector3 a = new Vector3(-4.55f + offsetsX[line], 8.62f, zs[i]);
                    Vector3 b = new Vector3(-4.55f + offsetsX[line], 8.62f, zs[i + 1]);
                    Wire(group, a, b, 0.62f + line * 0.09f, wire, 0.038f);
                }
            }

            // 低一点的一束
            for (int line = 0; line < 2; line++)
            {
                for (int i = 0; i < zs.Length - 1; i++)
                {
                    Vector3 a = new Vector3(-4.55f + 0.42f * (line == 0 ? -1 : 1), 7.92f, zs[i]);
                    Vector3 b = new Vector3(-4.55f + 0.42f * (line == 0 ? -1 : 1), 7.92f, zs[i + 1]);
                    Wire(group, a, b, 0.95f, wire, 0.055f);
                }
            }

            // 横跨马路的线
            Wire(group, new Vector3(-4.55f, 7.92f, 5.4f), new Vector3(5.55f, 7.52f, 8.6f), 0.8f, wire, 0.048f);
            Wire(group, new Vector3(-4.55f, 8.62f, 12.2f), new Vector3(5.55f, 7.52f, 18.0f), 1.0f, wire, 0.042f);
            Wire(group, new Vector3(-4.55f, 7.92f, 12.2f), new Vector3(5.55f, 7.52f, 18.0f), 1.2f, wire, 0.042f);
            Wire(group, new Vector3(-4.55f, 8.62f, 19.5f), new Vector3(5.55f, 7.52f, 29f), 1.3f, wire, 0.040f);
        }

        static void Wire(Transform parent, Vector3 a, Vector3 b, float sag, Material mat, float width)
        {
            var go = new GameObject("Wire");
            go.transform.SetParent(parent, false);

            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 14;
            for (int i = 0; i < 14; i++)
            {
                float t = i / 13f;
                Vector3 p = Vector3.Lerp(a, b, t);
                p.y -= sag * 4f * t * (1f - t);
                lr.SetPosition(i, p);
            }
            lr.sharedMaterial = mat;
            lr.widthMultiplier = width;
            lr.numCapVertices = 0;
            lr.shadowCastingMode = ShadowCastingMode.Off;
        }

        // ================= 店铺外壳 =================
        static void BuildStoreShell()
        {
            Transform root = Group("Store");

            Material wall = Std("M_StoreWall", new Color(0.40f, 0.38f, 0.355f), 0.12f);
            Material ceiling = Std("M_Ceiling", new Color(0.20f, 0.20f, 0.21f), 0.1f);
            Material ceilingDark = Std("M_CeilingDark", new Color(0.115f, 0.115f, 0.125f), 0.2f);

            // 地/顶/墙
            Mat("M_StoreFloor", "Market/GlossyTile", m =>
            {
                m.SetTexture("_MainTex", Tex("tex_tile", 256, 256, TextureWrapMode.Repeat, DrawTile));
                m.SetTextureScale("_MainTex", new Vector2(18f, 13f));
                m.SetColor("_Color", new Color(0.78f, 0.79f, 0.78f));
                m.SetFloat("_ReflectionStrength", 0.62f);
                m.SetFloat("_Smoothness", 0.78f);
                m.SetFloat("_Metallic", 0.05f);
            });

            Box(root, "Floor", new Vector3(0f, -0.1f, -4.55f), new Vector3(StoreMaxX - StoreMinX, 0.2f, StoreMaxZ - StoreMinZ), s_Materials["M_StoreFloor"], null, false);
            Box(root, "Ceiling", new Vector3(0f, CeilingY, -4.55f), new Vector3(StoreMaxX - StoreMinX, 0.16f, StoreMaxZ - StoreMinZ), ceiling);

            Box(root, "WallRight", new Vector3(StoreMaxX + 0.1f, 1.55f, -4.55f), new Vector3(0.2f, 3.1f, StoreMaxZ - StoreMinZ), wall);
            Box(root, "WallLeft", new Vector3(StoreMinX - 0.1f, 1.55f, -4.55f), new Vector3(0.2f, 3.1f, StoreMaxZ - StoreMinZ), wall);
            Box(root, "WallBack", new Vector3(0f, 1.55f, StoreMinZ - 0.1f), new Vector3(StoreMaxX - StoreMinX, 3.1f, 0.2f), wall);

            // 天花上的金属横梁
            for (int i = 0; i < 5; i++)
            {
                Box(root, "CeilingBeam_" + i, new Vector3(0f, CeilingY - 0.12f, -8.4f + i * 2.2f),
                    new Vector3(StoreMaxX - StoreMinX, 0.10f, 0.16f), ceilingDark);
            }

            // 后墙的超市货架剪影（增加纵深，细节可以后面再补）
            Material shelf = Std("M_ShelfWhite", new Color(0.82f, 0.82f, 0.80f), 0.25f);
            Box(root, "BackShelf", new Vector3(-3.2f, 1.0f, -8.4f), new Vector3(4.2f, 2.0f, 0.5f), shelf);
            Box(root, "BackShelf2", new Vector3(-3.2f, 2.2f, -8.4f), new Vector3(4.2f, 0.12f, 0.5f), shelf);
            Box(root, "BackFridge", new Vector3(3.4f, 1.1f, -8.5f), new Vector3(4.2f, 2.2f, 0.6f),
                Std("M_FridgeBody", new Color(0.55f, 0.60f, 0.62f), 0.5f, 0.4f));

            // 外观：店面以上的立面 + 屋顶 + 雨棚，从外面看才像一栋店
            Material extWall = Std("M_StoreExterior", new Color(0.26f, 0.25f, 0.25f), 0.15f);
            Material canopy = Std("M_Canopy", new Color(0.13f, 0.13f, 0.15f), 0.3f, 0.2f);
            Box(root, "ExteriorFacade", new Vector3(0f, 3.62f, 0.16f), new Vector3(12.8f, 1.42f, 0.34f), extWall);
            Box(root, "StoreRoof", new Vector3(0f, 4.40f, -4.55f), new Vector3(12.9f, 0.30f, 9.6f), canopy);
            Box(root, "CanopyBeam", new Vector3(0f, 3.02f, 0.55f), new Vector3(12.6f, 0.22f, 0.90f), canopy);
            Box(root, "SideWallExtL", new Vector3(-6.35f, 2.1f, -4.55f), new Vector3(0.30f, 4.2f, 9.4f), extWall);
            Box(root, "SideWallExtR", new Vector3(6.35f, 2.1f, -4.55f), new Vector3(0.30f, 4.2f, 9.4f), extWall);
        }

        // ================= 玻璃幕墙 / 店门 =================
        static void BuildStorefront()
        {
            Transform root = Group("Storefront");

            Material frame = Std("M_Aluminum", new Color(0.20f, 0.21f, 0.23f), 0.30f, 0.25f);
            Material frameDark = Std("M_AluminumDark", new Color(0.10f, 0.105f, 0.12f), 0.28f, 0.2f);
            Material blueBand = Std("M_LawsonBlue", new Color(0.045f, 0.30f, 0.70f), 0.45f, 0.1f,
                new Color(0.05f, 0.30f, 0.75f), 0.55f);
            Material kick = Std("M_KickPanel", new Color(0.10f, 0.11f, 0.13f), 0.25f, 0.3f);

            Mat("M_Glass", "Market/Glass", m =>
            {
                m.SetColor("_Color", new Color(0.40f, 0.47f, 0.56f, 0.06f));
                m.SetFloat("_ReflectionStrength", 0.36f);
                m.SetFloat("_FresnelBoost", 1.5f);
                m.SetFloat("_Glossiness", 0.95f);
            });
            Material glass = s_Materials["M_Glass"];

            // ---- 窗框：立柱 ----
            float[] mullionsX = { -2.92f, -1.46f, 0.02f, 1.46f, 2.44f };
            for (int i = 0; i < mullionsX.Length; i++)
            {
                Box(root, "Mullion_" + i, new Vector3(mullionsX[i], 1.55f, 0f), new Vector3(0.055f, 2.9f, 0.10f), frame);
            }
            // 横撑
            Box(root, "TransomTop", new Vector3(-0.24f, 2.66f, 0f), new Vector3(5.4f, 0.06f, 0.10f), frame);
            Box(root, "TransomSill", new Vector3(-0.24f, 0.47f, 0f), new Vector3(5.4f, 0.06f, 0.12f), frame);
            // 窗下踢脚
            Box(root, "KickPanel", new Vector3(-0.24f, 0.22f, 0f), new Vector3(5.4f, 0.46f, 0.10f), kick);
            // 窗上檐
            Box(root, "Header", new Vector3(-0.24f, 2.90f, 0f), new Vector3(5.42f, 0.34f, 0.16f), Std("M_HeaderDark", new Color(0.055f, 0.055f, 0.065f), 0.2f));
            // 窗下蓝色品牌带
            Box(root, "KickBand", new Vector3(-0.24f, 0.28f, -0.02f), new Vector3(5.40f, 0.16f, 0.02f), blueBand, null, false);

            // ---- 玻璃 ----
            Box(root, "GlassPane_A", new Vector3(-2.19f, 1.57f, 0f), new Vector3(1.40f, 2.16f, 0.02f), glass, null, false);
            Box(root, "GlassPane_B", new Vector3(-0.72f, 1.57f, 0f), new Vector3(1.40f, 2.16f, 0.02f), glass, null, false);
            Box(root, "GlassPane_C", new Vector3(0.74f, 1.57f, 0f), new Vector3(1.40f, 2.16f, 0.02f), glass, null, false);
            Box(root, "GlassPane_D", new Vector3(1.95f, 1.57f, 0f), new Vector3(0.94f, 2.16f, 0.02f), glass, null, false);

            // ---- 左侧自动门（两扇）----
            Box(root, "DoorFrameTop", new Vector3(-4.35f, 2.72f, 0f), new Vector3(3.1f, 0.16f, 0.16f), frameDark);
            Box(root, "DoorFrameL", new Vector3(-5.92f, 1.35f, 0f), new Vector3(0.14f, 2.7f, 0.16f), frameDark);
            Box(root, "DoorGlassA", new Vector3(-5.15f, 1.32f, 0f), new Vector3(1.52f, 2.55f, 0.02f), glass, null, false);
            Box(root, "DoorGlassB", new Vector3(-3.62f, 1.32f, 0f), new Vector3(1.52f, 2.55f, 0.02f), glass, null, false);
            Box(root, "DoorMullion", new Vector3(-4.39f, 1.32f, 0f), new Vector3(0.08f, 2.55f, 0.12f), frame);
            Box(root, "DoorBandA", new Vector3(-5.15f, 0.72f, 0.01f), new Vector3(1.52f, 0.26f, 0.03f), blueBand, null, false);
            Box(root, "DoorBandB", new Vector3(-3.62f, 0.72f, 0.01f), new Vector3(1.52f, 0.26f, 0.03f), blueBand, null, false);
            // 门把手
            Box(root, "HandleA", new Vector3(-4.55f, 1.15f, -0.05f), new Vector3(0.05f, 0.75f, 0.05f), frameDark);
            Box(root, "HandleB", new Vector3(-4.22f, 1.15f, -0.05f), new Vector3(0.05f, 0.75f, 0.05f), frameDark);

            // 玻璃上的贴纸/海报
            Material paper = Std("M_PaperSticker", new Color(0.92f, 0.92f, 0.90f), 0.2f, 0f, new Color(0.85f, 0.85f, 0.82f), 0.35f);
            Box(root, "StickerA", new Vector3(-5.55f, 1.85f, -0.02f), new Vector3(0.45f, 0.6f, 0.01f), paper, null, false);
            Box(root, "StickerB", new Vector3(-2.35f, 1.0f, -0.02f), new Vector3(0.30f, 0.36f, 0.01f), paper, null, false);
        }

        // ================= 店内 =================
        static void BuildInterior()
        {
            Transform root = Group("Interior");

            Material metal = Std("M_ShelfMetal", new Color(0.46f, 0.48f, 0.50f), 0.45f, 0.75f);
            Material shelfWhite = Std("M_ShelfBoard", new Color(0.42f, 0.43f, 0.41f), 0.30f);
            Material priceRail = Std("M_PriceRail", new Color(0.58f, 0.58f, 0.56f), 0.35f);
            Material counterTop = Std("M_CounterTop", new Color(0.15f, 0.17f, 0.21f), 0.55f, 0.05f);
            Material counterBody = Std("M_CounterBody", new Color(0.085f, 0.10f, 0.14f), 0.28f, 0.1f);
            Material darkPlastic = Std("M_DarkPlastic", new Color(0.08f, 0.08f, 0.09f), 0.45f, 0.05f);

            BuildShelfRun(root, metal, shelfWhite, priceRail);
            BuildCounter(root, counterTop, counterBody, darkPlastic);
            BuildCeilingLights(root);
            BuildHangingSign(root);
            BuildInteriorClutter(root, darkPlastic);
        }

        static void BuildShelfRun(Transform root, Material metal, Material board, Material rail)
        {
            Transform g = Group("ShelfRun", root);
            float height = 2.30f;
            float[] levels = { 0.10f, 0.52f, 0.94f, 1.36f, 1.78f, 2.20f };

            // 背板 / 侧板 / 层板
            Box(g, "BackPanel", new Vector3(ShelfBackX, height * 0.5f, (ShelfMinZ + ShelfMaxZ) * 0.5f),
                new Vector3(0.05f, height, ShelfMaxZ - ShelfMinZ), metal);
            Box(g, "SidePanelNear", new Vector3((ShelfFrontX + ShelfBackX) * 0.5f, height * 0.5f, ShelfMaxZ),
                new Vector3(ShelfBackX - ShelfFrontX, height, 0.05f), metal);
            Box(g, "SidePanelFar", new Vector3((ShelfFrontX + ShelfBackX) * 0.5f, height * 0.5f, ShelfMinZ),
                new Vector3(ShelfBackX - ShelfFrontX, height, 0.05f), metal);

            for (int i = 0; i < levels.Length; i++)
            {
                Box(g, "ShelfBoard_" + i, new Vector3((ShelfFrontX + ShelfBackX) * 0.5f, levels[i], (ShelfMinZ + ShelfMaxZ) * 0.5f),
                    new Vector3(ShelfBackX - ShelfFrontX, 0.035f, ShelfMaxZ - ShelfMinZ), board);
                Box(g, "Rail_" + i, new Vector3(ShelfFrontX + 0.02f, levels[i] + 0.05f, (ShelfMinZ + ShelfMaxZ) * 0.5f),
                    new Vector3(0.01f, 0.05f, ShelfMaxZ - ShelfMinZ), rail, null, false);
            }

            // 商品：每层一排彩色小盒子
            Material[] productMats = BuildProductMaterials();
            for (int level = 0; level < levels.Length - 1; level++)
            {
                float y = levels[level] + 0.035f;
                int count = 26;
                for (int i = 0; i < count; i++)
                {
                    float z = Mathf.Lerp(ShelfMinZ + 0.16f, ShelfMaxZ - 0.16f, i / (float)(count - 1));
                    float w = Rand(0.16f, 0.22f);
                    float h = Rand(0.20f, 0.30f);
                    float d = Rand(0.16f, 0.22f);
                    Material m = productMats[(level * 5 + i) % productMats.Length];
                    Box(g, "Product", new Vector3(ShelfFrontX + 0.05f + d * 0.5f, y + h * 0.5f, z),
                        new Vector3(d, h, w), m, new Vector3(0f, Rand(-4f, 4f), 0f), false);

                    // 第二排（更深一点，制造密度）
                    if (i % 2 == 0)
                    {
                        Box(g, "ProductBack", new Vector3(ShelfFrontX + 0.35f + d * 0.5f, y + h * 0.42f, z + Rand(-0.04f, 0.04f)),
                            new Vector3(d, h * 0.9f, w * 0.95f), productMats[(level * 7 + i + 3) % productMats.Length], null, false);
                    }
                }
            }
        }

        static Material[] BuildProductMaterials()
        {
            Color[] palette =
            {
                new Color(0.90f, 0.26f, 0.20f), new Color(0.97f, 0.76f, 0.22f), new Color(0.22f, 0.52f, 0.88f),
                new Color(0.20f, 0.68f, 0.42f), new Color(0.88f, 0.42f, 0.72f), new Color(0.96f, 0.56f, 0.20f),
                new Color(0.70f, 0.30f, 0.68f), new Color(0.94f, 0.90f, 0.84f), new Color(0.32f, 0.34f, 0.40f),
                new Color(0.62f, 0.80f, 0.95f), new Color(0.84f, 0.82f, 0.20f), new Color(0.72f, 0.18f, 0.22f)
            };

            var list = new List<Material>();
            for (int i = 0; i < palette.Length; i++)
            {
                float smooth = 0.25f + (i % 3) * 0.15f;
                list.Add(Std("M_Product_" + i, palette[i], smooth, 0.05f));
            }
            return list.ToArray();
        }

        static void BuildCounter(Transform root, Material top, Material body, Material dark)
        {
            Transform g = Group("Counter", root);

            float cx = (CounterX0 + CounterX1) * 0.5f;

            Box(g, "Body", new Vector3(cx, CounterTopY * 0.5f, (CounterZ0 + CounterZ1) * 0.5f),
                new Vector3(CounterX1 - CounterX0, CounterTopY, CounterZ1 - CounterZ0), body);
            Box(g, "Top", new Vector3(cx, CounterTopY + 0.02f, (CounterZ0 + CounterZ1) * 0.5f),
                new Vector3(CounterX1 - CounterX0 + 0.08f, 0.05f, CounterZ1 - CounterZ0 + 0.08f), top);
            Box(g, "BlueStripe", new Vector3(CounterX0 - 0.005f, 0.72f, (CounterZ0 + CounterZ1) * 0.5f),
                new Vector3(0.01f, 0.10f, CounterZ1 - CounterZ0), Std("M_LawsonBlue", new Color(0.045f, 0.30f, 0.70f), 0.45f, 0.1f, new Color(0.05f, 0.30f, 0.75f), 0.55f));

            // ---- POS 收银机 ----
            Transform pos = Group("POS", g);
            pos.localPosition = new Vector3(1.70f, CounterTopY + 0.05f, -0.55f);

            Box(pos, "Base", new Vector3(0f, 0.05f, 0f), new Vector3(0.46f, 0.10f, 0.34f), dark);
            Box(pos, "Neck", new Vector3(0f, 0.17f, 0.02f), new Vector3(0.12f, 0.22f, 0.12f), dark);

            Material screenMat = Mat("M_POSScreen", "Standard", m =>
            {
                Texture2D tex = Tex("tex_pos_screen", 640, 420, TextureWrapMode.Clamp, DrawPosScreen);
                m.SetTexture("_MainTex", tex);
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", tex);
                m.SetColor("_EmissionColor", new Color(0.72f, 0.85f, 1.0f) * 1.35f);
                m.SetFloat("_Glossiness", 0.6f);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            });
            Material bezel = Std("M_Bezel", new Color(0.13f, 0.13f, 0.14f), 0.4f);

            Box(pos, "Monitor", new Vector3(0f, 0.54f, -0.02f), new Vector3(0.58f, 0.42f, 0.07f), bezel);
            Box(pos, "Screen", new Vector3(0f, 0.54f, -0.056f), new Vector3(0.52f, 0.355f, 0.012f), screenMat, null, false);

            // 客显 / 扫码枪 / 小键盘
            Box(pos, "Scanner", new Vector3(-0.34f, 0.10f, -0.10f), new Vector3(0.16f, 0.14f, 0.16f), dark);
            Box(pos, "Keypad", new Vector3(0.30f, 0.06f, -0.16f), new Vector3(0.22f, 0.04f, 0.16f), dark);

            // ---- 蓝色应用立牌（参考图正下方那块）----
            Transform sign = Group("AppSign", g);
            sign.localPosition = new Vector3(0.55f, 0f, -1.55f);
            Material signMat = Mat("M_AppSign", "Standard", m =>
            {
                Texture2D tex = Tex("tex_app_sign", 384, 512, TextureWrapMode.Clamp, DrawAppSign);
                m.SetTexture("_MainTex", tex);
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", tex);
                m.SetColor("_EmissionColor", new Color(0.85f, 0.92f, 1.0f) * 0.85f);
                m.SetFloat("_Glossiness", 0.35f);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            });
            Box(sign, "Panel", new Vector3(0f, 1.05f, 0f), new Vector3(0.30f, 0.44f, 0.03f), Std("M_AppSignEdge", new Color(0.85f, 0.88f, 0.92f), 0.4f));
            Box(sign, "Face", new Vector3(0f, 1.05f, -0.02f), new Vector3(0.29f, 0.42f, 0.02f), signMat, null, false);
            Box(sign, "Stand", new Vector3(0f, 0.42f, 0f), new Vector3(0.04f, 0.84f, 0.04f), dark);
            Box(sign, "Foot", new Vector3(0f, 0.02f, 0.02f), new Vector3(0.30f, 0.04f, 0.18f), dark);

            // ---- 柜台后的小冰箱/杂物 ----
            Box(g, "BackStock", new Vector3(1.32f, 0.40f, -3.35f), new Vector3(0.9f, 0.8f, 0.55f),
                Std("M_StockBox", new Color(0.62f, 0.58f, 0.50f), 0.2f));
        }

        static void BuildCeilingLights(Transform root)
        {
            Transform g = Group("CeilingLights", root);
            Material housing = Std("M_LightHousing", new Color(0.30f, 0.30f, 0.29f), 0.25f, 0.4f);
            Material tube = Std("M_FluoroTube", new Color(0.95f, 0.97f, 1.0f), 0.6f, 0f, new Color(0.90f, 0.95f, 1.0f), 6.5f);

            float[] xs = { -3.6f, -0.9f, 1.8f, 4.2f };
            float[] zs = { -0.9f, -3.1f, -5.3f, -7.5f };

            for (int xi = 0; xi < xs.Length; xi++)
            {
                for (int zi = 0; zi < zs.Length; zi++)
                {
                    Box(g, "Housing", new Vector3(xs[xi], CeilingY - 0.09f, zs[zi]), new Vector3(0.30f, 0.08f, 2.2f), housing, null, false);
                    Box(g, "Tube", new Vector3(xs[xi], CeilingY - 0.15f, zs[zi]), new Vector3(0.22f, 0.035f, 2.02f), tube, null, false);
                }
            }
        }

        static void BuildHangingSign(Transform root)
        {
            Transform g = Group("HangingSign", root);
            g.position = new Vector3(2.28f, 0f, -0.50f);

            Material posterMat = Mat("M_Poster", "Standard", m =>
            {
                Texture2D tex = Tex("tex_poster", 512, 720, TextureWrapMode.Clamp, DrawPoster);
                m.SetTexture("_MainTex", tex);
                m.EnableKeyword("_EMISSION");
                m.SetTexture("_EmissionMap", tex);
                m.SetColor("_EmissionColor", new Color(0.95f, 0.95f, 1.0f) * 0.32f);
                m.SetFloat("_Glossiness", 0.25f);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            });
            Material back = Std("M_PosterBack", new Color(0.90f, 0.90f, 0.90f), 0.2f);
            Material barMat = Std("M_Aluminum", new Color(0.58f, 0.59f, 0.61f), 0.55f, 0.85f);

            // 挂杆 + 吊线
            Box(g, "HangerBar", new Vector3(0f, 2.88f, 0f), new Vector3(0.80f, 0.035f, 0.035f), barMat);
            Box(g, "HangerWireA", new Vector3(-0.26f, 2.92f, 0f), new Vector3(0.010f, 0.10f, 0.010f), barMat);
            Box(g, "HangerWireB", new Vector3(0.26f, 2.92f, 0f), new Vector3(0.010f, 0.10f, 0.010f), barMat);

            // 海报本体（正面朝 -Z，也就是朝相机）
            Box(g, "PosterBody", new Vector3(0f, 2.34f, 0f), new Vector3(0.68f, 0.94f, 0.02f), back);
            Box(g, "PosterFace", new Vector3(0f, 2.34f, -0.015f), new Vector3(0.66f, 0.91f, 0.01f), posterMat, null, false);
            Box(g, "PosterFaceBack", new Vector3(0f, 2.34f, 0.015f), new Vector3(0.66f, 0.91f, 0.01f), posterMat, null, false);
        }

        static void BuildInteriorClutter(Transform root, Material dark)
        {
            Transform g = Group("Clutter", root);

            // 窗边的杂志架
            Box(g, "MagRack", new Vector3(-3.9f, 0.55f, -0.75f), new Vector3(0.8f, 1.1f, 1.6f),
                Std("M_MagRack", new Color(0.78f, 0.78f, 0.76f), 0.3f));
            for (int i = 0; i < 6; i++)
            {
                Box(g, "Magazine", new Vector3(-3.88f, 0.95f, -1.35f + i * 0.17f), new Vector3(0.05f, 0.30f, 0.14f),
                    Std("M_Mag_" + i, Color.HSVToRGB((i * 0.13f) % 1f, 0.55f, 0.85f), 0.3f), null, false);
            }

            // 地上的纸箱/购物篮
            Box(g, "Crate", new Vector3(-4.9f, 0.22f, -2.4f), new Vector3(0.7f, 0.44f, 0.55f),
                Std("M_Crate", new Color(0.55f, 0.42f, 0.26f), 0.2f));
            Box(g, "BasketStack", new Vector3(-4.2f, 0.16f, -1.0f), new Vector3(0.45f, 0.32f, 0.45f),
                Std("M_Basket", new Color(0.78f, 0.40f, 0.20f), 0.3f));

            // 玻璃上贴的价签（少量，避免喧宾夺主）
            Material tagWhite = Std("M_PriceTag", new Color(0.86f, 0.86f, 0.84f), 0.2f, 0f, new Color(0.9f, 0.9f, 0.88f), 0.5f);
            Box(g, "TagThreadA", new Vector3(-1.95f, 2.62f, -0.02f), new Vector3(0.008f, 0.10f, 0.008f), dark, null, false);
            Box(g, "TagA", new Vector3(-1.95f, 2.52f, -0.03f), new Vector3(0.15f, 0.10f, 0.008f), tagWhite, null, false);
            Box(g, "TagThreadB", new Vector3(-1.58f, 2.62f, -0.02f), new Vector3(0.008f, 0.16f, 0.008f), dark, null, false);
            Box(g, "TagB", new Vector3(-1.58f, 2.42f, -0.03f), new Vector3(0.12f, 0.08f, 0.008f), tagWhite, null, false);

            // 左侧冷藏柜：冷白发光，正好给左边那几块玻璃提供反射内容
            Transform cooler = Group("CoolerCase", g);
            cooler.localPosition = new Vector3(-5.35f, 0f, -1.9f);
            Material coolerBody = Std("M_CoolerBody", new Color(0.42f, 0.44f, 0.46f), 0.45f, 0.5f);
            Material coolerGlow = Std("M_CoolerGlow", new Color(0.95f, 0.98f, 1.0f), 0.5f, 0f, new Color(0.80f, 0.90f, 1.0f), 2.6f);
            Box(cooler, "Body", new Vector3(0f, 1.05f, 0f), new Vector3(0.75f, 2.10f, 3.10f), coolerBody);
            Box(cooler, "Glow", new Vector3(0.30f, 1.10f, 0f), new Vector3(0.10f, 1.70f, 2.80f), coolerGlow, null, false);
            for (int i = 0; i < 4; i++)
            {
                Box(cooler, "CoolerShelf_" + i, new Vector3(0.30f, 0.42f + i * 0.42f, 0f),
                    new Vector3(0.18f, 0.03f, 2.80f), Std("M_PriceRail", new Color(0.58f, 0.58f, 0.56f), 0.35f), null, false);
            }
        }

        // ================= 程序化贴图 =================
        static Texture2D Tex(string fileName, int w, int h, TextureWrapMode wrap, System.Action<Color32[], int, int> draw)
        {
            string path = GeneratedFolder + "/" + fileName + ".png";
            var pixels = new Color32[w * h];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
            draw(pixels, w, h);

            Texture2D tmp = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tmp.SetPixels32(pixels);
            tmp.Apply();
            byte[] png = tmp.EncodeToPNG();
            Object.DestroyImmediate(tmp);

            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? GeneratedFolder);
            File.WriteAllBytes(path, png);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);

            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer != null)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.mipmapEnabled = true;
                importer.wrapMode = wrap;
                importer.filterMode = FilterMode.Trilinear;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.anisoLevel = 4;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Color32 Rgb(int r, int g, int b, int a = 255)
        {
            return new Color32((byte)Mathf.Clamp(r, 0, 255), (byte)Mathf.Clamp(g, 0, 255), (byte)Mathf.Clamp(b, 0, 255), (byte)a);
        }

        static void Fill(Color32[] px, int w, int h, Color32 c)
        {
            for (int i = 0; i < px.Length; i++) px[i] = c;
        }

        static void RectF(Color32[] px, int w, int h, int x0, int y0, int rw, int rh, Color32 c)
        {
            for (int y = y0; y < y0 + rh; y++)
            {
                if (y < 0 || y >= h) continue;
                for (int x = x0; x < x0 + rw; x++)
                {
                    if (x < 0 || x >= w) continue;
                    px[y * w + x] = c;
                }
            }
        }

        static void DrawTile(Color32[] px, int w, int h)
        {
            Fill(px, w, h, Rgb(148, 150, 143));
            var rng = new System.Random(15);
            // 细微噪点
            for (int i = 0; i < px.Length; i++)
            {
                int v = rng.Next(-6, 6);
                Color32 c = px[i];
                px[i] = Rgb(c.r + v, c.g + v, c.b + v);
            }
            Color32 grout = Rgb(104, 106, 103);
            RectF(px, w, h, 0, 0, w, 6, grout);
            RectF(px, w, h, 0, h - 6, w, 6, grout);
            RectF(px, w, h, 0, 0, 6, h, grout);
            RectF(px, w, h, w - 6, 0, 6, h, grout);
        }

        static void DrawPoster(Color32[] px, int w, int h)
        {
            Fill(px, w, h, Rgb(238, 244, 250));

            // 顶部深蓝标题带
            RectF(px, w, h, 0, h - 150, w, 150, Rgb(26, 62, 150));
            RectF(px, w, h, 40, h - 110, 300, 46, Rgb(255, 255, 255));
            RectF(px, w, h, 40, h - 56, 190, 16, Rgb(180, 210, 255));
            RectF(px, w, h, w - 130, h - 96, 90, 60, Rgb(255, 214, 120));

            // 中部图片区 2x3
            Color32[] blocks =
            {
                Rgb(246, 196, 70), Rgb(240, 168, 190), Rgb(150, 204, 240),
                Rgb(232, 226, 210), Rgb(206, 168, 226), Rgb(170, 216, 180)
            };
            int bx = 36, by = h - 470, bw = 200, bh = 180, gap = 40;
            for (int i = 0; i < 6; i++)
            {
                int cx = bx + (i % 2) * (bw + gap);
                int cy = by - (i / 2) * (bh + 26);
                RectF(px, w, h, cx, cy, bw, bh, blocks[i]);
                // 简单“商品”剪影
                RectF(px, w, h, cx + 60, cy + 40, 80, 80, Rgb(255, 255, 255));
                RectF(px, w, h, cx + 74, cy + 56, 52, 48, blocks[(i + 3) % 6]);
            }

            // 底部说明文字（用短横条表示）
            for (int i = 0; i < 3; i++)
            {
                RectF(px, w, h, 44, 90 - i * 26, 260 - i * 60, 12, Rgb(120, 140, 170));
            }
            RectF(px, w, h, 0, 0, w, 60, Rgb(26, 62, 150));
            RectF(px, w, h, 44, 24, 150, 12, Rgb(255, 255, 255));
        }

        static void DrawPosScreen(Color32[] px, int w, int h)
        {
            Fill(px, w, h, Rgb(236, 242, 248));

            // 顶部标题
            RectF(px, w, h, 0, h - 56, w, 56, Rgb(36, 96, 190));
            RectF(px, w, h, 16, h - 40, 120, 18, Rgb(255, 255, 255));

            // 左列：商品行
            for (int i = 0; i < 7; i++)
            {
                int y = h - 100 - i * 36;
                RectF(px, w, h, 20, y, 150, 10, Rgb(120, 136, 156));
                RectF(px, w, h, 190, y, 44, 10, Rgb(150, 166, 186));
                RectF(px, w, h, 250, y, 56, 10, Rgb(90, 110, 140));
            }

            // 右侧键盘
            int kx = 340, ky = 70, ks = 58, gap = 8;
            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    RectF(px, w, h, kx + c * (ks + gap), ky + r * (ks + gap), ks, ks, Rgb(212, 220, 230));
                    RectF(px, w, h, kx + c * (ks + gap) + 6, ky + r * (ks + gap) + 6, ks - 12, ks - 12, Rgb(246, 250, 255));
                }
            }

            // 合计区
            RectF(px, w, h, 20, 34, 300, 40, Rgb(255, 255, 255));
            RectF(px, w, h, 240, 44, 70, 18, Rgb(30, 30, 34));
        }

        static void DrawAppSign(Color32[] px, int w, int h)
        {
            Fill(px, w, h, Rgb(46, 110, 196));

            RectF(px, w, h, 24, h - 120, w - 48, 70, Rgb(255, 255, 255));
            RectF(px, w, h, 36, h - 104, 190, 22, Rgb(46, 110, 196));
            RectF(px, w, h, 36, h - 72, 130, 14, Rgb(130, 170, 220));

            // 中间白卡
            RectF(px, w, h, 24, 120, w - 48, h - 260, Rgb(250, 252, 255));
            RectF(px, w, h, 52, 260, 120, 90, Rgb(240, 180, 60));
            RectF(px, w, h, 200, 260, 130, 90, Rgb(120, 190, 240));
            for (int i = 0; i < 4; i++)
            {
                RectF(px, w, h, 52, 220 - i * 22, 190 - i * 20, 10, Rgb(150, 165, 190));
            }

            // 底部按钮条
            RectF(px, w, h, 24, 36, w - 48, 64, Rgb(255, 255, 255));
            RectF(px, w, h, 48, 52, 140, 32, Rgb(46, 110, 196));
        }

        // ================= 预览截图 =================
        public static void CapturePreview(string filePath)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogError("[Market3D] 场景里没有 MainCamera");
                return;
            }

            foreach (PlanarReflection pr in Object.FindObjectsByType<PlanarReflection>(FindObjectsSortMode.None))
            {
                pr.RenderReflection(cam);
            }

            const int width = 1920;
            const int height = 1080;

            RenderTexture rt = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previous = cam.targetTexture;

            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = previous;

            RenderTexture.active = rt;
            var tex = new Texture2D(width, height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);
            RenderTexture.ReleaseTemporary(rt);

            string dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            File.WriteAllBytes(filePath, png);
        }
    }
}
