using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using GamePlayDemo;

namespace GamePlayDemo.Editor
{
    public static class GamePlayDemoSceneBuilder
    {
        [MenuItem("GamePlayDemo/创建 GamePlayDemo 场景")]
        public static void BuildScene()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var camGo = new GameObject("Main Camera");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.05f, 0.06f, 0.08f, 1f);
            cam.orthographic = true;
            camGo.tag = "MainCamera";

            var demoGo = new GameObject("GamePlayDemo");
            demoGo.AddComponent<GamePlayDemoController>();

            bool ok = EditorSceneManager.SaveScene(scene, "Assets/Scenes/GamePlayDemo.unity");
            AssetDatabase.Refresh();
            Debug.Log("[GamePlayDemo] scene saved: " + ok);
        }
    }
}
