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
            bool visible = match != null;
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
            statusText.text = match.Phase == DeathmatchPhase.Playing
                ? $"{seconds / 60:00}:{seconds % 60:00}   HP {local.Health:000}   K {local.Kills} / D {local.Deaths}"
                : $"HP {local.Health:000}   K {local.Kills} / D {local.Deaths}";

            statusText.text += sessionManager.HasTimeSync
                ? $"   RTT {sessionManager.Metrics.LastNetworkRoundTripMilliseconds:F1} ms"
                : "   RTT --";
            if (sessionManager.IsLocalPractice)
            {
                statusText.text = $"PRACTICE   HP {local.Health:000}";
            }
            if (sessionManager.ClientSession != null)
            {
                var movement = sessionManager.ClientSession.CurrentState;
                int stamina = Mathf.RoundToInt((1f - movement.StaminaSpentFixed.ToFloat()) * 100f);
                statusText.text += $"   STAMINA {stamina:000} [SHIFT]";
                int maximumDashCharges = sessionManager.WorldContext?.Configuration.Dash.MaxCharges
                    ?? AFPS.Simulation.Characters.PlayerDashConfig.Default.MaxCharges;
                statusText.text += $"   DASH {maximumDashCharges - movement.DashSpentCharges} [ALT]";
                statusText.text += movement.SlideRemainingFixed > AFPS.Math.Fix.Zero
                    ? "   SLIDE [CTRL]" : movement.IsCrouched ? "   CROUCH [CTRL]" : "   CROUCH [CTRL] / BOUNCE [E]";
                if (movement.IsWallRunning)
                {
                    statusText.text += "   WALL RUN [SHIFT]";
                }
                if (movement.SprintExhausted)
                {
                    statusText.text += "   EXHAUSTED";
                }
            }

            if (sessionManager.ClientWeapon.HasState)
            {
                statusText.text += sessionManager.ClientWeaponConfiguration.FireMode == AFPS.Simulation.Weapons.WeaponFireMode.Projectile
                    ? "   LAUNCHER [Q]" : "   RIFLE [Q]";
                statusText.text += $"   AMMO {sessionManager.ClientWeapon.Ammo}/{sessionManager.ClientWeapon.State.Capacity}";
                if (sessionManager.ClientWeapon.IsSwitchPending || sessionManager.ClientWeapon.State.IsEquipping)
                {
                    statusText.text += "   EQUIPPING";
                }
                if (sessionManager.ClientWeapon.IsReloading)
                {
                    statusText.text += sessionManager.ClientWeapon.State.IsReloading
                        ? $"   RELOADING {sessionManager.ClientWeapon.State.ReloadTicksRemaining * tickDeltaTime:F1}s"
                        : "   RELOADING...";
                }
            }
            scoreBuilder.Clear();
            scoreBuilder.AppendLine("DEATHMATCH");
            for (int i = 0; i < match.PlayerCount; i++)
            {
                DeathmatchPlayerState player = match.GetPlayer(i);
                scoreBuilder.Append(player.EntityId == localEntityId ? "> " : "  ");
                scoreBuilder.Append("P").Append(player.EntityId).Append("   ").Append(player.Kills).Append(" / ").Append(player.Deaths);
                if (!player.IsAlive)
                {
                    scoreBuilder.Append("   DEAD");
                }
                scoreBuilder.AppendLine();
            }
            scoreText.text = scoreBuilder.ToString();

            switch (match.Phase)
            {
                case DeathmatchPhase.WaitingForPlayers:
                    centerText.text = "WAITING FOR ANOTHER PLAYER";
                    break;
                case DeathmatchPhase.Finished:
                    centerText.text = match.WinnerEntityId == 0 ? "DRAW" : match.WinnerEntityId == localEntityId ? "VICTORY" : $"PLAYER {match.WinnerEntityId} WINS";
                    break;
                default:
                    centerText.text = local.IsAlive ? string.Empty : $"RESPAWNING IN {Mathf.Max(1, Mathf.CeilToInt(local.RespawnTicksRemaining * tickDeltaTime))}";
                    break;
            }
        }

        private void UpdateCombatFeedback()
        {
            bool damaged = shotResults != null && shotResults.DamageFeedbackVisible;
            damageFlash.enabled = damaged;
            damageDirectionText.text = damaged ? "^" : string.Empty;
            var camera = UnityEngine.Camera.main;
            if (damaged && camera != null)
            {
                Vector3 direction = camera.transform.InverseTransformDirection(shotResults.LastDamageSource - camera.transform.position);
                float angle = Mathf.Atan2(direction.x, direction.z);
                damageDirectionText.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(angle), Mathf.Cos(angle)) * 90f;
                damageDirectionText.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -angle * Mathf.Rad2Deg);
            }
            rejectionText.text = shotResults != null && shotResults.RejectionFeedbackVisible
                ? shotResults.LastRejectionReason switch
                {
                    WeaponFireRejectReason.Reloading => "RELOADING",
                    WeaponFireRejectReason.EmptyMagazine => "RELOAD",
                    WeaponFireRejectReason.FireRateLimited => "WAIT TO FIRE",
                    WeaponFireRejectReason.WeaponUnavailable => "WEAPON UNAVAILABLE",
                    _ => "SHOT REJECTED"
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

            statusText = CreateText(root.transform, "", 28f, TextAlignmentOptions.Top, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(1500f, 60f));
            scoreText = CreateText(root.transform, "", 23f, TextAlignmentOptions.TopLeft, Vector2.one, Vector2.one, new Vector2(-220f, -28f), new Vector2(400f, 360f));
            centerText = CreateText(root.transform, "", 38f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 160f));
            hitMarkerText = CreateText(root.transform, "", 26f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(50f, 50f));
            damageDirectionText = CreateText(root.transform, "", 40f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(50f, 50f));
            damageDirectionText.color = Color.red;
            rejectionText = CreateText(root.transform, "", 22f, TextAlignmentOptions.Center,
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -80f), new Vector2(400f, 50f));
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
