---
name: 3d-project-helper
description: Unity 3D 游戏 / 3D 场景工作区手册（内部代号 3DProject_Helper），对应工程 `D:\Development_Of_Games\Program\Source\kuerss\Market 3D project`：2D 工程与 3D 工程的真实差别、渲染管线选型（Built-in / URP / HDRP）与代价、3D 场景的构图/打光/后期约定、程序化生成场景的架构（Market3D 那一套：日落天空盒 / 湿地面平面反射 / 玻璃反射 / 自写后期）、性能预算与已知坑。**仅在这个 3D 工作区启用**；2D 的活在 TenPercent2D 那个工作区，用那边的 2d-project-helper。
whenToUse: 在这个 3D 工作区里要改 Market 场景、要搭新 3D 场景与打光、要选渲染管线、问「这个工程怎么组织的」、要往工程里加 3D 资产，或提到 Market3D / 3DProject_Helper / Market 场景时。
---

# Unity 3D 工作区手册（Market 3D project）

环境：**Unity 2023.1.12f1**，编辑器 `D:\Development_Of_Games\Software\Unity\2023.1.12f1\Editor\Unity.exe`。
工程根目录 = 本工作区根目录：`D:\Development_Of_Games\Program\Source\kuerss\Market 3D project`（3D 模板、**Built-in 管线**、Linear 色彩空间）。
姊妹手册在另一个工作区：`2d-project-helper`（TenPercent2D，纯 2D，别混用）。

## 0. 先确认边界

- **本工作区 = 3D**（真 3D 几何、透视相机、实时光照）→ 用本手册。
- **TenPercent2D 那边 = 2D**（精灵、Tilemap、2.5D 平面战斗）→ 那是另一个工作区，用 `2d-project-helper`。
- 3D 内容 2026-11-17 已从 TenPercent2D 整体迁出，2D 仓库里不留 3D 文件，别回去改。

## 1. 2D 工程和 3D 工程到底差在哪（当初为什么单开一个）

Unity 工程**没有本质区别**，模板只改默认值。真正不同的是这些：

| 项 | 2D 模板 | 3D 模板 | 影响 |
|---|---|---|---|
| `ProjectSettings/EditorSettings.asset` → `m_DefaultBehaviorMode` | 1（2D） | 0（3D） | 新场景 Scene 视图默认正交、新建物体默认 Sprite |
| 新建场景的相机 | Orthographic | Perspective | 只影响新场景，已有场景无所谓 |
| 贴图导入默认值 | Texture Type = Sprite | Default | 3D 里把图当 Sprite 导会多一份浪费 |
| 渲染管线（模板自带） | Built-in / URP 2D Renderer | Built-in / URP 3D | URP 2D Renderer **不能**渲染 3D 光照与阴影 |
| 质量/光照默认值 | 阴影距离小、实时 GI 常关 | 开阴影、开 GI 相关默认 | 3D 场景要自己确认 `shadowDistance` 之类 |
| 自带包 | Tilemap/2D 相关 | 无额外 | 影响不大：模块都是内置的 |

同一个工程理论上 2D/3D 都能做（只要不是 URP 2D Renderer），但**分开放**的理由是：默认值互不干扰、资产库/编译/出包设置独立、3D 将来要上 URP 时不用动 2D 主线。

## 2. 渲染管线怎么选（有坑）

| 管线 | 适合 | 代价 |
|---|---|---|
| **Built-in（当前）** | 预研、快速验证、老项目 | 后期全靠自己写（OnRenderImage）；没有 Volume/Decal/Shader Graph |
| **URP** | 正式 3D 项目主流选择 | **Surface Shader 不能用**，现有 6 个 shader 要重写成 HLSL 或 Shader Graph；自定义平面反射要改成 ScriptableRendererFeature 或 Camera 回调 |
| **HDRP** | 写实、大场景、PBR 重度 | 学习与性能成本最高，小团队慎入 |

⚠️ **本机现状（2023.1.12f1）**：本地模板目录只有 `com.unity.template.2d-8.0.2.tgz`、`com.unity.template.3d-8.1.3.tgz`、`com.unity.template.universal-2d-3.0.2.tgz`，**没有 universal-3d 模板**。
所以断网时用命令行建出来的是 **3D (Built-in)**；要 URP 得从 registry 拉 `com.unity.render-pipelines.universal`（需要网络）或走 Unity Hub。

## 3. 工程结构（What's where）

```
Assets/Market3D/
  Editor/MarketSceneBuilder.cs   # 唯一的"场景源文件"：几何/材质/灯光/相机全在这里生成
  Editor/MarketMcpBoot.cs        # 加载时自动拉起 MCP 桥（只为让外部 MCP 客户端能连上，可删）
  Scripts/PlanarReflection.cs    # 平面反射（水平面 + 竖直面各一台镜像相机）
  Scripts/MarketPostFX.cs        # 自写后期：泛光 + 调色 + 暗角（不吃 Post Processing Stack）
  Scripts/MarketCameraDrift.cs   # 播放时的轻微呼吸
  Shaders/  SunsetSky / WetAsphalt / Glass / GlossyTile / Facade / PostFX
  Generated/ 程序化贴图（瓷砖、海报、POS 屏、蓝色立牌）
  Materials/ 生成器写出的材质
Assets/Scenes/Market.unity       # 生成结果
Docs/Market3D.md                 # 场景详细说明、构图参数、已知限制
Docs/Previews/                   # 预览图（market3d_preview.png / market3d_outside.png）
```

