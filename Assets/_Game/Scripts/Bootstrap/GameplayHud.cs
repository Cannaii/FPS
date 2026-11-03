using System.Text;
using AFPS.NetCode.Match;
using AFPS.NetCode.Sessions;
using AFPS.Presentation.Weapons;
using AFPS.NetCode.Weapons;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace AFPS.Bootstrap
{
    /// <summary>练习/联网共用的游戏HUD：生命、弹药、移动资源、对局比分和战斗反馈。</summary>
    // 保留旧组件类型的迁移信息；文件与.meta成对移动，场景/Prefab继续沿用原GUID。
    [UnityEngine.Scripting.APIUpdating.MovedFrom(
        true, sourceNamespace: "AFPS.Bootstrap", sourceAssembly: "_Game.Bootstrap",
        sourceClassName: "DeathmatchHud")]
    public sealed class GameplayHud : MonoBehaviour
    {
        private readonly StringBuilder scoreBuilder = new StringBuilder(256);
        private NetworkSessionCoordinator sessionCoordinator;
        private float tickDeltaTime;
        private Canvas canvas;
        private GameplayHudView view;
        private Vector2 damageDirectionOrigin;
        private Quaternion damageDirectionRotation;
        private TextMeshProUGUI statusText;
        private TextMeshProUGUI scoreText;
        private TextMeshProUGUI networkText;
        private TextMeshProUGUI healthText;
        private TextMeshProUGUI weaponText;
        private TextMeshProUGUI centerText;
        private TextMeshProUGUI hitMarkerText;
        private ShotPresentationController shotResults;
        private TextMeshProUGUI rejectionText;
        private TextMeshProUGUI damageDirectionText;
        private Image damageFlash;
        private CrosshairGraphic crosshair;

        public void Configure(NetworkSessionCoordinator manager, float tickSeconds, ShotPresentationController results = null)
        {
            sessionCoordinator = manager;
            tickDeltaTime = tickSeconds;
            shotResults = results;
            EnsureUi();
        }

        private void LateUpdate()
        {
            if (sessionCoordinator == null)
            {
                return;
            }

            DeathmatchState match = sessionCoordinator.ClientDeathmatchState;
            // 诊断展开时使用独立阅读层，避免只遮住半块生命/弹药HUD；关闭后按当前对局状态恢复。
            var diagnostics = GetComponent<NetworkDebugPanel>();
            // 连接菜单仍在前景时暂隐战斗HUD，避免人数、比分和枪械提示透在大厅背景里。
            bool visible = match != null && GetComponent<NetworkLobbyMenu>() == null
                && (diagnostics == null || !diagnostics.IsVisible);
            if (canvas != null)
            {
                canvas.enabled = visible;
            }
            if (!visible)
            {
                return;
            }

            bool confirmedHit = shotResults != null && shotResults.HitMarkerVisible;
            hitMarkerText.text = confirmedHit ? "X" : string.Empty;
            hitMarkerText.color = confirmedHit && shotResults.LastHitWasKill
                ? view.KillColor
                : view.HitColor;
            UpdateCombatFeedback();

            uint localEntityId = sessionCoordinator.RemotePlayers != null ? sessionCoordinator.RemotePlayers.LocalEntityId : 0;
            match.TryGetPlayer(localEntityId, out DeathmatchPlayerState local);
            crosshair.gameObject.SetActive(local.IsAlive && match.Phase == DeathmatchPhase.Playing);
            int seconds = Mathf.CeilToInt(match.RemainingTicks * tickDeltaTime);
            statusText.text = sessionCoordinator.IsLocalPractice
                ? "单人练习"
                : match.Phase == DeathmatchPhase.Playing ? $"自由死斗  {seconds / 60:00}:{seconds % 60:00}" : "自由死斗";
            networkText.text = sessionCoordinator.IsLocalPractice ? "本地练习 · 无需联网"
                : sessionCoordinator.HasTimeSync ? $"网络延迟  {sessionCoordinator.Metrics.LastNetworkRoundTripMilliseconds:F1} 毫秒"
                : "正在同步网络时钟…";
            healthText.text = $"生命  {local.Health:000}   击杀 {local.Kills} / 死亡 {local.Deaths}";
            if (sessionCoordinator.ClientSession != null)
            {
                var movement = sessionCoordinator.ClientSession.CurrentState;
                int stamina = Mathf.RoundToInt((1f - movement.StaminaSpentFixed.ToFloat()) * 100f);
                int maximumDashCharges = sessionCoordinator.WorldContext?.MovementConfiguration.Dash.MaxCharges
                    ?? AFPS.Simulation.Characters.PlayerDashConfig.Default.MaxCharges;
                string state = !local.IsAlive ? "已阵亡，等待重生"
                    : movement.SprintExhausted ? "体力耗尽"
                    : movement.IsGrappling ? "钩索摆荡 · 松开右键脱钩"
                    : movement.IsClimbing ? "攀爬中 · 空格跳离"
                    : movement.IsWallRunning ? "墙跑中"
                    : movement.SlideRemainingFixed > AFPS.Math.Fix.Zero ? "滑铲中"
                    : movement.IsCrouched ? "下蹲中" : movement.IsSprinting ? "冲刺中" : "就绪";
                healthText.text += $"\n体力 {stamina:000}%   突进 {maximumDashCharges - movement.DashSpentCharges}/{maximumDashCharges}\n{state}";
            }
            weaponText.text = string.Empty;
            if (sessionCoordinator.ClientWeapon.HasState)
            {
                string weapon = sessionCoordinator.ClientWeaponConfiguration.FireMode == AFPS.Simulation.Weapons.WeaponFireMode.Projectile
                    ? "榴弹发射器" : "突击步枪";
                weaponText.text = $"{weapon}\n<size={view.AmmoFontSize}>"
                    + $"{sessionCoordinator.ClientWeapon.Ammo:00} / "
                    + $"{sessionCoordinator.ClientWeapon.State.Capacity:00}</size>";
                if (sessionCoordinator.ClientWeapon.IsSwitchPending || sessionCoordinator.ClientWeapon.State.IsEquipping)
                {
                    weaponText.text += "\n正在切换武器…";
                }
                else if (sessionCoordinator.ClientWeapon.IsReloading)
                {
                    weaponText.text += sessionCoordinator.ClientWeapon.State.IsReloading
                        ? $"\n换弹中  {sessionCoordinator.ClientWeapon.State.ReloadTicksRemaining * tickDeltaTime:F1} 秒"
                        : "\n换弹中…";
                }
                else
                {
                    weaponText.text += "\n左键开火   R 换弹   Q 切枪";
                }
            }
            scoreBuilder.Clear();
            scoreBuilder.AppendLine("玩家    击杀 / 死亡");
            for (int i = 0; i < match.PlayerCount; i++)
            {
                DeathmatchPlayerState player = match.GetPlayer(i);
                scoreBuilder.Append(player.EntityId == localEntityId ? "> " : "  ");
                scoreBuilder.Append("玩家 ").Append(player.EntityId).Append("   ").Append(player.Kills).Append(" / ").Append(player.Deaths);
                if (!player.IsAlive)
                {
                    scoreBuilder.Append("   阵亡");
                }
                scoreBuilder.AppendLine();
            }
            scoreText.text = sessionCoordinator.IsLocalPractice ? "单人练习" : scoreBuilder.ToString();
            view.ScorePanel.SetActive(!sessionCoordinator.IsLocalPractice);

            switch (match.Phase)
            {
                case DeathmatchPhase.WaitingForPlayers:
                    centerText.text = "等待另一名玩家加入";
                    break;
                case DeathmatchPhase.Finished:
                    centerText.text = match.WinnerEntityId == 0 ? "平局" : match.WinnerEntityId == localEntityId ? "胜利" : $"玩家 {match.WinnerEntityId} 获胜";
                    break;
                default:
                    centerText.text = local.IsAlive ? string.Empty : $"{Mathf.Max(1, Mathf.CeilToInt(local.RespawnTicksRemaining * tickDeltaTime))} 秒后重生";
                    break;
            }
        }

        private void UpdateCombatFeedback()
        {
            bool damaged = shotResults != null && shotResults.DamageFeedbackVisible;
            damageFlash.enabled = damaged;
            var camera = UnityEngine.Camera.main;
            Vector2 direction = default;
            bool directed = damaged && camera != null
                && shotResults.TryGetDamageDirection(camera.transform.position, camera.transform.rotation, out direction);
            damageDirectionText.text = directed ? "▲" : string.Empty;
            if (directed)
            {
                float angle = Mathf.Atan2(direction.x, direction.y);
                damageDirectionText.rectTransform.anchoredPosition = damageDirectionOrigin
                    + direction * view.DamageDirectionRadius;
                damageDirectionText.rectTransform.localRotation = damageDirectionRotation
                    * Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg);
            }
            rejectionText.text = shotResults != null && shotResults.RejectionFeedbackVisible
                ? shotResults.LastRejectionReason switch
                {
                    WeaponFireRejectReason.Reloading => "正在换弹",
                    WeaponFireRejectReason.EmptyMagazine => "弹匣已空，请按 R 换弹",
                    WeaponFireRejectReason.FireRateLimited => "射速限制，请稍候",
                    WeaponFireRejectReason.WeaponUnavailable => "武器暂不可用",
                    _ => "本次射击未获服务器确认"
                }
                : string.Empty;
        }

        private void EnsureUi()
        {
            if (canvas != null)
            {
                return;
            }

            var prefab = Resources.Load<GameplayHudView>(GameplayHudView.ResourcePath);
            if (prefab == null)
            {
                throw new System.InvalidOperationException("缺少 Resources/UI/GameplayHud.prefab。");
            }

            if (!prefab.TryValidate(out string error))
            {
                throw new System.InvalidOperationException(error);
            }

            view = Instantiate(prefab, transform, false);
            // 保留既有HUD根名称，供诊断及原生验收识别；资源文件名不作为运行时约定。
            view.name = "Gameplay HUD";
            canvas = view.Canvas;
            damageFlash = view.DamageFlash;
            statusText = view.StatusText;
            scoreText = view.ScoreText;
            networkText = view.NetworkText;
            healthText = view.HealthText;
            weaponText = view.WeaponText;
            centerText = view.CenterText;
            hitMarkerText = view.HitMarkerText;
            rejectionText = view.RejectionText;
            damageDirectionText = view.DamageDirectionText;
            // 缓存编辑位置/朝向作为动画原点，受击反馈不覆盖美术设定的基准布局。
            damageDirectionOrigin = damageDirectionText.rectTransform.anchoredPosition;
            damageDirectionRotation = damageDirectionText.rectTransform.localRotation;
            var crosshairPrefab = Resources.Load<CrosshairGraphic>("UI/Crosshair");
            if (crosshairPrefab == null)
            {
                throw new System.InvalidOperationException("缺少UI/Crosshair.prefab。");
            }
            crosshair = Instantiate(crosshairPrefab, canvas.transform, false);
            crosshair.transform.SetSiblingIndex(1);
            canvas.enabled = false;
        }

        private void OnDisable()
        {
            if (canvas != null)
            {
                canvas.enabled = false;
            }
        }

        private void OnDestroy()
        {
            if (view == null)
            {
                return;
            }

            if (Application.isPlaying)
            {
                Destroy(view.gameObject);
            }
            else
            {
                DestroyImmediate(view.gameObject);
            }
        }
    }
}
