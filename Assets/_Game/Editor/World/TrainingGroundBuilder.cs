using System;
using System.Collections.Generic;
using System.IO;
using AFPS.Bootstrap;
using AFPS.Bootstrap.World;
using AFPS.Simulation.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AFPS.Editor.World
{
    /// <summary>独立训练关卡：坐标/尺寸单位为米，角色原点在脚底；不实例化第三方Prefab。</summary>
    public static class TrainingGroundBuilder
    {
        public const string ScenePath = "Assets/_Game/Scenes/TrainingGround.unity";
        public const string WorldPath = "Assets/_Game/Worlds/TrainingGround.bytes";
        private const string MaterialFolder = "Assets/_Game/Art/Training";
        private static readonly Vector3 Spawn = new Vector3(-8f, 0f, -26f);
        private static readonly FixedSurface Walkable = FixedSurface.Solid | FixedSurface.Ground;
        private static readonly FixedSurface Wall = FixedSurface.Solid | FixedSurface.Wall;
        private static readonly FixedSurface Anchor = Wall | FixedSurface.Grapple;

        [MenuItem("AFPS/Testing/Open Training Ground")]
        public static void Open()
        {
            // 只有用户主动切换场景时才走Unity标准保存提示；批处理生成不碰当前未保存场景。
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                EditorSceneManager.OpenScene(ScenePath);
            }
        }

        /// <summary>首次生成专用场景；已存在时拒绝覆盖，避免擦除后续人工关卡编辑。</summary>
        public static void Create()
        {
            if (File.Exists(ScenePath))
            {
                throw new InvalidOperationException("训练场已存在，请直接打开；生成器不会覆盖人工编辑。");
            }
            if (!AssetDatabase.CopyAsset("Assets/_Game/Scenes/MyLevel/SampleScene.unity", ScenePath))
            {
                throw new IOException("无法复制AFPS场景装配。");
            }
            var active = SceneManager.GetActiveScene();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                var keep = new HashSet<string>
                {
                    "NetworkBootstrap", "Directional Light", "Main Camera", "LocalPlayer", "TickRunner"
                };
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (!keep.Contains(root.name))
                    {
                        UnityEngine.Object.DestroyImmediate(root);
                    }
                }

                var floor = Material("Floor", new Color(0.13f, 0.18f, 0.23f));
                var concrete = Material("Concrete", new Color(0.32f, 0.39f, 0.46f));
                var dark = Material("Dark", new Color(0.055f, 0.085f, 0.12f));
                var cyan = Material("Cyan", new Color(0.1f, 0.8f, 0.9f));
                var orange = Material("Orange", new Color(1f, 0.48f, 0.12f));
                var white = Material("White", new Color(0.9f, 0.94f, 0.96f));
                var red = Material("Target", new Color(0.9f, 0.18f, 0.14f));
                var geometry = new GameObject("Training Geometry").transform;
                Box(geometry, "Foundation", new Vector3(0f, -0.5f, 6f), new Vector3(96f, 1f, 84f), floor, Walkable);
                Box(geometry, "West Boundary", new Vector3(-48f, 2.5f, 6f), new Vector3(1f, 5f, 84f), concrete, Wall);
                Box(geometry, "East Boundary", new Vector3(48f, 2.5f, 6f), new Vector3(1f, 5f, 84f), concrete, Wall);
                Box(geometry, "North Boundary", new Vector3(0f, 2.5f, 48f), new Vector3(96f, 5f, 1f), concrete, Wall);
                Box(geometry, "South Boundary", new Vector3(0f, 2.5f, -36f), new Vector3(96f, 5f, 1f), concrete, Wall);

                var signs = new GameObject("Training Signs").transform;
                Sign(signs, "AFPS / TRAINING GROUND", "SOLO PRACTICE: local controls | HOST + CLIENT: combat / respawn",
                    new Vector3(4f, 2.8f, -31f), dark, white, 180f);
                Sign(signs, "01 / FIRING RANGE", "LMB fire / hold auto   R reload   Q switch weapon\nStatic targets: impacts only | Duel: damage / kills / 3s respawn",
                    new Vector3(-30f, 3.5f, 25f), dark, white);
                Sign(signs, "02 / CROUCH + SLIDE", "Hold CTRL to crouch | SHIFT + W then CTRL to slide\n1.45m clearance: release CTRL inside to test blocked standing",
                    new Vector3(-29f, 3.2f, -6f), dark, white);
                Sign(signs, "03 / SPRINT + DASH", "SHIFT + W sprint / stamina   ALT dash\nSPACE jump / double jump | Walk off raised pads: coyote jump",
                    new Vector3(0f, 3f, 14f), dark, white);
                Sign(signs, "04 / WALL RUN + JUMP", "Jump near wall, look ALONG wall, hold SHIFT + W\nSPACE wall jump   E wall bounce | Land to reset wall-run budget",
                    new Vector3(29f, 3.8f, -5f), dark, white);
                Sign(signs, "05 / LADDER", "Face ladder: W / S climb | Release: hang\nSPACE jump off | Climb onto deck / descend to floor",
                    new Vector3(34f, 6.2f, 34f), dark, white);
                Sign(signs, "06 / GRAPPLE", "Aim at CYAN anchors, hold RMB / F | Release to detach\nJump + attach + look sideways: reel / swing | ALT cancels",
                    new Vector3(0f, 12f, 39f), dark, white);

                BuildRange(geometry, concrete, dark, red, white, orange);
                BuildStance(geometry, concrete, orange);
                BuildMovement(geometry, concrete, cyan, orange);
                BuildWalls(geometry, concrete, cyan);
                BuildLadder(geometry, concrete, orange);
                BuildGrapple(geometry, concrete, cyan);
                // 地面色带无碰撞；不制造很薄的模拟盒，也不会挡住射击/钩索查询。
                for (int z = -28; z <= 10; z += 4)
                {
                    Box(geometry, "Sprint Lane", new Vector3(0f, 0.012f, z), new Vector3(0.16f, 0.02f, 2f), cyan);
                }
                for (int x = -8; x <= 10; x += 3)
                {
                    Box(geometry, "Spawn Mark", new Vector3(x, 0.012f, -26f), new Vector3(1.2f, 0.02f, 1.2f), orange);
                }

                var controller = Find<UnityNetworkMovementController>(scene);
                var serialized = new SerializedObject(controller);
                serialized.FindProperty("serverSpawnPosition").vector3Value = Spawn;
                serialized.FindProperty("serverSpawnSpacing").floatValue = 2.5f;
                serialized.FindProperty("collisionLayers").intValue = 1 << 8;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var network = new SerializedObject(Find<UnityNetworkBootstrap>(scene));
                // 八人出生线完全落在空旷大厅；训练场不改变全局32人服务器能力。
                network.FindProperty("maxConnections").intValue = 8;
                network.FindProperty("showConnectionMenu").boolValue = true;
                network.ApplyModifiedPropertiesWithoutUndo();
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.name == "LocalPlayer")
                    {
                        root.transform.position = Spawn;
                    }
                }
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
                RenderSettings.ambientLight = new Color(0.65f, 0.72f, 0.8f);
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveScene(scene);
                FixedWorldBakeMenu.BakeActiveScene();
                serialized.Update();
                serialized.FindProperty("fixedWorldSnapshot").objectReferenceValue = AssetDatabase.LoadAssetAtPath<TextAsset>(WorldPath);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.SaveScene(scene);
                // 写入快照引用后再烘焙一次，让manifest对应最终保存的场景依赖。
                FixedWorldBakeMenu.BakeActiveScene();
                AssetDatabase.SaveAssets();
                var builds = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
                if (!builds.Exists(item => item.path == ScenePath))
                {
                    builds.Add(new EditorBuildSettingsScene(ScenePath, true));
                    EditorBuildSettings.scenes = builds.ToArray();
                }
                Debug.Log("AFPS_TRAINING_CREATED " + ScenePath);
            }
            finally
            {
                EditorSceneManager.CloseScene(scene, true);
                if (active.IsValid() && active.isLoaded)
                {
                    SceneManager.SetActiveScene(active);
                }
            }
        }

        private static void BuildRange(Transform parent, Material concrete, Material dark, Material red, Material white, Material orange)
        {
            Box(parent, "Range Backstop", new Vector3(-30f, 2.5f, 27f), new Vector3(24f, 5f, 1f), concrete, Wall);
            for (int i = 0; i < 3; i++)
            {
                float x = -38f + i * 8f;
                float z = 7f + i * 7f;
                Box(parent, "Target Stand", new Vector3(x, 0.65f, z), new Vector3(0.2f, 1.3f, 0.2f), dark, FixedSurface.Solid);
                Box(parent, "Static Target", new Vector3(x, 1.8f, z), new Vector3(1.4f, 1.4f, 0.2f), white, FixedSurface.Solid);
                Box(parent, "Target Ring", new Vector3(x, 1.8f, z - 0.11f), new Vector3(0.9f, 0.9f, 0.02f), red);
                Box(parent, "Target Center", new Vector3(x, 1.8f, z - 0.13f), new Vector3(0.3f, 0.3f, 0.02f), dark);
                Box(parent, "Firing Line", new Vector3(x, 0.012f, -2f), new Vector3(5f, 0.02f, 0.12f), orange);
            }
            Box(parent, "Duel Cover A", new Vector3(-39f, 0.65f, 0f), new Vector3(3f, 1.3f, 1f), concrete, Walkable);
            Box(parent, "Duel Cover B", new Vector3(-21f, 0.65f, 6f), new Vector3(3f, 1.3f, 1f), concrete, Walkable);
        }

        private static void BuildStance(Transform parent, Material concrete, Material orange)
        {
            Box(parent, "Low Tunnel Roof", new Vector3(-29f, 1.7f, -12f), new Vector3(6f, 0.5f, 10f), concrete, Walkable);
            Box(parent, "Tunnel Left", new Vector3(-32.25f, 0.85f, -12f), new Vector3(0.5f, 1.7f, 10f), concrete, Wall);
            Box(parent, "Tunnel Right", new Vector3(-25.75f, 0.85f, -12f), new Vector3(0.5f, 1.7f, 10f), concrete, Wall);
            Box(parent, "Slide Approach", new Vector3(-29f, 0.012f, -23f), new Vector3(0.18f, 0.02f, 10f), orange);
        }

        private static void BuildMovement(Transform parent, Material concrete, Material cyan, Material orange)
        {
            for (int i = 0; i < 4; i++)
            {
                Box(parent, "Step " + i, new Vector3(9f, (i + 1) * 0.125f, 2f + i),
                    new Vector3(3f, (i + 1) * 0.25f, 1f), concrete, Walkable);
            }
            for (int i = 0; i < 3; i++)
            {
                Box(parent, "Jump Pad " + i, new Vector3(-6f + i * 6f, 0.75f, 21f),
                    new Vector3(3f, 1.5f, 4f), concrete, Walkable);
                Box(parent, "Pad Edge", new Vector3(-6f + i * 6f, 1.512f, 21f), new Vector3(2.8f, 0.02f, 0.15f), orange);
            }
            Box(parent, "Jump Landing", new Vector3(0f, 1.5f, 29f), new Vector3(5f, 3f, 4f), cyan, Walkable);
        }

        private static void BuildWalls(Transform parent, Material concrete, Material cyan)
        {
            for (int i = 0; i < 2; i++)
            {
                float x = 25f + i * 7f;
                Box(parent, "Wall Run " + i, new Vector3(x, 3f, 7f), new Vector3(0.6f, 6f, 24f), concrete, Wall);
                Box(parent, "Wall Run Stripe", new Vector3(x, 1.5f, 7f), new Vector3(0.63f, 0.15f, 24f), cyan);
            }
            Box(parent, "Bounce Wall", new Vector3(38f, 3f, 19f), new Vector3(10f, 6f, 0.6f), concrete, Wall);
        }

        private static void BuildLadder(Transform parent, Material concrete, Material orange)
        {
            // 平台前沿与梯面留0.8米，超过胶囊半径，避免攀爬时头部先撞平台底面。
            Box(parent, "Ladder Deck", new Vector3(33f, 3.85f, 31.1f), new Vector3(8f, 0.3f, 7f), concrete, Walkable);
            var ladder = VerticalLadderMenu.Create(new Vector3(32f, 0f, 26.8f));
            ladder.transform.SetParent(parent, true);
            foreach (var renderer in ladder.GetComponentsInChildren<Renderer>())
            {
                renderer.sharedMaterial = orange;
            }
            Box(parent, "Deck Back Rail", new Vector3(33f, 4.5f, 34.4f), new Vector3(8f, 1f, 0.2f), concrete, Wall);
        }

        private static void BuildGrapple(Transform parent, Material concrete, Material cyan)
        {
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -10f : 10f;
                Box(parent, "Grapple Pillar", new Vector3(x, 5f, 36f), new Vector3(1f, 10f, 1f), concrete, Wall);
                Box(parent, "Grapple Anchor", new Vector3(x, 10f, 36f), new Vector3(2.5f, 1f, 2.5f), cyan, Anchor);
            }
            Box(parent, "Grapple Beam", new Vector3(0f, 10f, 36f), new Vector3(18f, 0.6f, 0.8f), cyan, Anchor);
        }

        private static Material Material(string name, Color color)
        {
            Directory.CreateDirectory(MaterialFolder);
            string path = MaterialFolder + "/" + name + ".mat";
            var result = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (result == null)
            {
                result = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(result, path);
            }
            result.color = color;
            result.SetFloat("_Glossiness", 0.15f);
            EditorUtility.SetDirty(result);
            return result;
        }

        private static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size,
            Material material, FixedSurface surface = FixedSurface.None)
        {
            var result = GameObject.CreatePrimitive(PrimitiveType.Cube);
            result.name = name;
            result.transform.SetParent(parent, false);
            result.transform.localPosition = center;
            result.transform.localScale = size;
            result.layer = 8;
            result.isStatic = true;
            result.GetComponent<Renderer>().sharedMaterial = material;
            if (surface == FixedSurface.None)
            {
                UnityEngine.Object.DestroyImmediate(result.GetComponent<Collider>());
            }
            else
            {
                result.AddComponent<WorldSurfaceAuthoring>().Surfaces = surface;
            }
            return result;
        }

        private static void Sign(Transform parent, string title, string instruction, Vector3 center,
            Material backing, Material textMaterial, float yaw = 0f)
        {
            var root = new GameObject(title).transform;
            root.SetParent(parent, false);
            root.position = center;
            root.rotation = Quaternion.Euler(0f, yaw, 0f);
            Box(root, "Sign Backing", Vector3.zero, new Vector3(14f, 2.1f, 0.1f), backing);
            var label = new GameObject("Instructions").AddComponent<TextMesh>();
            label.transform.SetParent(root, false);
            label.transform.localPosition = new Vector3(0f, 0f, -0.07f);
            label.text = title + "\n\n" + instruction;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 64;
            label.characterSize = 0.045f;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.GetComponent<MeshRenderer>().sharedMaterial = label.font.material;
            label.color = textMaterial.color;
        }

        private static T Find<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var component = root.GetComponentInChildren<T>(true);
                if (component != null)
                {
                    return component;
                }
            }
            throw new InvalidOperationException("场景缺少AFPS组件：" + typeof(T).Name);
        }
    }
}
