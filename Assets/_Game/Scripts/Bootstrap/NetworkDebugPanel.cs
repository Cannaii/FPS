using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using AFPS.NetCode.Metrics;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Sessions;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace AFPS.Bootstrap
{
    /// <summary>F3只读诊断；默认关闭，不捕获鼠标、不暂停输入，隐藏时不重建文字或扫描连接。</summary>
    public sealed class NetworkDebugPanel : MonoBehaviour
    {
        private NetworkSessionCoordinator manager;
        private UnityNetworkBootstrap bootstrap;
        private NetworkMetricsRateSampler rates = new NetworkMetricsRateSampler();
        private readonly List<ServerConnectionDiagnostics> connections = new List<ServerConnectionDiagnostics>(64);
        private readonly StringBuilder builder = new StringBuilder(1024);
        private Canvas canvas;
        private TMP_Text clientText;
        private TMP_Text serverText;
        private TMP_Text detailText;
        private double nextRefresh;
        public bool IsVisible { get; private set; }
        public bool IsConfigured => manager != null;
        public string DisplayedText => clientText == null ? string.Empty
            : clientText.text + "\n" + serverText.text + "\n" + detailText.text;

        public void Configure(NetworkSessionCoordinator owner, UnityNetworkBootstrap network)
        {
            manager = owner ?? throw new ArgumentNullException(nameof(owner));
            bootstrap = network;
            rates = new NetworkMetricsRateSampler();
            SetVisible(Array.Exists(Environment.GetCommandLineArgs(),
                value => value.Equals("-afpsNetworkDebug", StringComparison.OrdinalIgnoreCase)));
        }

        public void SetVisible(bool visible)
        {
            IsVisible = visible && manager != null;
            if (IsVisible)
            {
                EnsureUi();
                Refresh();
                nextRefresh = Time.realtimeSinceStartupAsDouble + 0.25d;
            }
            if (canvas != null)
            {
                canvas.enabled = IsVisible;
            }
        }

        private void Update()
        {
            if (manager == null)
            {
                return;
            }
            double now = Time.realtimeSinceStartupAsDouble;
            rates.Update((ulong)(now * 1000000d), manager.Metrics.SnapshotBytesSent, manager.Metrics.SnapshotPacketsSent);
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
            {
                SetVisible(!IsVisible);
            }
            // 面板4Hz刷新、速率最少0.5秒一个窗口，避免每渲染帧格式化字符串和动态字形更新。
            if (IsVisible && now >= nextRefresh)
            {
                Refresh();
                nextRefresh = now + 0.25d;
            }
        }

        private void OnDisable()
        {
            if (canvas != null)
            {
                canvas.enabled = false;
            }
            IsVisible = false;
        }

        private void OnDestroy()
        {
            if (canvas != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(canvas.gameObject);
                }
                else
                {
                    DestroyImmediate(canvas.gameObject);
                }
            }
        }

        private void Refresh()
        {
            var metrics = manager.Metrics;
            bool server = manager.IsServerRunning;
            string mode = manager.IsLocalPractice ? "单人练习（本地通道）"
                : bootstrap?.ActiveMode == NetworkLaunchMode.Host ? "主机"
                : bootstrap?.ActiveMode == NetworkLaunchMode.DedicatedServer || (server && !manager.IsClientRunning) ? "独立服务器"
                : manager.IsClientRunning ? "客户端" : "会话已停止";
            builder.Clear();
            builder.AppendLine("客户端与网络").Append("模式：").AppendLine(mode);
            builder.Append("本地实体：").Append(manager.RemotePlayers?.LocalEntityId ?? 0)
                .Append("    远端实体：").Append(manager.RemotePlayers?.Count ?? 0).AppendLine();
            builder.Append("网络时钟：").AppendLine(!manager.IsClientRunning ? "客户端未启用" : manager.IsLocalPractice ? "本地通道，无公网RTT"
                : manager.HasTimeSync ? "已同步" : "待同步或样本已过期");
            builder.Append("往返延迟 / 抖动：").AppendLine(manager.IsClientRunning && manager.HasTimeSync && !manager.IsLocalPractice
                ? Number(metrics.LastNetworkRoundTripMilliseconds) + " / " + Number(metrics.RoundTripJitterMilliseconds) + " 毫秒"
                : "—（无有效网络样本）");
            builder.Append("插值 当前 / 目标：").Append(Number(metrics.RemoteInterpolationDelayTicks))
                .Append(" / ").Append(Number(metrics.TargetRemoteInterpolationDelayTicks)).AppendLine(" Tick");
            builder.Append("预测校正：").Append(metrics.CorrectionsApplied).AppendLine(" 次");
            builder.Append("时钟 请求 / 响应 / 确认：").Append(metrics.TimeSyncRequestsSent)
                .Append(" / ").Append(metrics.TimeSyncResponsesReceived).Append(" / ")
                .Append(metrics.TimeSyncAcknowledgementsSent).AppendLine();
            builder.Append("状态上报 / 合并替换：").Append(metrics.StateHashReportsSent)
                .Append(" / ").Append(metrics.StateHashReportsSuperseded).AppendLine();
            builder.Append("客户端恢复：").AppendLine(manager.ClientRecoveryPending ? "补输入或等待确认"
                : manager.ClientSession == null ? "未连接" : "就绪");
            builder.Append("算法 / 协议：").Append(manager.WorldContext?.Identity.AlgorithmVersion ?? 0)
                .Append(" / ").Append(PacketHeader.CurrentProtocolVersion);
            clientText.text = builder.ToString();

            builder.Clear();
            builder.AppendLine("服务器与远端快照");
            if (!server)
            {
                builder.AppendLine("当前进程未运行服务器").AppendLine("服务器模拟耗时、发送流量与输入窗口")
                    .AppendLine("请在主机或独立服务器查看。");
            }
            else
            {
                builder.Append("在线会话：").Append(manager.ServerSessionCount).AppendLine();
                builder.Append("模拟 最近 / 峰值：").Append(metrics.LastServerTickMicroseconds)
                    .Append(" / ").Append(metrics.MaximumServerTickMicroseconds).AppendLine(" 微秒");
                builder.Append("快照速率：").AppendLine(rates.HasRate
                    ? Number(rates.PacketsPerSecond) + " 包/秒    " + Number(rates.BytesPerSecond / 1024d) + " 千字节/秒"
                    : "采样中…");
                builder.Append("快照累计：").Append(metrics.SnapshotPacketsSent).Append(" 包    ")
                    .Append(metrics.SnapshotBytesSent).AppendLine(" 字节（非总流量）");
                builder.Append("射击结果：").Append(metrics.ShotResultsSent).AppendLine(" 条");
                builder.Append("回溯查询：").Append(metrics.RewindQueries).Append(" 次    最近 ")
                    .Append(metrics.LastRewindMicroseconds).AppendLine(" 微秒");
                builder.Append("等待 / 持续复用 / 中性替代：").Append(metrics.ServerInputWaits).Append(" / ")
                    .Append(metrics.ServerRepeatedInputTicks).Append(" / ").Append(metrics.ServerNeutralInputTicks).AppendLine(" Tick");
                builder.Append("服务器往返采样：").Append(metrics.ServerRoundTripSamplesReceived).AppendLine(" 次");
                builder.Append("核对一致 / 状态差异：").Append(metrics.StateHashMatches).Append(" / ")
                    .Append(metrics.StateHashMismatches).AppendLine();
                builder.Append("输入差异 / 历史缺失 / 拒绝：").Append(metrics.StateHashDifferentInputs).Append(" / ")
                    .Append(metrics.StateHashHistoryMisses).Append(" / ").Append(metrics.StateHashReportsRejected);
            }
            serverText.text = builder.ToString();

            manager.CopyServerDiagnostics(connections);
            builder.Clear();
            builder.Append("连接诊断（实体排序，显示前4 / 共").Append(connections.Count).AppendLine("）");
            for (int index = 0; index < System.Math.Min(4, connections.Count); index++)
            {
                var entry = connections[index];
                builder.Append("实体 ").Append(entry.EntityId).Append("    输入Tick ").Append(entry.InputTick)
                    .Append("    等待 / 复用上限 ").Append(entry.WaitLimitTicks).Append(" / ")
                    .Append(entry.RepeatLimitTicks).Append(" Tick    往返 / 抖动 ")
                    .AppendLine(manager.IsLocalPractice ? "本地通道" : entry.HasFreshQuality
                        ? Number(entry.RoundTripMilliseconds) + " / " + Number(entry.JitterMilliseconds) + " 毫秒"
                        : "待测或已过期");
            }
            var drift = manager.LastStateDrift;
            builder.AppendLine(drift.Client.EntityId == 0 ? "最近状态漂移：无记录" :
                "最近状态漂移：实体 " + drift.Client.EntityId + "，输入Tick " + drift.Client.InputTick);
            if (drift.Client.EntityId != 0)
            {
                builder.Append("客户端 / 权威哈希：").Append(drift.Client.StateHash.ToString("X16"))
                    .Append(" / ").Append(drift.ServerStateHash.ToString("X16"));
            }
            detailText.text = builder.ToString();
        }

        private void EnsureUi()
        {
            if (canvas != null)
            {
                return;
            }
            var root = new GameObject("Network Debug UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            root.transform.SetParent(transform, false);
            canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            var panel = new GameObject("Diagnostics Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(root.transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.06f, 0.17f);
            rect.anchorMax = new Vector2(0.94f, 0.86f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.025f, 0.045f, 0.065f, 0.98f);
            panel.GetComponent<Image>().raycastTarget = false;
            Label(rect, "网络诊断    F3 收起    ·    只读，不暂停对局", 27f,
                new Vector2(0f, 1f), Vector2.one, new Vector2(24f, -64f), new Vector2(-24f, -12f));
            clientText = Label(rect, "", 20f, new Vector2(0f, 0.36f), new Vector2(0.5f, 1f),
                new Vector2(24f, 0f), new Vector2(-12f, -72f));
            serverText = Label(rect, "", 20f, new Vector2(0.5f, 0.36f), Vector2.one,
                new Vector2(12f, 0f), new Vector2(-24f, -72f));
            detailText = Label(rect, "", 20f, Vector2.zero, new Vector2(1f, 0.36f),
                new Vector2(24f, 20f), new Vector2(-24f, -4f));
        }

        private static TMP_Text Label(Transform parent, string value, float size, Vector2 minimum,
            Vector2 maximum, Vector2 offsetMinimum, Vector2 offsetMaximum)
        {
            var text = new GameObject("Diagnostic Text", typeof(RectTransform)).AddComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false);
            text.rectTransform.anchorMin = minimum;
            text.rectTransform.anchorMax = maximum;
            text.rectTransform.offsetMin = offsetMinimum;
            text.rectTransform.offsetMax = offsetMaximum;
            text.font = ChineseUiFont.Asset;
            text.fontSize = size;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.color = new Color(0.87f, 0.94f, 1f);
            text.raycastTarget = false;
            text.richText = false;
            text.enableWordWrapping = true;
            text.text = value;
            return text;
        }

        private static string Number(double value) => value.ToString("F1", CultureInfo.InvariantCulture);
    }
}
