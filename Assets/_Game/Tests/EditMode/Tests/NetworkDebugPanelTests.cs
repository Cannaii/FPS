using System.Collections.Generic;
using System.IO;
using System.Reflection;
using AFPS.Bootstrap;
using AFPS.Editor.UI;
using AFPS.Input;
using AFPS.NetCode.Messages;
using AFPS.NetCode.Metrics;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Sessions;
using AFPS.NetCode.Transport;
using AFPS.Simulation.Characters;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static AFPS.Tests.EditMode.SimulationAdmissionTests;

namespace AFPS.Tests.EditMode
{
    public sealed class NetworkDebugPanelTests
    {
        [Test]
        public void Prefab_HasPersistentFontAndNoInputRaycaster()
        {
            var prefab = Resources.Load<NetworkDebugView>(NetworkDebugView.ResourcePath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.TryValidate(out string error), Is.True, error);
            Assert.That(prefab.GetComponent<GraphicRaycaster>(), Is.Null);
            Assert.That(prefab.Canvas.sortingOrder, Is.EqualTo(100));
            foreach (var graphic in prefab.GetComponentsInChildren<Graphic>(true))
            {
                Assert.That(graphic.raycastTarget, Is.False);
            }

            foreach (var text in prefab.GetComponentsInChildren<TMP_Text>(true))
            {
                Assert.That(AssetDatabase.GetAssetPath(text.font), Is.EqualTo(NetworkLobbyAssetBuilder.FontPath));
                Assert.That(text.richText, Is.False);
                Assert.That(text.transform.IsChildOf(prefab.transform), Is.True);
            }
        }

        [Test]
        public void ReconfigureAndHiddenUpdate_KeepAuthoredStyleAndDoNotCaptureInput()
        {
            var root = new GameObject("Diagnostics resource binding test");
            var wire = new FakeTransport(TransportRole.Server);
            try
            {
                var manager = CreateManager(wire, null, Context());
                var input = root.AddComponent<LocalPlayerInputCollector>();
                var panel = root.AddComponent<NetworkDebugPanel>();
                bool cursorVisible = Cursor.visible;
                var cursorLock = Cursor.lockState;
                panel.Configure(manager, null);
                Assert.That(root.GetComponentInChildren<Canvas>(true), Is.Null);
                panel.SetVisible(true);
                var view = root.GetComponentInChildren<NetworkDebugView>(true);
                Assert.That(view.name, Is.EqualTo("Network Debug UI"));
                view.ClientText.fontSize = 23f;
                view.ClientText.rectTransform.anchoredPosition = new Vector2(8f, 12f);
                view.ServerText.color = Color.cyan;
                panel.Configure(manager, null);
                Assert.That(panel.IsVisible, Is.False);
                Assert.That(view.Canvas.enabled, Is.False);
                view.ClientText.text = "隐藏时保留文字";
                Invoke(panel, "Update");
                Assert.That(view.ClientText.text, Is.EqualTo("隐藏时保留文字"));
                panel.SetVisible(true);
                Assert.That(view.ClientText.text, Does.Contain("客户端与网络"));
                Assert.That(view.ClientText.fontSize, Is.EqualTo(23f));
                Assert.That(view.ClientText.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(8f, 12f)));
                Assert.That(view.ServerText.color, Is.EqualTo(Color.cyan));
                Assert.That(root.GetComponentsInChildren<Canvas>(true).Length, Is.EqualTo(1));
                Assert.That(input.enabled, Is.True);
                Assert.That(Cursor.visible, Is.EqualTo(cursorVisible));
                Assert.That(Cursor.lockState, Is.EqualTo(cursorLock));

                // EditMode不自动调度普通MonoBehaviour生命周期；显式验证其清理边界。
                Invoke(panel, "OnDisable");
                Assert.That(panel.IsVisible, Is.False);
                Assert.That(view.Canvas.enabled, Is.False);
                Invoke(panel, "OnDestroy");
                Assert.That(view == null, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
                wire.Dispose();
            }
        }

        [Test]
        public void MissingBinding_ReportsDiagnosticField()
        {
            var view = Object.Instantiate(Resources.Load<NetworkDebugView>(NetworkDebugView.ResourcePath));
            try
            {
                var serialized = new SerializedObject(view);
                serialized.FindProperty("detailText").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(view.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("detailText"));
            }
            finally
            {
                Object.DestroyImmediate(view.gameObject);
            }
        }

        [Test]
        public void Generator_PreservesExistingPrefabAndSharedFont()
        {
            byte[] prefab = File.ReadAllBytes(NetworkDebugAssetBuilder.PrefabPath);
            byte[] font = File.ReadAllBytes(NetworkLobbyAssetBuilder.FontPath);
            NetworkDebugAssetBuilder.Create();
            Assert.That(File.ReadAllBytes(NetworkDebugAssetBuilder.PrefabPath), Is.EqualTo(prefab));
            Assert.That(File.ReadAllBytes(NetworkLobbyAssetBuilder.FontPath), Is.EqualTo(font));
        }

        private static void Invoke(NetworkDebugPanel panel, string method)
        {
            typeof(NetworkDebugPanel).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(panel, null);
        }

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
