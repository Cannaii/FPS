using AFPS.Simulation.Characters;
using AFPS.Simulation.Weapons;
using UnityEngine;

namespace AFPS.Presentation.Weapons
{
    public sealed partial class FirstPersonWeaponView
    {
        private const int ViewLayer = 31; // 独立武器相机专用层，不用于世界碰撞。
        [SerializeField, Range(0f, 25f)] private float viewFieldOfViewOffset = 15f; // 角度，保留中央视野。
        private GameObject ownedModel;
        private Transform modelPivot;
        private UnityEngine.Camera worldCamera;
        private UnityEngine.Camera weaponCamera;
        private AudioSource reloadAudio;
        private LineRenderer flash;
        private Material flashMaterial;
        private ushort weaponId;
        private bool launcher;
        private float movementPhase;
        private int originalCameraMask;
        private bool ownsCameraMask;
        private Vector3 muzzlePoint = new Vector3(0.1f, -0.05f, 0.6f);

        public float RecoilWeight { get; private set; }
        public bool Reloading { get; private set; }
        public float FlashRemaining { get; private set; }
        public ushort WeaponId => weaponId;
        public bool HasOwnedModel => ownedModel != null && animator != null
            && animator.avatar != null && animator.avatar.isValid && animator.runtimeAnimatorController != null;

        /// <summary>默认场景缺少接线时装配视图；模型的坐标/缩放仅影响渲染。</summary>
        public void Configure(UnityEngine.Camera camera)
        {
            if (worldCamera != null || camera == null)
            {
                return;
            }
            worldCamera = camera;
            originalCameraMask = camera.cullingMask;
            ownsCameraMask = (originalCameraMask & (1 << ViewLayer)) != 0;
            camera.cullingMask &= ~(1 << ViewLayer);
            var target = new GameObject("Weapon Camera");
            target.transform.SetParent(transform, false);
            weaponCamera = target.AddComponent<UnityEngine.Camera>();
            weaponCamera.clearFlags = CameraClearFlags.Depth;
            weaponCamera.cullingMask = 1 << ViewLayer;
            weaponCamera.depth = camera.depth + 1f;
            weaponCamera.nearClipPlane = 0.06f;
            weaponCamera.farClipPlane = 5f;
            weaponCamera.allowHDR = camera.allowHDR;
            weaponCamera.allowMSAA = camera.allowMSAA;
            weaponCamera.fieldOfView = Mathf.Clamp(camera.fieldOfView + viewFieldOfViewOffset, 40f, 110f);
            // 音频监听器仍使用主相机，武器相机不能再创建第二个监听器。
        }

        public void SetWeapon(in WeaponSimulationConfig config)
        {
            if (weaponId == config.WeaponId)
            {
                return;
            }
            StopPresentation();
            weaponId = config.WeaponId;
            launcher = config.FireMode == WeaponFireMode.Projectile;
            if (ownedModel != null)
            {
                ownedModel.SetActive(false);
                Release(ownedModel);
            }
            if (modelPivot == null)
            {
                modelPivot = new GameObject("Weapon Recoil").transform;
                modelPivot.SetParent(transform, false);
                recoilPivot = modelPivot;
                recoilBaseRotation = Quaternion.identity;
                recoilBasePosition = Vector3.zero;
            }
            GameObject prefab = CombatPresentationAssets.Load<GameObject>(launcher ? "Launcher" : "Rifle");
            if (prefab != null)
            {
                ownedModel = Instantiate(prefab, modelPivot, false);
                animator = ownedModel.GetComponent<Animator>();
                if (animator == null)
                {
                    animator = ownedModel.AddComponent<Animator>();
                }
                animator.runtimeAnimatorController = CombatPresentationAssets.Load<RuntimeAnimatorController>(launcher ? "Launcher" : "Rifle");
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.Rebind();
                animator.Update(0f);
                Material material = CombatPresentationAssets.Load<Material>("Weapon");
                Material skinMaterial = CombatPresentationAssets.Load<Material>("Skin");
                Renderer[] renderers = ownedModel.GetComponentsInChildren<Renderer>(true);
                Bounds bounds = default;
                Bounds weaponBounds = default;
                bool hasBounds = false;
                bool hasWeaponBounds = false;
                foreach (var renderer in renderers)
                {
                    Bounds localBounds = BoundsInPivot(renderer, modelPivot);
                    if (!hasBounds)
                    {
                        bounds = localBounds;
                        hasBounds = true;
                    }
                    else
                    {
                        bounds.Encapsulate(localBounds);
                    }
                    bool arms = renderer.name.ToLowerInvariant().Contains("arm")
                        || renderer.name.ToLowerInvariant().Contains("hand");
                    if (!arms)
                    {
                        if (hasWeaponBounds)
                        {
                            weaponBounds.Encapsulate(localBounds);
                        }
                        else
                        {
                            weaponBounds = localBounds;
                            hasWeaponBounds = true;
                        }
                    }
                    renderer.sharedMaterials = System.Array.ConvertAll(renderer.sharedMaterials,
                        _ => arms && skinMaterial != null ? skinMaterial : material);
                    renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    renderer.receiveShadows = false;
                }
                // 在相机局部空间量尺寸，避免切换时 Yaw/Pitch 改变武器缩放。
                // 原 rig 的观察点在 +Y 1.6 米；保留此锚点，不能把手臂整体前移导致切口进入镜头。
                if (hasBounds && bounds.size.magnitude > 0.001f)
                {
                    float scale = 0.8f / Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                    ownedModel.transform.localScale *= scale;
                    Vector3 offset = new Vector3(0.04f, -1.6f * scale - 0.06f, 0.04f);
                    ownedModel.transform.localPosition = offset;
                    if (hasWeaponBounds)
                    {
                        muzzlePoint = offset + new Vector3(weaponBounds.center.x,
                            weaponBounds.center.y, weaponBounds.max.z) * scale;
                    }
                }
                foreach (Transform child in ownedModel.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.layer = ViewLayer;
                }
                foreach (var collider in ownedModel.GetComponentsInChildren<Collider>(true))
                {
                    collider.enabled = false;
                }
                CacheParameters();
                PlayOwnedAnimation("Equip");
            }
            if (shotAudio == null)
            {
                shotAudio = gameObject.AddComponent<AudioSource>();
                shotAudio.playOnAwake = false;
                shotAudio.spatialBlend = 0f;
                shotAudio.volume = 0.55f;
            }
            shotAudio.clip = CombatPresentationAssets.FireClip(launcher);
            if (reloadAudio == null)
            {
                reloadAudio = gameObject.AddComponent<AudioSource>();
                reloadAudio.playOnAwake = false;
                reloadAudio.volume = 0.5f;
            }
            reloadAudio.clip = CombatPresentationAssets.ReloadClip(launcher);
        }

