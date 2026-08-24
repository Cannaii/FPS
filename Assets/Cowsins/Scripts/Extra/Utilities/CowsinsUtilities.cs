using System;
using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Presets;
using UnityEditor.Animations;
#endif
using System.IO;

namespace cowsins
{
    public static class CowsinsUtilities
    {
        /// <summary>
        /// Returns a Vector3 that applies spread to the bullets shot
        /// </summary>
        public static Vector3 GetSpreadDirection(float amount, Camera camera)
        {
            float horSpread = UnityEngine.Random.Range(-amount, amount);
            float verSpread = UnityEngine.Random.Range(-amount, amount);
            Vector3 spread = camera.transform.InverseTransformDirection(new Vector3(horSpread, verSpread, 0));
            Vector3 dir = camera.transform.forward + spread;

            return dir;
        }
        public static void PlayAnim(string anim, Animator animator)
        {
            animator.SetTrigger(anim);
        }

        public static void ForcePlayAnim(string anim, Animator animator)
        {
            animator.Play(anim, 0, 0);
        }
        public static void StartAnim(string anim, Animator animated) => animated.SetBool(anim, true);

        public static void StopAnim(string anim, Animator animated) => animated.SetBool(anim, false);
#if UNITY_EDITOR
        public static void SavePreset(UnityEngine.Object source, string name)
        {
            if (EmptyString(name))
            {
                Debug.LogError("ERROR: Do not forget to give your preset a name!");
                return;
            }
            Preset preset = new Preset(source);

            string directoryPath = "Assets/" + "Cowsins/" + "CowsinsPresets/";

            if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);

            string fullPath = directoryPath + name + ".preset";
            AssetDatabase.CreateAsset(preset, fullPath);
            Debug.Log($"Preset successfully saved in {fullPath}");
        }
        public static void ApplyPreset(Preset preset, UnityEngine.Object target)
        {
            preset.ApplyTo(target);
        }

        public static bool IsUsingUnity6()
        {
            string unityVersion = Application.unityVersion;
            return unityVersion.StartsWith("6"); 
        }

#endif
        public static bool EmptyString(string string_)
        {
            if (string_.Length == 0) return true;
            int i = 0;
            while (i < string_.Length)
            {
                if (string_[i].ToString() == " ") return true;
                i++;
            }
            return false;
        }

        public static IDamageable GatherDamageableParent(Transform child)
        {
            for (Transform parent = child.parent; parent != null; parent = parent.parent)
            {
                if (parent.TryGetComponent(out IDamageable component))
                {
                    return component;
                }
            }
            return null;
        }

