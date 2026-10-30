using System;
using System.Collections.Generic;
using System.IO;
using AFPS.Presentation.Characters;
using AFPS.Presentation.Weapons;
using UnityEditor;
using UnityEngine;

namespace AFPS.Editor.Art
{
    /// <summary>用Unity基础网格生成AFPS自有人形开发模型，不读取第三方资产。</summary>
    public static class RemoteCharacterAssetBuilder
    {
        public const string PrefabPath = "Assets/_Game/Art/Resources/Characters/RemotePlayer.prefab";
        private const string Root = "Assets/_Game/Art/Resources/Characters";

        [MenuItem("AFPS/Characters/Create Development Character")]
        public static void Create()
        {
            // 已存在时拒绝覆盖美术或Inspector改动；自定义角色可直接通过Registry的Prefab字段接入。
            if (File.Exists(PrefabPath))
            {
                throw new InvalidOperationException("角色Prefab已存在，生成器不会覆盖人工修改：" + PrefabPath);
            }
            Directory.CreateDirectory(Root);
            AssetDatabase.Refresh();
            Material armor = MaterialAsset("Armor", new Color(0.8f, 0.85f, 0.9f));
            Material dark = MaterialAsset("Joints", new Color(0.08f, 0.11f, 0.15f));
            Material visor = MaterialAsset("Visor", new Color(0.05f, 0.2f, 0.28f));
            GameObject actor = new GameObject("RemotePlayer");
            try
            {
                Transform visual = Node("CharacterVisual", actor.transform, Vector3.zero);
                Transform hips = Node("Hips", visual, Vector3.up * 0.92f);
                Part("Pelvis", hips, new Vector3(0f, 0f, 0f), new Vector3(0.35f, 0.18f, 0.25f), dark);
                Transform upper = Node("UpperBody", hips, Vector3.up * 0.08f);
                Part("Armor", upper, new Vector3(0f, 0.27f, 0f), new Vector3(0.46f, 0.48f, 0.28f), armor);
                Part("Helmet", upper, new Vector3(0f, 0.64f, 0f), new Vector3(0.34f, 0.34f, 0.32f), armor);
                Part("Visor", upper, new Vector3(0f, 0.65f, 0.169f), new Vector3(0.28f, 0.12f, 0.025f), visor);
                Arm("LeftArm", upper, -1f, armor);
                Arm("RightArm", upper, 1f, armor);
                Transform weapon = Node("DevelopmentRifle", upper, new Vector3(0.12f, 0.2f, 0.35f));
                Part("Receiver", weapon, Vector3.zero, new Vector3(0.12f, 0.13f, 0.35f), dark);
                Part("Barrel", weapon, Vector3.forward * 0.26f, new Vector3(0.055f, 0.055f, 0.24f), dark);
                AddWeapons(upper, weapon, dark);
                Transform leftThigh = Leg("Left", hips, -0.13f, armor, dark, out Transform leftKnee, out Transform leftFoot);
                Transform rightThigh = Leg("Right", hips, 0.13f, armor, dark, out Transform rightKnee, out Transform rightFoot);
                var pose = visual.gameObject.AddComponent<RemoteCharacterPoseView>();
                pose.Configure(hips, upper, leftThigh, rightThigh, leftKnee, rightKnee, leftFoot, rightFoot);
                var view = actor.AddComponent<RemotePlayerView>();
                var armorRenderers = new List<Renderer>();
                foreach (Renderer renderer in actor.GetComponentsInChildren<Renderer>())
                {
                    if (renderer.sharedMaterial == armor)
                    {
                        armorRenderers.Add(renderer);
                    }
                }
                // 仅装甲参与存活/死亡染色，保留护目镜、关节与枪械的深色轮廓。
                var serializedView = new SerializedObject(view);
                SerializedProperty renderers = serializedView.FindProperty("renderers");
                renderers.arraySize = armorRenderers.Count;
                for (int i = 0; i < armorRenderers.Count; i++)
                {
                    renderers.GetArrayElementAtIndex(i).objectReferenceValue = armorRenderers[i];
                }
                serializedView.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(actor, PrefabPath);
                AssetDatabase.SaveAssets();
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(actor);
            }
        }

