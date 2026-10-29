using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using AFPS.Math;
using AFPS.NetCode.Match;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Sessions;
using AFPS.NetCode.Transport;
using AFPS.Simulation.Characters;
using AFPS.Transport.Unity;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace AFPS.Verification
{
    /// <summary>独立进程负载探针；服务器和机器人进程共用正式UTP/会话，不绕过世界准入或准备。</summary>
    public sealed class ServerLoadProbe : MonoBehaviour
    {
        public const int PlayerCount = 32;
        private const double TickSeconds = 0.02; // 真实秒，50Hz；模拟仍使用规范化Q16.16 Step50Hz。
        private const int WarmupTicks = 150; // 32人正式Playing后的世界Tick，排除连接/准备和首次执行开销。
        private readonly byte[] receiveBuffer = new byte[2048];
        private readonly List<GameNetworkRuntime> runtimes = new List<GameNetworkRuntime>();
        private readonly List<NetworkMovementSessionManager> managers = new List<NetworkMovementSessionManager>();
        private readonly Stopwatch lifetime = Stopwatch.StartNew();
        private readonly uint[] shotSequences = new uint[PlayerCount];
        private NetworkMovementSessionManager server;
        private double accumulator;
        private uint worldTick;
        private uint startedMatchTick;
        private bool hasMatch;
        private bool finished;
        private string role;
        private string scenario;
        private string output;
        private string stopPath;
        private int groupCount;
        private int sampleTicks;
        private int count;
        private int minimumOnline = PlayerCount;
        private int minimumRemote = PlayerCount - 1;
        private int frames;
        private long firstSampleTime;
        private long lastSampleTime;
        private long initialMemory;
        private long peakMemory;
        private int initialGc;
        private ulong initialBytes;
        private ulong initialPackets;
        private ulong initialShots;
        private ulong initialNeutral;
        private ulong initialRepeated;
        private ulong initialRewinds;
        private ulong simulations;
        private ulong generatedInputs;
        private int clientMeasuredSteps;
        private double clientStepTotal;
        private double clientStepMaximum;
        private double[] tickSamples;
        private double[] outerSamples;
        private double[] pumpSamples;
        private uint[] ticks;
        private bool sawAllPlaying;

        [Serializable]
        public sealed class Result
        {
            public string role;
            public string scenario;
            public bool passed;
            public bool correctnessPassed;
            public bool budgetPassed;
            public string error;
            public string processor;
            public string unityVersion;
            public string worldHash;
            public string configurationHash;
            public int players;
            public int minimumOnline;
            public int minimumRemote;
            public int frames;
            public int samples;
            public double effectiveTickHz;
            public double inputCoverage;
            public ulong generatedInputs;
            public ulong snapshotPackets;
            public ulong snapshotBytes;
            public ulong shotResultPackets;
            public ulong neutralInputs;
            public ulong repeatedInputs;
            public ulong rewindQueries;
            public double clientMeanStepMicroseconds;
            public double clientMaximumStepMicroseconds;
            public long managedBytesDelta;
            public long peakManagedBytes;
            public int gen0Collections;
            public bool timeSynced;
            public LoadTestStatistics serverTick;
            public LoadTestStatistics fullStep;
            public LoadTestStatistics pump;
        }

        private SimulationWorldContext context;

        private void Start()
        {
            if (Application.isEditor)
            {
                enabled = false;
                return;
            }
            try
            {
                role = Argument("-afpsLoadRole");
                scenario = Argument("-afpsLoadScenario");
                output = Argument("-afpsLoadOutput");
                stopPath = Argument("-afpsLoadStop");
                ushort port = ushort.Parse(Argument("-afpsLoadPort"), CultureInfo.InvariantCulture);
                sampleTicks = int.Parse(Argument("-afpsLoadSamples"), CultureInfo.InvariantCulture);
                groupCount = int.Parse(Argument("-afpsLoadGroupCount"), CultureInfo.InvariantCulture);
                if ((role != "Server" && role != "Clients") || (scenario != "Idle" && scenario != "Movement" && scenario != "Combat")
                    || sampleTicks < 100 || sampleTicks > 3000 || groupCount < 1 || groupCount > PlayerCount)
                {
                    throw new ArgumentException("非法压测角色/场景/采样数量。");
                }
                Application.runInBackground = true;
                Application.targetFrameRate = 100;
                QualitySettings.vSyncCount = 0;
                var spatial = new SimulationSessionSpatialConfig(
                    new FixVec3(Fix.FromInt(-20), Fix.Zero, Fix.FromInt(-26)), Fix.FromRatio(5, 4), Fix.FromInt(150));
                var snapshot = Resources.Load<TextAsset>("AFPSLoadWorld");
                if (snapshot == null)
                {
                    throw new InvalidOperationException("缺少训练场FixedWorld快照。");
                }
                context = SimulationWorldContext.Load(new ArraySegment<byte>(snapshot.bytes),
                    new PlayerSimulationConfig(6f, 20f, 20f, 8f), SimulationStep.Step50Hz, 1 << 8, spatial);
                if (role == "Server")
                {
                    tickSamples = new double[sampleTicks];
                    outerSamples = new double[sampleTicks];
                    pumpSamples = new double[sampleTicks];
                    ticks = new uint[sampleTicks];
                    var runtime = StartRuntime(NetworkLaunchMode.DedicatedServer, port);
                    server = CreateManager(runtime.ServerTransport, null);
                    managers.Add(server);
                    File.WriteAllText(Argument("-afpsLoadReady"), "listening");
                }
                else
                {
                    for (int index = 0; index < groupCount; index++)
                    {
                        var runtime = StartRuntime(NetworkLaunchMode.Client, port);
                        managers.Add(CreateManager(null, runtime.ClientTransport));
                    }
                }
            }
            catch (Exception exception)
            {
                Finish(exception.ToString());
            }
        }

        private GameNetworkRuntime StartRuntime(NetworkLaunchMode mode, ushort port)
        {
            var runtime = new GameNetworkRuntime(() => new UnityGameTransport());
            runtimes.Add(runtime);
            if (!runtime.TryStart(new NetworkLaunchOptions(mode, "127.0.0.1", port, PlayerCount), out string error))
            {
                throw new InvalidOperationException(error);
            }
            return runtime;
        }

        private NetworkMovementSessionManager CreateManager(IGameTransport serverTransport, IGameTransport clientTransport)
        {
            var initial = new PlayerState { PositionFixed = context.SessionSpatial.SpawnOrigin, IsGrounded = true };
            return new NetworkMovementSessionManager(serverTransport, clientTransport, initial, initial,
                context.Configuration, 0.02f, 128, 3, 64, 2, 2, 0f, 0f,
                serverSpawnSpacing: context.SessionSpatial.SpawnSpacing.ToFloat(),
                deathmatchKillLimit: 9999, deathmatchDurationSeconds: 300f, simulationWorld: context);
        }

        private void Update()
        {
            if (Application.isEditor || finished || context == null)
            {
                return;
            }
            try
            {
                if (lifetime.Elapsed.TotalSeconds > 120)
                {
                    throw new TimeoutException("32人准入/准备/负载采样未在120秒内完成。");
                }
                if (role == "Clients" && File.Exists(stopPath))
                {
                    Finish(null);
                    return;
                }
                long pumpStart = Stopwatch.GetTimestamp();
                foreach (var runtime in runtimes)
                {
                    runtime.Pump();
                }
                for (int index = 0; index < runtimes.Count; index++)
                {
                    Drain(role == "Server" ? runtimes[index].ServerTransport : runtimes[index].ClientTransport,
                        managers[index], role == "Server" ? NetworkTransportSide.Server : NetworkTransportSide.Client);
                }
                double pumpMicroseconds = Microseconds(Stopwatch.GetTimestamp() - pumpStart);
                frames++;
                accumulator += Time.unscaledDeltaTime;
                for (int catchUp = 0; accumulator >= TickSeconds && catchUp < 8 && !finished; catchUp++)
                {
                    accumulator -= TickSeconds;
                    worldTick++;
                    if (role == "Server")
                    {
                        AdvanceServer(pumpMicroseconds);
                    }
                    else
                    {
                        long clientStarted = Stopwatch.GetTimestamp();
                        AdvanceClients();
                        if (hasMatch && worldTick - startedMatchTick >= WarmupTicks)
                        {
                            double elapsed = Microseconds(Stopwatch.GetTimestamp() - clientStarted);
                            clientStepTotal += elapsed;
                            clientStepMaximum = System.Math.Max(clientStepMaximum, elapsed);
                            clientMeasuredSteps++;
                        }
                    }
                }
                foreach (var manager in managers)
                {
                    manager.AdvanceTimeSync(worldTick, (float)System.Math.Min(1d, accumulator / TickSeconds));
                }
            }
            catch (Exception exception)
            {
                Finish(exception.ToString());
            }
        }

        private void AdvanceServer(double pumpMicroseconds)
        {
            bool sampling = hasMatch && worldTick - startedMatchTick >= WarmupTicks;
            if (sampling && count == 0)
            {
                // 先保存计数器，再执行第一个采样Tick，避免漏掉首Tick的替代输入/复制计数。
                initialMemory = GC.GetTotalMemory(false);
                initialGc = GC.CollectionCount(0);
                initialBytes = server.Metrics.SnapshotBytesSent;
                initialPackets = server.Metrics.SnapshotPacketsSent;
                initialShots = server.Metrics.ShotResultsSent;
                initialNeutral = server.Metrics.ServerNeutralInputTicks;
                initialRepeated = server.Metrics.ServerRepeatedInputTicks;
                initialRewinds = server.Metrics.RewindQueries;
            }
            long before = Stopwatch.GetTimestamp();
            int advanced = server.AdvanceServerSessions(worldTick);
            double elapsed = Microseconds(Stopwatch.GetTimestamp() - before);
            if (!hasMatch && server.ServerSessionCount == PlayerCount && server.ServerDeathmatchPhase == DeathmatchPhase.Playing)
            {
                hasMatch = true;
                startedMatchTick = worldTick;
            }
            if (!sampling)
            {
                return;
            }
            if (count == 0)
            {
                firstSampleTime = before;
            }
            minimumOnline = System.Math.Min(minimumOnline, server.ServerSessionCount);
            simulations += (uint)advanced;
            tickSamples[count] = server.Metrics.LastServerTickMicroseconds;
            outerSamples[count] = elapsed;
            pumpSamples[count] = pumpMicroseconds;
            ticks[count] = worldTick;
            lastSampleTime = before;
            peakMemory = System.Math.Max(peakMemory, GC.GetTotalMemory(false));
            count++;
            if (count == sampleTicks)
            {
                Finish(null);
            }
        }

        private void AdvanceClients()
        {
            for (int index = 0; index < managers.Count; index++)
            {
                var manager = managers[index];
                manager.AdvanceServerSessions(worldTick);
                if (manager.ClientLobby?.PlayerCount == PlayerCount && manager.TryGetLocalLobbyPlayer(out var member)
                    && !member.Ready && !manager.LobbyReadyPending && manager.ClientLobby.Phase == LobbyPhase.Waiting)
                {
                    manager.TrySetLobbyReady(true);
                }
                if (manager.CanEnterMatch && hasMatch && worldTick - startedMatchTick >= WarmupTicks)
                {
                    minimumRemote = System.Math.Min(minimumRemote, manager.RemotePlayers.Count);
                }
                if (!manager.CanPredictClientInput)
                {
                    continue;
                }
                uint tick = unchecked(manager.ClientSession.CurrentState.Tick + 1);
                var command = new PlayerInputCommand { Tick = tick };
                if (manager.CanEnterMatch)
                {
                    if (scenario != "Idle")
                    {
                        // 每100输入Tick换向，200Tick为一个往返周期，保持在训练场附近。
                        command.MoveY = tick % 200 < 100 ? 1f : -1f;
                    }
                    if (scenario == "Combat")
                    {
                        command.LookYaw = manager.RemotePlayers.LocalEntityId % 2 == 0 ? 270f : 90f;
                        command.ReloadPressed = manager.ClientWeapon.CanReload && manager.ClientWeapon.Ammo <= 5;
                        if (!command.ReloadPressed && manager.ClientWeapon.CanFire && tick % 6 == 0)
                        {
                            command.FirePressed = true;
                            command.ShotSequence = ++shotSequences[index];
                            command.ShotServerTick = worldTick;
                        }
                    }
                }
                manager.TryPredictAndSend(command, out _, out _);
                generatedInputs++;
            }
            bool allPlaying = true;
            foreach (var manager in managers)
            {
                allPlaying &= manager.CanEnterMatch;
            }
            sawAllPlaying |= allPlaying;
            if (allPlaying && !hasMatch)
            {
                hasMatch = true;
                startedMatchTick = worldTick;
            }
        }

        private void Drain(IGameTransport transport, NetworkMovementSessionManager manager, NetworkTransportSide side)
        {
            while (transport.TryPollEvent(new ArraySegment<byte>(receiveBuffer), out var message))
            {
                switch (message.Type)
                {
                    case TransportEventType.Connected:
                        manager.HandleConnected(side, message.ConnectionId);
                        break;
                    case TransportEventType.Disconnected:
                        manager.HandleDisconnected(side, message.ConnectionId);
                        break;
                    case TransportEventType.Data:
                        manager.TryHandleData(side, message.ConnectionId,
                            new ArraySegment<byte>(receiveBuffer, 0, message.PayloadLength), out _);
                        break;
                }
            }
        }

        private void Finish(string error)
        {
            if (finished)
            {
                return;
            }
            finished = true;
            var result = new Result
            {
                role = role,
                scenario = scenario,
                error = error,
                processor = SystemInfo.processorType,
                unityVersion = Application.unityVersion,
                worldHash = context?.Identity.WorldHash.ToString("X16"),
                configurationHash = context?.Identity.ConfigurationHash.ToString("X16"),
                players = role == "Server" ? PlayerCount : groupCount,
                minimumOnline = minimumOnline,
                minimumRemote = minimumRemote,
                frames = frames,
                samples = count,
                generatedInputs = generatedInputs
            };
            if (role == "Server" && count > 0)
            {
                result.serverTick = LoadTestStatistics.Compute(tickSamples, count, 20000);
                result.fullStep = LoadTestStatistics.Compute(outerSamples, count, 20000);
                result.pump = LoadTestStatistics.Compute(pumpSamples, count, 20000);
                result.effectiveTickHz = (count - 1) * (double)Stopwatch.Frequency / (lastSampleTime - firstSampleTime);
                result.snapshotBytes = server.Metrics.SnapshotBytesSent - initialBytes;
                result.snapshotPackets = server.Metrics.SnapshotPacketsSent - initialPackets;
                result.shotResultPackets = server.Metrics.ShotResultsSent - initialShots;
                result.neutralInputs = server.Metrics.ServerNeutralInputTicks - initialNeutral;
                result.repeatedInputs = server.Metrics.ServerRepeatedInputTicks - initialRepeated;
                result.rewindQueries = server.Metrics.RewindQueries - initialRewinds;
                result.inputCoverage = (simulations - result.neutralInputs - result.repeatedInputs)
                    / (double)(count * PlayerCount);
                result.managedBytesDelta = GC.GetTotalMemory(false) - initialMemory;
                result.peakManagedBytes = peakMemory;
                result.gen0Collections = GC.CollectionCount(0) - initialGc;
                result.correctnessPassed = error == null && count == sampleTicks && minimumOnline == PlayerCount
                    && result.inputCoverage >= 0.98 && result.snapshotPackets > 0
                    && (scenario != "Combat" || (result.shotResultPackets > 0 && result.rewindQueries > 0));
                // 50Hz的20ms步预算；Tick p99与实际推进频率分别裁决，不能只看平均值。
                result.budgetPassed = result.serverTick.p99Microseconds <= 20000 && result.effectiveTickHz >= 47.5;
                var csv = new StringBuilder("worldTick,serverTickUs,fullStepUs,pumpUs\n");
                for (int index = 0; index < count; index++)
                {
                    csv.Append(ticks[index]).Append(',').Append(tickSamples[index].ToString("R", CultureInfo.InvariantCulture))
                        .Append(',').Append(outerSamples[index].ToString("R", CultureInfo.InvariantCulture))
                        .Append(',').Append(pumpSamples[index].ToString("R", CultureInfo.InvariantCulture)).Append('\n');
                }
                File.WriteAllText(output + ".ticks.csv", csv.ToString());
            }
            else if (role == "Clients")
            {
                result.clientMeanStepMicroseconds = clientMeasuredSteps == 0 ? 0 : clientStepTotal / clientMeasuredSteps;
                result.clientMaximumStepMicroseconds = clientStepMaximum;
                result.timeSynced = true;
                foreach (var manager in managers)
                {
                    result.timeSynced &= manager.HasTimeSync;
                }
                result.correctnessPassed = error == null && sawAllPlaying && minimumRemote == PlayerCount - 1
                    && generatedInputs >= (ulong)(sampleTicks * groupCount) && result.timeSynced && clientMeasuredSteps > 0;
                result.budgetPassed = true;
            }
            result.passed = result.correctnessPassed && result.budgetPassed;
            try
            {
                File.WriteAllText(output, JsonUtility.ToJson(result, true));
                if (role == "Server")
                {
                    File.WriteAllText(stopPath, "finished");
                }
                Debug.Log("[AFPS_LOAD_RESULT] " + JsonUtility.ToJson(result));
            }
            finally
            {
                StopRuntimes();
            }
            Application.Quit(result.passed ? 0 : 2);
        }

        private void StopRuntimes()
        {
            foreach (var runtime in runtimes)
            {
                runtime.Dispose();
            }
        }

        private void OnDestroy()
        {
            StopRuntimes();
        }

        private static double Microseconds(long ticks) => ticks * 1000000d / Stopwatch.Frequency;

        private static string Argument(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, key);
            if (index < 0 || index + 1 == args.Length)
            {
                throw new ArgumentException("缺少压测参数：" + key);
            }
            return args[index + 1];
        }
    }
}
