using System.Collections.Generic;
using AFPS.Bootstrap;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Metrics;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Sessions;
using AFPS.NetCode.Transport;
using AFPS.Simulation.Characters;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AFPS.Tests.EditMode
{
    public sealed class NetworkDebugPanelTests
    {
        [TestCase(1920, 1080)]
        [TestCase(1440, 1080)]
        public void ToggleShowsChineseReadOnlyMetricsWithoutOverflowOrBlockingInput(int width, int height)
        {
            LocalGameTransport.CreatePair(out var server, out var client);
            var root = new GameObject("Diagnostics test");
            try
            {
                var initial = new PlayerMovementState { IsGrounded = true };
                var config = new PlayerMovementConfig(6f, 20f, 20f, 8f);
                var manager = new NetworkSessionCoordinator(
                    server,
                    client,
                    initial,
                    initial,
                    config,
                    0.02f,
                    64,
                    3,
                    16,
                    2,
                    2,
                    AuthoritativeMovementStateCodec.RecommendedPositionErrorThreshold,
                    AuthoritativeMovementStateCodec.RecommendedVelocityErrorThreshold,
                    isLocalPractice: true,
                    collisionWorld: global::AFPS.Simulation.Characters.Collision.FlatGroundCollisionWorld.Instance
                );
                for (uint id = 1; id <= 4; id++)
                {
                    manager.HandleConnected(NetworkTransportSide.Server, new TransportConnectionId(id));
                }
                var panel = root.AddComponent<NetworkDebugPanel>();
                panel.Configure(manager, null);
                Assert.That(panel.IsVisible, Is.False);
                Assert.That(root.GetComponentInChildren<Canvas>(true), Is.Null, "默认关闭不创建UI");
                panel.SetVisible(true);
                var canvas = root.GetComponentInChildren<Canvas>(true);
                canvas.GetComponent<CanvasScaler>().enabled = false;
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(width, height);
                Canvas.ForceUpdateCanvases();
                Assert.That(panel.DisplayedText, Does.Contain("单人练习"));
                Assert.That(panel.DisplayedText, Does.Contain("无公网RTT"));
                Assert.That(panel.DisplayedText, Does.Contain("非总流量"));
                foreach (var text in root.GetComponentsInChildren<TMP_Text>())
                {
                    text.ForceMeshUpdate();
                    Assert.That(text.isTextOverflowing, Is.False, text.text);
                }
                foreach (var graphic in root.GetComponentsInChildren<Graphic>())
                {
                    Assert.That(graphic.raycastTarget, Is.False);
                }
                var rows = new List<ServerConnectionDiagnostics>();
                manager.CopyServerDiagnostics(rows);
                Assert.That(rows.Count, Is.EqualTo(4));
                for (int index = 0; index < rows.Count; index++)
                {
                    Assert.That(rows[index].EntityId, Is.EqualTo(index + 1));
                    Assert.That(rows[index].InputTick, Is.Zero, "面板不得消费输入或推进模拟");
                }
                panel.SetVisible(false);
                Assert.That(canvas.enabled, Is.False);
                panel.SetVisible(true);
                Assert.That(root.GetComponentsInChildren<Canvas>(true).Length, Is.EqualTo(1));
            }
            finally
            {
                Object.DestroyImmediate(root);
                server.Dispose();
                client.Dispose();
            }
        }
    }
}
