using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AFPS.World.Editor
{
    /// <summary>仅处理自有副本，绝不修改或引用第三方资源的 GUID。</summary>
    public static class CombatPresentationAssetBuilder
    {
        private const string Root = "Assets/_Game/Art/Resources/Combat/";

        [MenuItem("AFPS/Combat/Rebuild Presentation Assets")]
        public static void Rebuild()
        {
            BuildWeapon("Rifle", "rifle", 0.1f);
            BuildWeapon("Launcher", "rpg", 0.3f);
            string materialPath = Root + "Weapon.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, materialPath);
            }
            material.color = new Color(0.23f, 0.27f, 0.31f);
            material.SetFloat("_Metallic", 0.55f);
            material.SetFloat("_Glossiness", 0.4f);
            EditorUtility.SetDirty(material);
            string skinPath = Root + "Skin.mat";
            Material skin = AssetDatabase.LoadAssetAtPath<Material>(skinPath);
            if (skin == null)
            {
                skin = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(skin, skinPath);
            }
            skin.color = new Color(0.64f, 0.41f, 0.28f);
            skin.SetFloat("_Glossiness", 0.2f);
            EditorUtility.SetDirty(skin);
            string effectsPath = Root + "Effects.mat";
            Material effects = AssetDatabase.LoadAssetAtPath<Material>(effectsPath);
            if (effects == null)
            {
                effects = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(effects, effectsPath);
            }
            // 显式资源引用使 Player 构建保留特效 Shader，不依赖运行时 Shader.Find 恰好成功。
            EditorUtility.SetDirty(effects);
            AssetDatabase.SaveAssets();
        }

        private static void BuildWeapon(string name, string prefix, float fireSeconds)
        {
            string modelPath = Root + name + ".fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
            if (importer == null)
            {
                throw new InvalidOperationException("Missing owned combat model: " + modelPath);
            }
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            ModelImporterClipAnimation[] clips = importer.defaultClipAnimations;
            foreach (var clip in clips)
            {
                // 原包事件可能调用原脚本，副本必须清除，动画也不能驱动模拟根节点。
                clip.events = Array.Empty<AnimationEvent>();
                clip.loopTime = clip.name.EndsWith("Idle", StringComparison.Ordinal);
                clip.lockRootRotation = true;
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();

            string controllerPath = Root + name + ".controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            }
            var machine = controller.layers[0].stateMachine;
            foreach (var child in machine.states)
            {
                machine.RemoveState(child.state);
            }
            AnimationClip[] animations = AssetDatabase.LoadAllAssetsAtPath(modelPath)
                .OfType<AnimationClip>().Where(clip => !clip.name.StartsWith("__")).ToArray();
            AddState(machine, animations, "Idle", prefix + "_Idle", 0f);
            AddState(machine, animations, "Fire", prefix + "_Fire", fireSeconds);
            AddState(machine, animations, "Reload", prefix + "_Reload", 2f);
            AddState(machine, animations, "Equip", prefix + "_Equip", 0.2f);
            machine.defaultState = machine.states.First(child => child.state.name == "Idle").state;
            EditorUtility.SetDirty(controller);
        }

        private static void AddState(AnimatorStateMachine machine, AnimationClip[] clips,
            string stateName, string clipName, float durationSeconds)
        {
            AnimationClip clip = clips.FirstOrDefault(value => value.name == clipName);
            if (clip == null)
            {
                throw new InvalidOperationException("Missing animation: " + clipName);
            }
            AnimatorState state = machine.AddState(stateName);
            state.motion = clip;
            state.speed = durationSeconds > 0f ? clip.length / durationSeconds : 1f;
            if (stateName == "Fire" || stateName == "Equip")
            {
                var transition = state.AddTransition(machine.states.First(child => child.state.name == "Idle").state);
                transition.hasExitTime = true;
                transition.exitTime = 1f;
                transition.duration = 0.04f;
            }
        }
    }
}
