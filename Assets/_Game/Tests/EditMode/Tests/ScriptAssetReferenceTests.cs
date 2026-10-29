using System;
using AFPS.Bootstrap;
using AFPS.Presentation.Characters;
using AFPS.Presentation.Weapons;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AFPS.Tests.EditMode
{
    public sealed class ScriptAssetReferenceTests
    {
        [Test]
        public void RenamedComponentsKeepOriginalScriptGuidAndResolveNewType()
        {
            // GUID取自改名前的资产；只改路径和类型名，不能靠重挂组件掩盖引用丢失。
            Check<AuthoritativeMovementTraceRecorder>(
                "Bootstrap/AuthoritativeMovementTraceRecorder.cs",
                "a1bec26a9b09424e871017e9487bfaea");
            Check<GameplayHud>(
                "Bootstrap/GameplayHud.cs",
                "55c5edbd111d4de2a7e68cf52f66f3ab");
            Check<NetworkLobbyMenu>(
                "Bootstrap/NetworkLobbyMenu.cs",
                "3e214d1a03cc3b64490943c6b3d0c299");
            Check<NetworkGameplaySmokeTestDriver>(
                "Bootstrap/NetworkGameplaySmokeTestDriver.cs",
                "613d461c9e932f249a32d6e301aadbf8");
            Check<UnityNetworkGameplayController>(
                "Bootstrap/UnityNetworkGameplayController.cs",
                "96fbe1cb74de0244aabfa045b41938d9");
            Check<LocalPlayerView>(
                "Presentation/Characters/LocalPlayerView.cs",
                "8087823f3bbae2746827d0dc73a2f70c");
            Check<RemotePlayerViewRegistry>(
                "Presentation/Characters/RemotePlayerViewRegistry.cs",
                "6bddd25003585af4a94ab4bca46fe381");
            Check<ProjectileVisualProxy>(
                "Presentation/Weapons/ProjectileVisualProxy.cs",
                "4616bba483252544fa4d9b10aceabfa4");
            Check<ShotPresentationController>(
                "Presentation/Weapons/ShotPresentationController.cs",
                "e933305709764b00956c3dc0c04799d8");
        }

        private static void Check<T>(string relativePath, string expectedGuid) where T : MonoBehaviour
        {
            string path = "Assets/_Game/Scripts/" + relativePath;
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(expectedGuid), path);
            var script = AssetDatabase.LoadAssetAtPath<MonoScript>(path);
            Assert.That(script, Is.Not.Null, path);
            Assert.That(script.GetClass(), Is.EqualTo(typeof(T)), path);
        }

        [Test]
        public void GameScenesAndPrefabsHaveNoMissingScripts()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets/_Game" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var existing = SceneManager.GetSceneByPath(path);
                bool loadedHere = !existing.IsValid() || !existing.isLoaded;
                var scene = loadedHere ? EditorSceneManager.OpenScene(path, OpenSceneMode.Additive) : existing;
                try
                {
                    foreach (var root in scene.GetRootGameObjects())
                    {
                        CheckHierarchy(root, path);
                    }
                }
                finally
                {
                    if (loadedHere)
                    {
                        EditorSceneManager.CloseScene(scene, true);
                    }
                }
            }
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    CheckHierarchy(root, path);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(root);
                }
            }
        }

        private static void CheckHierarchy(GameObject root, string assetPath)
        {
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
            {
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject),
                    Is.Zero, assetPath + ": " + node.name);
            }
        }
    }
}