        private static Material MaterialAsset(string name, Color color)
        {
            string assetPath = Root + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            if (material != null)
            {
                return material;
            }
            material = new Material(Shader.Find("Standard")) { color = color };
            AssetDatabase.CreateAsset(material, assetPath);
            return material;
        }

        [MenuItem("AFPS/Characters/Add Development Weapon Presentation")]
        public static void UpgradeWeapons()
        {
            GameObject actor = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                if (actor.GetComponentInChildren<RemoteWeaponView>(true) != null)
                {
                    return;
                }
                Transform upper = actor.transform.Find("CharacterVisual/Hips/UpperBody");
                Transform rifle = upper != null ? upper.Find("DevelopmentRifle") : null;
                if (rifle == null)
                {
                    throw new InvalidOperationException("只支持已知自有开发模型；自定义角色请手动配置武器表现节点。");
                }
                AddWeapons(upper, rifle, MaterialAsset("Joints", new Color(0.08f, 0.11f, 0.15f)));
                // 原Prefab根和全部既有节点不变，沿用GUID，不重建角色或覆盖材质/姿态人工修改。
                PrefabUtility.SaveAsPrefabAsset(actor, PrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(actor);
            }
        }

        private static void AddWeapons(Transform upper, Transform rifle, Material material)
        {
            Transform launcher = Node("DevelopmentLauncher", upper, new Vector3(0.12f, 0.25f, 0.35f));
            Part("LauncherTube", launcher, Vector3.zero, new Vector3(0.2f, 0.2f, 0.75f), material);
            Part("LauncherSight", launcher, new Vector3(0f, 0.18f, 0.12f), new Vector3(0.06f, 0.18f, 0.06f), material);
            upper.gameObject.AddComponent<RemoteWeaponView>().Configure(rifle, launcher, upper.Find("LeftArm"));
            launcher.gameObject.SetActive(false);
        }

        private static Transform Node(string name, Transform parent, Vector3 position)
        {
            var node = new GameObject(name).transform;
            node.SetParent(parent, false);
            node.localPosition = position;
            return node;
        }

        private static void Part(string name, Transform parent, Vector3 position, Vector3 scale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            // 生成时移除Collider，发布的Prefab没有物理组件，不能污染客户端查询或权威世界。
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
        }

        private static void Arm(string name, Transform upper, float side, Material material)
        {
            Transform arm = Node(name, upper, new Vector3(side * 0.3f, 0.37f, 0f));
            Part("UpperArm", arm, Vector3.down * 0.14f, new Vector3(0.14f, 0.28f, 0.15f), material);
            Part("Forearm", arm, new Vector3(-side * 0.04f, -0.27f, 0.14f), new Vector3(0.13f, 0.13f, 0.32f), material);
        }

        private static Transform Leg(
            string side,
            Transform hips,
            float offsetX,
            Material armor,
            Material dark,
            out Transform knee,
            out Transform foot)
        {
            Transform thigh = Node(side + "Thigh", hips, Vector3.right * offsetX);
            Part("Thigh", thigh, Vector3.down * 0.215f, new Vector3(0.17f, 0.43f, 0.18f), armor);
            knee = Node(side + "Knee", thigh, Vector3.down * 0.43f);
            Part("Shin", knee, Vector3.down * 0.215f, new Vector3(0.15f, 0.43f, 0.16f), armor);
            foot = Node(side + "Foot", knee, Vector3.down * 0.43f);
            Part("Boot", foot, Vector3.forward * 0.06f, new Vector3(0.19f, 0.12f, 0.3f), dark);
            return thigh;
        }
    }
}