        public static bool InvokeFunc(Func<bool> del, bool defaultValue = true)
        {
            if (del == null)
                return defaultValue;

            foreach (Func<bool> func in del.GetInvocationList())
            {
                if (!func())
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Determine wether this is determined as floor or not
        /// </summary>
        public static bool IsFloor(Vector3 v, float maxSlopeAngle)
        {
            float currentFloorAngle = Vector3.Angle(Vector3.up, v);
            return currentFloorAngle < maxSlopeAngle;
        }

        /// <summary>
        /// Checks if the attachment is compatible with the current unholstered weapon
        /// </summary>
        /// <param name="weapon">Weapon to check compatibility</param>
        /// <returns></returns>
        public static (bool found, Attachment attachment, int index) CompatibleAttachment(WeaponIdentification weaponIdentification, AttachmentIdentifier_SO identifier)
        {
            if (weaponIdentification == null) return (false, null, -1);

            Weapon_SO weapon = weaponIdentification.weapon;

            if (weapon?.weaponObject == null || identifier == null)
                return (false, null, -1);

            var compatible = weaponIdentification.compatibleAttachments;

            foreach (AttachmentType type in System.Enum.GetValues(typeof(AttachmentType)))
            {
                IReadOnlyList<Attachment> attachments = compatible.GetCompatible(type);

                for (int i = 0; i < attachments.Count; i++)
                {
                    if (attachments[i]?.attachmentIdentifier == identifier)
                        return (true, attachments[i], i);
                }
            }

            return (false, null, -1);
        }


#if UNITY_EDITOR
        public static (bool, float) CheckClipAvailability(Animator animator, string stateName)
        {
            AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;
            if (controller == null) return (false, 0f);

            foreach (var layer in controller.layers)
            {
                foreach (var childState in layer.stateMachine.states)
                {
                    if (childState.state.name == stateName)
                    {
                        AnimationClip clip = childState.state.motion as AnimationClip;
                        if (clip != null)
                        {
                            return (true, clip.length);
                        }
                        return (false, 0f);
                    }
                }
            }

            return (false, 0f);
        }
#endif
        public static AudioClip[] GetSoundsForLayer(this PlayerMovementSettings.FootStepsSounds footsteps, int layer)
        {
            // Check dynamic entries first
            foreach (var entry in footsteps.surfaceSounds)
            {
                if (entry.cachedLayerIndex == -1)
                    entry.cachedLayerIndex = LayerMask.NameToLayer(entry.layerName);

                if (entry.cachedLayerIndex == layer && entry.sounds.Length > 0)
                    return entry.sounds;
            }

            // Fallback to default
            return footsteps.defaultStep;
        }

        public static GameObject GetImpactForLayer(this WeaponControllerSettings.ImpactEffects impactEffects, int layer)
        {
            foreach (var entry in impactEffects.impacts)
            {
                if (entry.cachedLayerIndex == -1)
                    entry.cachedLayerIndex = LayerMask.NameToLayer(entry.layerName);

                if (entry.cachedLayerIndex == layer && entry.impact != null)
                    return entry.impact;
            }

            return impactEffects.defaultImpact;
        }
        public static GameObject GetBulletHoleForLayer(this BulletHoleImpact bulletHoles, int layer)
        {
            foreach (var entry in bulletHoles.bulletHoleImpact)
            {
                entry.cachedLayerIndex = LayerMask.NameToLayer(entry.layerName);

                if (entry.cachedLayerIndex == layer && entry.bulletHoleImpact != null)
                    return entry.bulletHoleImpact;
            }

            return bulletHoles.defaultImpact;
        }

        public static bool MatchesBulletType(BulletTypeIdentifier_SO a, BulletTypeIdentifier_SO b)
        {
            bool IsUniversal(BulletTypeIdentifier_SO id) =>
                id == null || id.bulletType == BulletTypeIdentifier_SO.BulletType.Universal;

            // If both are universal -> valid.
            if (IsUniversal(a) && IsUniversal(b))
                return true;

            // If only one is universal -> Not valid.
            if (IsUniversal(a) || IsUniversal(b))
                return false;

            // Both are non-universal -> They must match exactly.
            return a == b;
        }

        private const string logPrefix = "<color=red>[COWSINS]</color>";
        private static void Log(LogType type, string message, UnityEngine.Object context = null)
        {
            switch (type)
            {
                case LogType.Warning:
                    if (context == null) Debug.LogWarning(Format(message));
                    else Debug.LogWarning(Format(message), context);
                    break;

                case LogType.Error:
                    if (context == null) Debug.LogError(Format(message));
                    else Debug.LogError(Format(message), context);
                    break;

                case LogType.Exception:
                    if (context == null) Debug.LogError(Format(message));
                    else Debug.LogError(Format(message), context);
                    break;
            }
        }
        private static string Format(string message) => $"{logPrefix} {message}";

        public static void LogWarning(string message, UnityEngine.Object context = null)
            => Log(LogType.Warning, message, context);

        public static void LogError(string message, UnityEngine.Object context = null)
            => Log(LogType.Error, message, context);

        public static void LogErrorFormat(string message, params object[] args)
            => Debug.LogErrorFormat(Format(message), args);
    }
}
