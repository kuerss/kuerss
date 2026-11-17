using System;
using System.Threading.Tasks;
using MCPForUnity.Editor.Services;
using UnityEditor;
using UnityEngine;

namespace Market3D.Bootstrap
{
    /// <summary>
    /// 一次性引导脚本：编辑器加载后如果 MCP 桥没有在跑，就把它拉起来。
    /// 目的只是让外部 MCP 客户端（DSH）在新工程里能连上；
    /// 连上之后这份脚本可以删掉，或者在 Window > MCP for Unity 里打开「Auto-Start on Editor Load」。
    /// </summary>
    [InitializeOnLoad]
    internal static class MarketMcpBoot
    {
        static MarketMcpBoot()
        {
            EditorApplication.delayCall += Boot;
        }

        static async void Boot()
        {
            try
            {
                var bridge = MCPServiceLocator.Bridge;
                if (bridge == null)
                {
                    Debug.LogWarning("[Market3D] MCP bridge service 不可用");
                    return;
                }

                if (bridge.IsRunning)
                {
                    Debug.Log("[Market3D] MCP bridge 已在运行，端口 " + bridge.CurrentPort);
                    return;
                }

                bool started = await bridge.StartAsync();
                Debug.Log("[Market3D] MCP bridge 启动结果: " + started + "，端口 " + bridge.CurrentPort);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Market3D] MCP bridge 启动失败: " + e.Message);
            }
        }
    }
}
