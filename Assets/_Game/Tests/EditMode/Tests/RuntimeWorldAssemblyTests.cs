using System.Linq;
using System.Reflection;
using AFPS.Bootstrap;
using AFPS.NetCode.Runtime;
using AFPS.Simulation.Characters;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AFPS.Tests.EditMode
{
    public sealed class RuntimeWorldAssemblyTests
    {
        private Scene scene;
        private Scene previousActiveScene;
        private string temporaryScenePath;
        private UnityNetworkMovementController movement;
        private TextAsset damagedSnapshot;

        [SetUp]
        public void OpenSavedScene()
        {
            // 从临时副本附加加载，不能保存或污染用户当前打开的场景。
            previousActiveScene = SceneManager.GetActiveScene();
            temporaryScenePath = AssetDatabase.GenerateUniqueAssetPath("Assets/__AFPSRuntimeWorldTest.unity");
            AssetDatabase.CopyAsset(
                "Assets/_Game/Scenes/MyLevel/SampleScene.unity",
                temporaryScenePath);
            scene = EditorSceneManager.OpenScene(temporaryScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(previousActiveScene);
            movement = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<UnityNetworkMovementController>(true))
                .Single();
        }

        [TearDown]
        public void ClosePreview()
        {
            if (scene.IsValid())
            {
                EditorSceneManager.CloseScene(scene, true);
            }
            AssetDatabase.DeleteAsset(temporaryScenePath);
            if (damagedSnapshot != null)
            {
                Object.DestroyImmediate(damagedSnapshot);
            }
        }

        [Test]
        public void SavedScene_LoadsBakedWorldWithSceneCollisionMask()
        {
            Assert.That(movement.TryPrepareSimulation(out string error), Is.True, error);
            var field = typeof(UnityNetworkMovementController).GetField(
                "preparedWorld", BindingFlags.Instance | BindingFlags.NonPublic);
            var world = (SimulationWorldContext)field.GetValue(movement);
            Assert.That(world.Identity.WorldHash, Is.EqualTo(11671102255531124119UL));
            Assert.That(world.CollisionLayerMask, Is.EqualTo(1 << 8));
            Assert.That(world.Identity.AlgorithmVersion, Is.EqualTo(20));
            // 保存场景的坡度上限为 80.9°，不能套用默认 50° 移动配置加 Layer 8 的指纹。
            Assert.That(world.Identity.ConfigurationHash, Is.EqualTo(12352265310144270905UL));
            Assert.That(world.TickDeltaTime.Raw, Is.EqualTo(1311));
            Assert.That(world.CollisionWorld, Is.Not.Null);
            Assert.That(movement.CanEnterGame, Is.False);
        }

        [TestCase(NetworkLaunchMode.Host)]
        [TestCase(NetworkLaunchMode.Client)]
        [TestCase(NetworkLaunchMode.DedicatedServer)]
        public void MissingSnapshot_BlocksTransportStartupForEveryMode(NetworkLaunchMode mode)
        {
            SetSnapshot(null);
            var bootstrap = movement.GetComponent<UnityNetworkBootstrap>();
            var options = new NetworkLaunchOptions(mode, "127.0.0.1", 47996, 2);
            Assert.That(bootstrap.TryStartNetwork(options, out string error), Is.False);
            Assert.That(error, Does.Contain("Missing baked simulation world"));
            Assert.That(bootstrap.Runtime, Is.Null);
            Assert.That(movement.SessionManager, Is.Null);
            Assert.That(movement.CanEnterGame, Is.False);
        }

        [Test]
        public void DamagedSnapshot_FailsWithoutFallback()
        {
            damagedSnapshot = new TextAsset("invalid world snapshot");
            SetSnapshot(damagedSnapshot);
            Assert.That(movement.TryPrepareSimulation(out string error), Is.False);
            Assert.That(error, Does.StartWith("Invalid simulation world:"));
            Assert.That(movement.CanEnterGame, Is.False);
        }

        private void SetSnapshot(TextAsset snapshot)
        {
            var serialized = new SerializedObject(movement);
            serialized.FindProperty("fixedWorldSnapshot").objectReferenceValue = snapshot;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