核心思想：**场景 = 代码**。所有物体由 `MarketSceneBuilder.Build()` 用 `GameObject.CreatePrimitive + 程序化贴图 + 材质资产` 拼出来，可重复生成、可 diff、不带任何第三方美术资产。适合"预研镜头"，不适合手摆关卡。

菜单：`Market3D > 生成 Market 场景` / `Market3D > 导出当前视角预览图`。
也可以用 MCP `execute_code`：
```csharp
TenPercent2D.Market3D.MarketSceneBuilder.Build();
TenPercent2D.Market3D.MarketSceneBuilder.CapturePreview("Docs/Previews/market3d_preview.png");
```

## 4. 3D 场景搭建约定（Market 调出来的数值，可直接复用）

- **相机**：竖 FOV 55（16:9 时横 ≈ 91°，广角感），俯 1.5°；离玻璃 3 m。地平线压在画面中线稍上。
- **天花板压低到 2.95**：否则天花板吃掉画面上部，窗户里的天空就不够。
- **打光**：方向光只当"太阳"（强度 ~1.75，暖橘）；窗口放一盏 Spot 模拟泼进来的光；室内靠 4~5 盏暖白 Point（1.8 左右）+ Trilight 环境光（天顶偏紫、赤道暖、地面接近黑）。
- **雾**：指数平方、密度 0.006 左右、颜色比天空暗一档；密度一高远景就糊成一片。
- **后期泛光阈值一定要调高**（1.9 左右）：阈值低会让整面天空糊掉，只剩"过曝的橘色"。
- **湿地面**：反射用镜像相机 RT（屏幕空间采样），强度按 Fresnel 拉开；积水靠程序化噪声遮罩。
- **玻璃**：半透明 + 竖直平面反射（法线朝店内），玻璃上才会叠出店内货架的影子。
- **远景建筑**：世界坐标算窗格 + 随机亮灯，不依赖贴图，一栋楼就是一个 Cube。

## 5. 性能与常见坑

- 每帧多渲染**两台镜像相机**（1280×720 + 1024×576）；`[ExecuteAlways]` 在编辑器里也跑，嫌卡就禁用 `PlanarReflection` 组件。
- 反射相机 `CopyFrom(main)` 后要重新设 `targetTexture`，并用 `CalculateObliqueMatrix` 裁掉平面另一侧。
- **MCP `execute_code` 调的是已编译程序集**：改了 C# 必须 `refresh_unity`（compile）等编译完再调用生成器，否则"改了没反应"（跑的是旧代码）。
- `Build()` 每次都 `EditorSceneManager.NewScene` → **不要手改场景物体**，改生成器。
- Built-in 的 Surface Shader 在 URP 下不工作；用 URP 就得整套重写。
- 程序化天空盒里的云在低仰角容易被拉成细条，用「方位角/仰角」投影而不是平面投影。
- 材质/贴图是按名字复用+覆盖的，重复 Build 不会堆垃圾，但**会覆盖同名资产**；复制整个 `Assets/Market3D` 时要连 `.meta` 一起拷，否则场景丢引用（材质/贴图靠 GUID 互相引用）。

## 6. MCP（UnityMCP）

- 工程已装 `com.coplaydev.unity-mcp`（本地包 `D:/Development_Of_Games/Software/unity-mcp-beta/MCPForUnity`，默认桥端口 6400）。
- 桥没起来时 `execute_code` 等工具会报没有实例：先在 Unity 里 `Window > MCP for Unity` → Start Bridge，或依赖 `MarketMcpBoot.cs` 自动拉起。
- 2D 和 3D 两个工程可能同时开着 Unity，操作前先 `set_active_instance`（实例 id 形如 `Market 3D project@<hash>`），别对着 2D 工程乱改。

## 7. 以后要新建 3D 工程

```powershell
$unity  = 'D:\Development_Of_Games\Software\Unity\2023.1.12f1\Editor\Unity.exe'
$target = 'D:\Development_Of_Games\Program\Source\<新工程名>'
# 先用 Unity Hub「新建 3D 工程」最省事；命令行等价于：
& $unity -createProject $target -quit -batchmode -logFile "$target\create.log"
```

注意：
1. `-createProject` 要求目标目录不存在，且 Unity 未占用该路径。
2. 建完确认 `m_DefaultBehaviorMode: 0`、Color Space = Linear、Quality 的阴影距离。
3. 3D 工程建议开：Linear、MSAA、HDR 相机；补一份 `.gitignore`（忽略 `Library/ Temp/ Logs/ obj/`）。
