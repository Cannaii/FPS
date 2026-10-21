using System.Text;
using AFPS.NetCode.Match;
using AFPS.NetCode.Sessions;
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

        public void Configure(NetworkMovementSessionManager manager, float tickSeconds)
        {
            sessionManager = manager;
            tickDeltaTime = tickSeconds;
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
            if (canvas != null) canvas.enabled = visible;
            if (!visible) return;

            uint localEntityId = sessionManager.RemotePlayers != null ? sessionManager.RemotePlayers.LocalEntityId : 0;
            match.TryGetPlayer(localEntityId, out DeathmatchPlayerState local);
            int seconds = Mathf.CeilToInt(match.RemainingTicks * tickDeltaTime);
            statusText.text = match.Phase == DeathmatchPhase.Playing
                ? $"{seconds / 60:00}:{seconds % 60:00}   HP {local.Health:000}   K {local.Kills} / D {local.Deaths}"
                : $"HP {local.Health:000}   K {local.Kills} / D {local.Deaths}";

            scoreBuilder.Clear();
            scoreBuilder.AppendLine("DEATHMATCH");
            for (int i = 0; i < match.PlayerCount; i++)
            {
                DeathmatchPlayerState player = match.GetPlayer(i);
                scoreBuilder.Append(player.EntityId == localEntityId ? "> " : "  ");
                scoreBuilder.Append("P").Append(player.EntityId).Append("   ").Append(player.Kills).Append(" / ").Append(player.Deaths);
                if (!player.IsAlive) scoreBuilder.Append("   DEAD");
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

        private void EnsureUi()
        {
            if (canvas != null) return;
            GameObject root = new GameObject("Deathmatch HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 20;
            CanvasScaler scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            statusText = CreateText(root.transform, "", 28f, TextAlignmentOptions.Top, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -22f), new Vector2(900f, 60f));
            scoreText = CreateText(root.transform, "", 23f, TextAlignmentOptions.TopLeft, Vector2.one, Vector2.one, new Vector2(-220f, -28f), new Vector2(400f, 360f));
            centerText = CreateText(root.transform, "", 38f, TextAlignmentOptions.Center, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 160f));
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
