using System.IO;
using System.Reflection;
using AFPS.Bootstrap;
using AFPS.Editor.UI;
using AFPS.Input;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Transport;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static AFPS.Tests.EditMode.SimulationAdmissionTests;

namespace AFPS.Tests.EditMode
{
    public sealed class NetworkRecoveryViewTests
    {
        private GameObject root;
        private GameObject eventRoot;
        private NetworkRecoveryOverlay overlay;
        private UnityNetworkBootstrap owner;
        private LocalPlayerInputCollector input;
        private CursorLockMode originalLock;
        private bool originalVisible;

        [SetUp]
        public void SetUp()
        {
            originalLock = Cursor.lockState;
            originalVisible = Cursor.visible;
            if (EventSystem.current == null)
            {
                eventRoot = new GameObject("Recovery test event system", typeof(EventSystem));
            }

            root = new GameObject("Recovery binding test");
            input = root.AddComponent<LocalPlayerInputCollector>();
            owner = root.AddComponent<UnityNetworkBootstrap>();
            overlay = root.AddComponent<NetworkRecoveryOverlay>();
            overlay.Configure(owner);
            typeof(NetworkRecoveryOverlay).GetField("input", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(overlay, input);
        }

        [TearDown]
        public void TearDown()
        {
            // EditMode不自动调度普通MonoBehaviour的清理回调，不能把此处当作PlayMode生命周期证据。
            if (overlay != null)
            {
                Invoke("OnDestroy");
            }

            Object.DestroyImmediate(root);
            if (eventRoot != null)
            {
                Object.DestroyImmediate(eventRoot);
            }

            Cursor.lockState = originalLock;
            Cursor.visible = originalVisible;
        }

        [Test]
        public void Prefab_HasPersistentFontAndBlocksBackgroundInput()
        {
            var prefab = Resources.Load<NetworkRecoveryView>(NetworkRecoveryView.ResourcePath);
            Assert.That(prefab.TryValidate(out string error), Is.True, error);
            Assert.That(prefab.GetComponent<GraphicRaycaster>(), Is.Not.Null);
            Assert.That(prefab.Canvas.sortingOrder, Is.EqualTo(1200));
            foreach (var label in prefab.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                Assert.That(AssetDatabase.GetAssetPath(label.font), Is.EqualTo(NetworkLobbyAssetBuilder.FontPath));
                Assert.That(label.raycastTarget, Is.False);
            }

            Assert.That(prefab.Retry.targetGraphic.raycastTarget, Is.True);
            Assert.That(prefab.Cancel.targetGraphic.raycastTarget, Is.True);
            Assert.That(prefab.JoinAsNew.targetGraphic.raycastTarget, Is.True);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void FocusAndButtons_FollowRecoveryStateAndRestoreOriginalInput(bool initiallyEnabled)
        {
            input.enabled = initiallyEnabled;
            var view = root.GetComponentInChildren<NetworkRecoveryView>(true);
            Assert.That(view.Canvas.enabled, Is.False);
            Render(ClientReconnectStatus.Waiting, true, "正在等待重连");
            Assert.That(view.Canvas.enabled, Is.True);
            Assert.That(input.enabled, Is.False);
            Assert.That(view.Retry.interactable, Is.False);
            Assert.That(view.Cancel.interactable, Is.True);
            Render(ClientReconnectStatus.Cancelled, false, "已取消自动重连");
            Assert.That(view.Message.text, Is.EqualTo("已取消自动重连"));
            Assert.That(view.Retry.interactable, Is.True);
            Assert.That(view.Cancel.interactable, Is.False);
            Assert.That(input.enabled, Is.False);
            Render(ClientReconnectStatus.Idle, false, "");
            Assert.That(view.Canvas.enabled, Is.False);
            Assert.That(input.enabled, Is.EqualTo(initiallyEnabled));
        }

        [Test]
        public void ReconfigureAndCleanup_PreserveLayoutAndReleaseFocus()
        {
            var view = root.GetComponentInChildren<NetworkRecoveryView>(true);
            view.Message.fontSize = 26f;
            view.Message.rectTransform.anchoredPosition = new Vector2(10f, 48f);
            overlay.Configure(owner);
            Assert.That(root.GetComponentsInChildren<Canvas>(true).Length, Is.EqualTo(1));
            Assert.That(view.Message.fontSize, Is.EqualTo(26f));
            Assert.That(view.Message.rectTransform.anchoredPosition, Is.EqualTo(new Vector2(10f, 48f)));
            Render(ClientReconnectStatus.Connecting, true, "正在连接");
            Assert.That(input.enabled, Is.False);
            Invoke("OnDisable");
            Assert.That(input.enabled, Is.True);
            Assert.That(view.Canvas.enabled, Is.False);
            Invoke("OnDestroy");
            Assert.That(view == null, Is.True);
        }

        [Test]
        public void BoundButtons_InvokeRealReconnectController()
        {
            var wire = new FakeTransport(TransportRole.Client);
            using (var runtime = new GameNetworkRuntime(() => wire))
            {
                var manager = CreateManager(null, wire, Context());
                var controller = new ClientReconnectController(runtime, manager);
                typeof(UnityNetworkBootstrap).GetProperty("Reconnect").SetValue(owner, controller);
                var view = root.GetComponentInChildren<NetworkRecoveryView>(true);
                view.Cancel.onClick.Invoke();
                Invoke("Update");
                Assert.That(controller.Status, Is.EqualTo(ClientReconnectStatus.Cancelled));
                Assert.That(view.Message.text, Does.Contain("已取消"));
                view.Retry.onClick.Invoke();
                Invoke("Update");
                Assert.That(controller.Status, Is.EqualTo(ClientReconnectStatus.Failed));
                Assert.That(view.Message.text, Does.Contain("没有可恢复的会话"));
                view.JoinAsNew.onClick.Invoke();
                Invoke("Update");
                Assert.That(controller.Status, Is.EqualTo(ClientReconnectStatus.Waiting));
                Assert.That(view.Cancel.interactable, Is.True);
            }
        }

        [Test]
        public void Generator_DoesNotOverwritePrefabOrSharedFont()
        {
            byte[] prefab = File.ReadAllBytes(NetworkRecoveryAssetBuilder.PrefabPath);
            byte[] font = File.ReadAllBytes(NetworkLobbyAssetBuilder.FontPath);
            NetworkRecoveryAssetBuilder.Create();
            Assert.That(File.ReadAllBytes(NetworkRecoveryAssetBuilder.PrefabPath), Is.EqualTo(prefab));
            Assert.That(File.ReadAllBytes(NetworkLobbyAssetBuilder.FontPath), Is.EqualTo(font));
        }

        private void Render(ClientReconnectStatus status, bool active, string message)
        {
            Invoke("RenderState", status, active, message);
        }

        private void Invoke(string method, params object[] arguments)
        {
            typeof(NetworkRecoveryOverlay).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(overlay, arguments);
        }
    }
}
