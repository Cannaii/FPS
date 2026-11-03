using System;
using AFPS.Input;
using AFPS.NetCode.Match;
using AFPS.Presentation.Characters;
using UnityEngine;

namespace AFPS.Bootstrap
{
    /// <summary>仅-afpsHitscanSmoke启用；双方轮流从真实显示位置瞄准，逐方验证步枪伤害。</summary>
    [DefaultExecutionOrder(200)]
    public sealed class HitscanSmokeTestDriver : MonoBehaviour
    {
        public static bool IsRequested => Array.Exists(Environment.GetCommandLineArgs(),
            value => value.Equals("-afpsHitscanSmoke", StringComparison.OrdinalIgnoreCase));
        private UnityNetworkGameplayController movement;
        private LocalPlayerInputCollector input;
        private double configuredAt;
        private double startedAt = -1d;
        private uint localEntity;
        private uint observedResults;
        private int requestedShots;
        private int confirmedHits;
        private bool hitMarker;
        private bool receivedDamage;
        private bool healthReduced;
        private uint maximumAge;

        public void Configure(UnityNetworkGameplayController controller, LocalPlayerInputCollector collector)
        {
            movement = controller;
            input = collector;
            configuredAt = Time.realtimeSinceStartupAsDouble;
            Application.runInBackground = true;
            QualitySettings.vSyncCount = 0;
            int frameRate = 60;
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index + 1 < arguments.Length; index++)
            {
                if (arguments[index].Equals("-afpsHitscanFrameRate", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(arguments[index + 1], out int requested) && requested >= 15 && requested <= 240)
                {
                    frameRate = requested;
                }
            }
            // 仅验证夹具的帧率，单位帧/秒；用30fps复查显示/输入跨帧调度，不改50Hz模拟。
            Application.targetFrameRate = frameRate;
        }

        private void Update()
        {
            double now = Time.realtimeSinceStartupAsDouble;
            if (now - configuredAt > 25d)
            {
                Finish(false);
                return;
            }
            var manager = movement.SessionCoordinator;
            if (manager?.ClientSession == null || !SmokeTestLobby.EnsurePlaying(movement)
                || !movement.CanEnterGame || manager.ClientDeathmatchState?.Phase != DeathmatchPhase.Playing)
            {
                return;
            }
            localEntity = manager.RemotePlayers.LocalEntityId;
            var target = FindObjectOfType<RemotePlayerView>();
            if (localEntity == 0 || target == null || !manager.ClientWeapon.HasState)
            {
                return;
            }
            if (startedAt < 0d)
            {
                startedAt = now;
            }
            double elapsed = now - startedAt;
            if (observedResults != manager.ShotResultVersion)
            {
                observedResults = manager.ShotResultVersion;
                var result = manager.LastReceivedShotResult;
                uint age = unchecked(result.Shot.ServerTick - result.Shot.RewindTick);
                if (result.Shot.EntityId == localEntity)
                {
                    maximumAge = System.Math.Max(maximumAge, age);
                    if (result.DidHit && result.TargetEntityId != localEntity)
                    {
                        confirmedHits++;
                    }
                }
                Debug.Log($"[AFPS_HITSCAN_SHOT] local={localEntity} shooter={result.Shot.EntityId}"
                    + $" server={result.Shot.ServerTick} rewind={result.Shot.RewindTick} age={age}"
                    + $" hit={result.DidHit} target={result.TargetEntityId} health={result.TargetHealthFixed}");
            }
            var presentation = movement.GetComponent<AFPS.Presentation.Weapons.ShotPresentationController>();
            hitMarker |= presentation != null && presentation.HitMarkerVisible;
            receivedDamage |= movement.ReceivedDamageCount >= 2;
            if (manager.ClientDeathmatchState.TryGetPlayer(localEntity, out var player))
            {
                healthReduced |= player.HealthFixed <= AFPS.Math.Fix.FromInt(60);
            }
            // 双方各两发20伤害，保留生命；按实体分阶段，不能用弹丸/爆炸结果代替命中扫描。
            double firstShotSeconds = localEntity == 1 ? 3d : 6d;
            bool fire = requestedShots < 2 && elapsed >= firstShotSeconds + requestedShots * 0.7d
                && manager.ClientWeapon.CanFire;
            if (fire)
            {
                requestedShots++;
            }
            // 默认训练场的躯干目标为脚底以上1米、站立眼高1.6米；不直接覆盖射击Tick或命中结果。
            Vector3 direction = target.transform.position + Vector3.up
                - manager.ClientSession.CurrentState.Position - Vector3.up * 1.6f;
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Atan2(direction.y, new Vector2(direction.x, direction.z).magnitude) * Mathf.Rad2Deg;
            input.SetAutomationInput(0f, 0f, yaw, pitch, false, firePressed: fire);
            if (elapsed >= 10d)
            {
                Finish(requestedShots == 2 && confirmedHits == 2 && receivedDamage && healthReduced && hitMarker);
            }
        }

        private void Finish(bool success)
        {
            enabled = false;
            input.ClearAutomationInput();
            Debug.Log($"[AFPS_HITSCAN_RESULT] {(success ? "PASS" : "FAIL")} entity={localEntity}"
                + $" requested={requestedShots} hits={confirmedHits} damage={receivedDamage}"
                + $" healthReduced={healthReduced} marker={hitMarker} maxAge={maximumAge}");
            SmokeTestExit.Quit(this, success ? 0 : 2);
        }
    }
}
