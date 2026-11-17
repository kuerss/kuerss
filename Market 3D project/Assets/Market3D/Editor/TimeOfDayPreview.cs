using UnityEditor;
using UnityEngine;

namespace TenPercent2D.Market3D
{
    /// <summary>
    /// 不进 Play 模式也能把三个时段各渲染一张 1920×1080 预览图，方便对比与写文档。
    /// 菜单：Market3D > 导出三时段预览图
    ///
    /// 换天空盒后环境反射要过一帧才刷新，所以这里用 EditorApplication.update 分帧
    /// 套预设 / 截图，避免柜台、货架这类光滑表面残留上一个时段的反射色。
    /// </summary>
    public static class TimeOfDayPreview
    {
        static readonly string[] FileNames =
        {
            "market3d_day.png",
            "market3d_dusk.png",
            "market3d_night.png"
        };

        static int s_Index;
        static int s_WaitFrames;
        static bool s_Running;
        static TimeOfDaySystem s_System;

        [MenuItem("Market3D/导出三时段预览图", false, 22)]
        public static void CaptureAll()
        {
            if (s_Running) return;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[Market3D] 三时段预览图请在退出 Play 模式后导出。");
                return;
            }

            s_System = Object.FindFirstObjectByType<TimeOfDaySystem>();
            if (s_System == null)
            {
                Debug.LogError("[Market3D] 场景里没有 TimeOfDaySystem，先重新生成场景。");
                return;
            }

            s_Running = true;
            s_Index = 0;
            s_WaitFrames = 0;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            if (!s_Running) { EditorApplication.update -= Tick; return; }
            if (s_System == null) { Finish(); return; }

            int count = Mathf.Min(FileNames.Length, s_System.presets != null ? s_System.presets.Length : 0);
            if (s_Index >= count)
            {
                // 改回黄昏，避免存盘时把天空盒/灯光停在最后一个预览状态。
                s_System.ApplyPresetImmediate(1);
                DynamicGI.UpdateEnvironment();
                Finish();
                return;
            }

            if (s_WaitFrames == 0)
            {
                s_System.ApplyPresetImmediate(s_Index);
                DynamicGI.UpdateEnvironment();
                s_WaitFrames = 2;
                return;
            }

            s_WaitFrames--;
            if (s_WaitFrames > 0) return;

            MarketSceneBuilder.CapturePreview("Docs/Previews/" + FileNames[s_Index]);
            Debug.Log("[Market3D] 已导出 " + FileNames[s_Index]);
            s_Index++;
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            AssetDatabase.SaveAssets();
            s_Running = false;
            s_System = null;
            Debug.Log("[Market3D] 三时段预览图导出完成。");
        }
    }
}
