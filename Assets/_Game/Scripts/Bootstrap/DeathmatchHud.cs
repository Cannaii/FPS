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
    public sealed class DeathmatchHud : MonoBehaviour
    {
        private readonly StringBuilder scoreBuilder = new StringBuilder(256);
        private NetworkMovementSessionManager sessionManager;
        private float tickDeltaTime;
        private Canvas canvas;
        private TextMeshProUGUI statusText;
        private TextMeshProUGUI scoreText;
        private TextMeshProUGUI networkText;
        private TextMeshProUGUI healthText;
        private TextMeshProUGUI weaponText;
        private TextMeshProUGUI centerText;
        private TextMeshProUGUI hitMarkerText;
        private ShotResultView shotResults;
        private TextMeshProUGUI rejectionText;
        private TextMeshProUGUI damageDirectionText;
        private Image damageFlash;

        public void Configure(NetworkMovementSessionManager manager, float tickSeconds, ShotResultView results = null)
        {
            sessionManager = manager;
            tickDeltaTime = tickSeconds;
            shotResults = results;
            EnsureUi();
        }

        private void LateUpdate()
        {
            if (sessionManager == null)
            {
                return;
            }

            DeathmatchState match = sessionManager.ClientDeathmatchState;
            // 诊断展开时使用独立阅读层，避免只遮住半块生命/弹药HUD；关闭后按当前对局状态恢复。
            var diagnostics = GetComponent<NetworkDebugPanel>();
            // 连接菜单仍在前景时暂隐战斗HUD，避免人数、比分和枪械提示透在大厅背景里。
            bool visible = match != null && GetComponent<NetworkConnectionMenu>() == null
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
                ? new Color(1f, 0.25f, 0.2f)
                : Color.white;
            UpdateCombatFeedback();

            uint localEntityId = sessionManager.RemotePlayers != null ? sessionManager.RemotePlayers.LocalEntityId : 0;
            match.TryGetPlayer(localEntityId, out DeathmatchPlayerState local);
            int seconds = Mathf.CeilToInt(match.RemainingTicks * tickDeltaTime);
            statusText.text = sessionManager.IsLocalPractice
                ? "单人练习"
                : match.Phase == DeathmatchPhase.Playing ? $"自由死斗  {seconds / 60:00}:{seconds % 60:00}" : "自由死斗";
            networkText.text = sessionManager.IsLocalPractice ? "本地练习 · 无需联网"
                : sessionManager.HasTimeSync ? $"网络延迟  {sessionManager.Metrics.LastNetworkRoundTripMilliseconds:F1} 毫秒"
                : "正在同步网络时钟…";
            healthText.text = $"生命  {local.Health:000}   击杀 {local.Kills} / 死亡 {local.Deaths}";
            if (sessionManager.ClientSession != null)
            {
                var movement = sessionManager.ClientSession.CurrentState;
                int stamina = Mathf.RoundToInt((1f - movement.StaminaSpentFixed.ToFloat()) * 100f);
                int maximumDashCharges = sessionManager.WorldContext?.Configuration.Dash.MaxCharges
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
            if (sessionManager.ClientWeapon.HasState)
            {
                string weapon = sessionManager.ClientWeaponConfiguration.FireMode == AFPS.Simulation.Weapons.WeaponFireMode.Projectile
                    ? "榴弹发射器" : "突击步枪";
                weaponText.text = $"{weapon}\n<size=38>{sessionManager.ClientWeapon.Ammo:00} / {sessionManager.ClientWeapon.State.Capacity:00}</size>";
                if (sessionManager.ClientWeapon.IsSwitchPending || sessionManager.ClientWeapon.State.IsEquipping)
                {
                    weaponText.text += "\n正在切换武器…";
                }
                else if (sessionManager.ClientWeapon.IsReloading)
                {
                    weaponText.text += sessionManager.ClientWeapon.State.IsReloading
                        ? $"\n换弹中  {sessionManager.ClientWeapon.State.ReloadTicksRemaining * tickDeltaTime:F1} 秒"
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
            scoreText.text = sessionManager.IsLocalPractice ? "单人练习" : scoreBuilder.ToString();
            scoreText.transform.parent.gameObject.SetActive(!sessionManager.IsLocalPractice);

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
                damageDirectionText.rectTransform.anchoredPosition = direction * 110f;
                damageDirectionText.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg);
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
            GameObject root = new GameObject("Deathmatch HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            var flashRoot = new GameObject("Damage Flash", typeof(RectTransform), typeof(Image));
            flashRoot.transform.SetParent(root.transform, false);
            var flashRect = flashRoot.GetComponent<RectTransform>();
            flashRect.anchorMin = Vector2.zero;
            flashRect.anchorMax = Vector2.one;
            flashRect.sizeDelta = Vector2.zero;
            damageFlash = flashRoot.GetComponent<Image>();
            damageFlash.color = new Color(1f, 0f, 0f, 0.12f);
            damageFlash.raycastTarget = false;
            damageFlash.enabled = false;

            // 以1920×1080为参考，面板各占独立区域；底部留出操作提示，不挤入顶部状态长串。
            scaler.matchWidthOrHeight = 0.5f;
            statusText = Panel(root.transform, "Match", new Vector2(0.5f, 1f), new Vector2(0f, -24f), new Vector2(360f, 90f), 26f);
            statusText.alignment = TextAlignmentOptions.Center;
            networkText = Panel(root.transform, "Network", new Vector2(0f, 1f), new Vector2(28f, -24f), new Vector2(330f, 54f), 20f);
            scoreText = Panel(root.transform, "Scores", Vector2.one, new Vector2(-28f, -24f), new Vector2(330f, 320f), 22f);
            healthText = Panel(root.transform, "Health", Vector2.zero, new Vector2(28f, 92f), new Vector2(430f, 145f), 24f);
            weaponText = Panel(root.transform, "Weapon", Vector2.right, new Vector2(-28f, 92f), new Vector2(360f, 165f), 23f);
            centerText = CreateText(root.transform, "", 38f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 180f), new Vector2(850f, 120f));
            hitMarkerText = CreateText(root.transform, "", 26f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(50f, 50f));
            damageDirectionText = CreateText(root.transform, "", 36f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(50f, 50f));
            damageDirectionText.color = new Color(1f, 0.25f, 0.18f);
            rejectionText = CreateText(root.transform, "", 23f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 130f), new Vector2(700f, 50f));
            CreateText(root.transform, "Shift 冲刺   Alt 突进   空格 跳跃/双跳   Ctrl 下蹲/滑铲   E 蹬墙反弹   右键/F 钩索   F3 网络诊断", 20f,
                TextAlignmentOptions.Center, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f),
                new Vector2(0f, 24f), new Vector2(1500f, 42f));
        }

        private static TextMeshProUGUI Panel(Transform parent, string name, Vector2 anchor, Vector2 position,
            Vector2 dimensions, float fontSize)
        {
            var target = new GameObject(name, typeof(RectTransform), typeof(Image));
            var rect = target.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            var image = target.GetComponent<Image>();
            image.color = new Color(0.025f, 0.045f, 0.065f, 0.76f);
            image.raycastTarget = false;
            var text = CreateText(rect, "", fontSize, TextAlignmentOptions.TopLeft,
                Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            text.rectTransform.offsetMin = new Vector2(18f, 12f);
            text.rectTransform.offsetMax = new Vector2(-18f, -12f);
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Truncate;
            return text;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string value, float size, TextAlignmentOptions alignment, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 dimensions)
        {
            GameObject target = new GameObject("Text", typeof(RectTransform));
            target.transform.SetParent(parent, false);
            RectTransform rect = target.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = anchorMin;
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            TextMeshProUGUI text = target.AddComponent<TextMeshProUGUI>();
            text.font = ChineseUiFont.Asset;
            text.text = value;
            text.fontSize = size;
            text.fontStyle = FontStyles.Bold;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            text.outlineWidth = 0.18f;
            text.outlineColor = new Color32(0, 0, 0, 220);
            return text;
        }
    }
}