        public void SetReloading(bool reloading)
        {
            if (Reloading == reloading)
            {
                return;
            }
            Reloading = reloading;
            PlayOwnedAnimation(reloading ? "Reload" : "Idle");
            if (reloadAudio != null)
            {
                if (reloading && reloadAudio.clip != null)
                {
                    reloadAudio.Play();
                }
                else
                {
                    reloadAudio.Stop();
                }
            }
        }

        private void PlayOwnedAnimation(string state)
        {
            if (ownedModel != null && animator != null && animator.runtimeAnimatorController != null)
            {
                animator.CrossFadeInFixedTime(state, 0.035f);
            }
        }

        private void AdvancePresentation(float elapsed, in PlayerState state)
        {
            FlashRemaining = Mathf.Max(0f, FlashRemaining - elapsed);
            if (weaponCamera != null && worldCamera != null)
            {
                weaponCamera.fieldOfView = Mathf.Clamp(worldCamera.fieldOfView + viewFieldOfViewOffset, 40f, 110f);
                weaponCamera.rect = worldCamera.rect;
            }
            if (modelPivot != null)
            {
                movementPhase += elapsed * 10f;
                float speed = new Vector3(state.Velocity.x, 0f, state.Velocity.z).magnitude;
                float amount = state.IsGrounded ? Mathf.Clamp01(speed / fullSpeed) : 0f;
                modelPivot.localPosition = new Vector3(
                    Mathf.Sin(movementPhase) * amount * 0.006f,
                    Mathf.Abs(Mathf.Cos(movementPhase)) * amount * 0.006f - (Reloading ? 0.05f : 0f),
                    -RecoilWeight * 0.025f);
            }
            if (flash == null && FlashRemaining > 0f)
            {
                Material template = CombatPresentationAssets.Load<Material>("Effects");
                Shader shader = template != null ? template.shader : Shader.Find("Sprites/Default");
                if (shader != null)
                {
                    flashMaterial = template != null ? new Material(template) : new Material(shader);
                    flash = new GameObject("Local Muzzle Flash").AddComponent<LineRenderer>();
                    flash.transform.SetParent(modelPivot != null ? modelPivot : transform, false);
                    flash.gameObject.layer = ViewLayer;
                    flash.sharedMaterial = flashMaterial;
                    flash.useWorldSpace = false;
                    flash.positionCount = 4;
                    flash.widthMultiplier = 0.04f;
                    flash.startColor = Color.yellow;
                    flash.endColor = new Color(1f, 0.35f, 0f, 0f);
                }
            }
            if (flash != null)
            {
                // 切换后重用闪光对象，但更新为当前枪模前端，避免悬浮在旧武器的枪口位置。
                flash.SetPositions(new[] { muzzlePoint, muzzlePoint + Vector3.forward * 0.12f,
                    muzzlePoint + Vector3.left * 0.04f, muzzlePoint + Vector3.right * 0.04f });
                flash.enabled = FlashRemaining > 0f;
            }
        }

        private static Bounds BoundsInPivot(Renderer renderer, Transform pivot)
        {
            Bounds local = renderer.localBounds;
            Matrix4x4 matrix = pivot.worldToLocalMatrix * renderer.localToWorldMatrix;
            var result = new Bounds(matrix.MultiplyPoint3x4(local.center), Vector3.zero);
            for (int corner = 0; corner < 8; corner++)
            {
                Vector3 offset = new Vector3(
                    (corner & 1) == 0 ? -local.extents.x : local.extents.x,
                    (corner & 2) == 0 ? -local.extents.y : local.extents.y,
                    (corner & 4) == 0 ? -local.extents.z : local.extents.z);
                result.Encapsulate(matrix.MultiplyPoint3x4(local.center + offset));
            }
            return result;
        }

        public void StopPresentation()
        {
            SetReloading(false);
            recoilAges.Clear();
            RecoilWeight = 0f;
            FlashRemaining = 0f;
            if (shotAudio != null)
            {
                shotAudio.Stop();
            }
            if (flash != null)
            {
                flash.enabled = false;
            }
            if (recoilPivot != null)
            {
                recoilPivot.localRotation = recoilBaseRotation;
                recoilPivot.localPosition = recoilBasePosition;
            }
        }

        private void OnDisable()
        {
            StopPresentation();
        }

        private void OnDestroy()
        {
            if (worldCamera != null && ownsCameraMask)
            {
                worldCamera.cullingMask |= 1 << ViewLayer;
            }
            if (flashMaterial != null)
            {
                Release(flashMaterial);
            }
        }

        private static void Release(Object target)
        {
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
