using System.IO;
using System.Reflection;
using AFPS.Bootstrap;
using AFPS.Editor.UI;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace AFPS.Tests.EditMode
{
    public sealed class NetworkLobbyViewTests
    {
        [Test]
        public void ShippedPrefab_HasPersistentFontAndLocalReferences()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<NetworkLobbyView>(NetworkLobbyAssetBuilder.PrefabPath);
            Assert.That(prefab, Is.Not.Null);
            Assert.That(prefab.TryValidate(out string error), Is.True, error);
            Assert.That(Resources.Load<NetworkLobbyView>(NetworkLobbyView.ResourcePath), Is.EqualTo(prefab));
            foreach (var text in prefab.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                Assert.That(AssetDatabase.GetAssetPath(text.font), Is.EqualTo(NetworkLobbyAssetBuilder.FontPath));
                Assert.That(text.font.sourceFontFile, Is.EqualTo(ChineseUiFont.Source));
            }

            var bindings = new SerializedObject(prefab);
            var property = bindings.GetIterator();
            while (property.NextVisible(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference
                    || property.objectReferenceValue == null
                    || property.name == "m_Script")
                {
                    continue;
                }

                var component = property.objectReferenceValue as Component;
                Assert.That(component, Is.Not.Null, property.propertyPath);
                Assert.That(component.transform.IsChildOf(prefab.transform), Is.True, property.propertyPath);
            }

            Assert.That(prefab.AddressInput.characterLimit, Is.EqualTo(64));
            Assert.That(prefab.PortInput.contentType, Is.EqualTo(TMP_InputField.ContentType.IntegerNumber));
            Assert.That(prefab.EnterButton.gameObject.activeSelf, Is.False);
            Assert.That(prefab.CopyRoomCodeButton.interactable, Is.False);
        }

        [Test]
        public void RuntimeBinding_PreservesAuthoredStyleAndWiresPagination()
        {
            var root = new GameObject("Lobby binding test");
            NetworkLobbyView view = null;
            try
            {
                var owner = root.AddComponent<UnityNetworkBootstrap>();
                var menu = root.AddComponent<NetworkLobbyMenu>();
                Set(menu, "bootstrap", owner);
                typeof(NetworkLobbyMenu).GetMethod("BuildMenu", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(menu, null);
                view = ((GameObject)Get(menu, "canvasRoot")).GetComponent<NetworkLobbyView>();
                var source = Resources.Load<NetworkLobbyView>(NetworkLobbyView.ResourcePath);
                Assert.That(view.MainPanel.anchoredPosition, Is.EqualTo(source.MainPanel.anchoredPosition));
                Assert.That(view.StatusText.fontSize, Is.EqualTo(source.StatusText.fontSize));
                Assert.That(view.HostButton.image.color, Is.EqualTo(source.HostButton.image.color));
                Assert.That(view.AddressInput.text, Is.EqualTo(owner.DefaultServerAddress));
                Assert.That(view.PortInput.text, Is.EqualTo(owner.DefaultPort.ToString()));

                view.NextRoomsButton.onClick.Invoke();
                Assert.That(Get(menu, "roomPage"), Is.EqualTo(1));
                view.PreviousRoomsButton.onClick.Invoke();
                Assert.That(Get(menu, "roomPage"), Is.EqualTo(0));
                Set(menu, "roomPage", 3);
                view.RefreshRoomsButton.onClick.Invoke();
                Assert.That(Get(menu, "roomPage"), Is.EqualTo(0));

                // 修改实例的排版后重复刷新数据，不能重建或覆盖美术编辑结果。
                view.MainPanel.anchoredPosition = new Vector2(-250f, 15f);
                view.StatusText.fontSize = 27f;
                view.RefreshRoomsButton.onClick.Invoke();
                Assert.That(view.MainPanel.anchoredPosition, Is.EqualTo(new Vector2(-250f, 15f)));
                Assert.That(view.StatusText.fontSize, Is.EqualTo(27f));
            }
            finally
            {
                Object.DestroyImmediate(root);
                if (view != null)
                {
                    Object.DestroyImmediate(view.gameObject);
                }
            }
        }

        [Test]
        public void MissingBinding_ReportsActionableError()
        {
            var instance = Object.Instantiate(Resources.Load<NetworkLobbyView>(NetworkLobbyView.ResourcePath));
            try
            {
                var serialized = new SerializedObject(instance);
                serialized.FindProperty("joinButton").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(instance.TryValidate(out string error), Is.False);
                Assert.That(error, Does.Contain("joinButton"));
            }
            finally
            {
                Object.DestroyImmediate(instance.gameObject);
            }
        }

        [Test]
        public void Generator_DoesNotOverwriteExistingPrefabOrFont()
        {
            byte[] prefab = File.ReadAllBytes(NetworkLobbyAssetBuilder.PrefabPath);
            byte[] font = File.ReadAllBytes(NetworkLobbyAssetBuilder.FontPath);
            NetworkLobbyAssetBuilder.Create();
            Assert.That(File.ReadAllBytes(NetworkLobbyAssetBuilder.PrefabPath), Is.EqualTo(prefab));
            Assert.That(File.ReadAllBytes(NetworkLobbyAssetBuilder.FontPath), Is.EqualTo(font));
        }

        private static object Get(NetworkLobbyMenu menu, string name)
        {
            return typeof(NetworkLobbyMenu).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(menu);
        }

        private static void Set(NetworkLobbyMenu menu, string name, object value)
        {
            typeof(NetworkLobbyMenu).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(menu, value);
        }
    }
}
