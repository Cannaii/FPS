using System.Collections.Generic;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Weapons;
using UnityEngine;

namespace AFPS.Presentation.Weapons
{
    /// <summary>远端开发枪与持枪手臂姿态，只读权威消息和显示Tick，不预测弹量/开火。</summary>
    public sealed class RemoteWeaponView : MonoBehaviour
    {
        [SerializeField] private Transform rifle;
        [SerializeField] private Transform launcher;
        [SerializeField] private Transform reloadArm;
        private Quaternion armRestRotation;
        private Vector3 rifleRestPosition;
        private Vector3 launcherRestPosition;
        private Quaternion rifleRestRotation;
        private Quaternion launcherRestRotation;
        private bool initialized;
        private bool hasState;
        private bool hasShot;
        private bool hasSequence;
        private readonly List<AuthoritativeShot> pendingShots = new List<AuthoritativeShot>(16);
        private uint lastShotSequence;
        private uint shotTick;
        private RemoteWeaponState current;

        public ushort WeaponId => hasState ? current.WeaponId : (ushort)0;
        public bool IsReloading { get; private set; }
        public bool IsLauncherVisible => launcher != null && launcher.gameObject.activeSelf;
        public ulong PresentedShotCount { get; private set; }
        public bool IsConfigured => rifle != null && launcher != null && reloadArm != null;

        public void Configure(Transform rifleTransform, Transform launcherTransform, Transform armTransform)
        {
            rifle = rifleTransform;
            launcher = launcherTransform;
            reloadArm = armTransform;
            initialized = false;
            Initialize();
        }

        private void Awake()
        {
            Initialize();
            Hide();
        }

        private void Initialize()
        {
            if (initialized || !IsConfigured)
            {
                return;
            }
            rifleRestPosition = rifle.localPosition;
            launcherRestPosition = launcher.localPosition;
            rifleRestRotation = rifle.localRotation;
            launcherRestRotation = launcher.localRotation;
            armRestRotation = reloadArm.localRotation;
            initialized = true;
        }

        public void Render(in RemoteWeaponState state, bool projectile, double renderTick)
        {
            Initialize();
            if (!initialized || !state.IsValid || !state.IsEquipped
                || double.IsNaN(renderTick) || double.IsInfinity(renderTick)
                || renderTick < 0d || renderTick >= long.MaxValue)
            {
                Hide();
                return;
            }
            if (!hasState || current.WeaponId != state.WeaponId)
            {
                hasShot = false;
            }
            current = state;
            hasState = true;
            IsReloading = state.ReloadTicksRemaining > System.Math.Max(0d, TickDistance(renderTick, state.ServerTick));
            for (int i = pendingShots.Count - 1; i >= 0; i--)
            {
                AuthoritativeShot shot = pendingShots[i];
                double age = TickDistance(renderTick, shot.ServerTick);
                if (age < 0d)
                {
                    continue;
                }
                pendingShots.RemoveAt(i);
                if (age <= 6d && shot.WeaponId == state.WeaponId
                    && (!hasShot || SequenceMath.IsNewer(shot.ServerTick, shotTick)))
                {
                    hasShot = true;
                    shotTick = shot.ServerTick;
                    PresentedShotCount++;
                }
            }
            rifle.gameObject.SetActive(!projectile);
            launcher.gameObject.SetActive(projectile);
            double elapsed = TickDistance(renderTick, state.ServerTick);
            float remaining = Mathf.Max(0f, state.ReloadTicksRemaining - (float)System.Math.Max(0d, elapsed));
            float progress = 1f - remaining / ServerWeaponFireController.ReloadDurationTicks;
            float reload = IsReloading ? Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI) : 0f;
            float equip = Mathf.Clamp01((state.EquipTicksRemaining - (float)System.Math.Max(0d, elapsed))
                / AFPS.Simulation.Weapons.WeaponCatalog.EquipDurationTicks);
            double sinceShot = hasShot ? TickDistance(renderTick, shotTick) : -1d;
            // 后坐力只在显示时刻到达射击Tick后播放，6Tick（50Hz约0.12秒）内恢复。
            float recoil = sinceShot >= 0d ? Mathf.Clamp01(1f - (float)sinceShot / 6f) : 0f;
            Quaternion offset = Quaternion.Euler(reload * 25f + equip * 35f - recoil * 8f, 0f, reload * -12f);
            Vector3 translation = new Vector3(0f, -equip * 0.2f, -recoil * 0.06f);
            rifle.localRotation = rifleRestRotation * offset;
            launcher.localRotation = launcherRestRotation * offset;
            rifle.localPosition = rifleRestPosition + translation;
            launcher.localPosition = launcherRestPosition + translation;
            reloadArm.localRotation = armRestRotation * Quaternion.Euler(-reload * 35f, 0f, reload * -45f);
        }

        public bool PlayShot(in AuthoritativeShot shot)
        {
            // 独立武器流可能晚于射击结果；先有界排队，到达移动显示Tick后再消费。
            // 序号跨隐藏/切枪保留，避免旧生命或旧武器的重传再次播放。
            if (hasSequence && !SequenceMath.IsNewer(shot.ShotSequence, lastShotSequence))
            {
                return false;
            }
            hasSequence = true;
            lastShotSequence = shot.ShotSequence;
            if (pendingShots.Count == 16)
            {
                pendingShots.RemoveAt(0);
            }
            pendingShots.Add(shot);
            return true;
        }

        public void Hide()
        {
            Initialize();
            hasState = false;
            hasShot = false;
            pendingShots.Clear();
            IsReloading = false;
            if (!initialized)
            {
                return;
            }
            rifle.gameObject.SetActive(false);
            launcher.gameObject.SetActive(false);
            rifle.localPosition = rifleRestPosition;
            launcher.localPosition = launcherRestPosition;
            rifle.localRotation = rifleRestRotation;
            launcher.localRotation = launcherRestRotation;
            reloadArm.localRotation = armRestRotation;
        }

        private static double TickDistance(double renderTick, uint serverTick)
        {
            double whole = System.Math.Floor(renderTick);
            uint raw = unchecked((uint)(long)whole);
            return renderTick - whole + unchecked((int)(raw - serverTick));
        }

        private void OnDisable()
        {
            Hide();
        }
    }
}
