using System;
using System.Net;
using System.Net.Sockets;
using AFPS.Math;
using AFPS.NetCode.Runtime;
using AFPS.NetCode.Sessions;
using AFPS.NetCode.Transport;
using AFPS.Simulation.Characters;
using AFPS.Simulation.World;
using AFPS.Transport.Unity;
using UnityEngine;

namespace AFPS.Tests.PlayMode
{
    /// <summary>仅测试装配：由真实Update驱动UTP和50Hz会话，不跳过准入/准备，也不反射修改权威状态。</summary>
    public sealed class PlayModeNetworkRig : MonoBehaviour
    {
        private const double StepSeconds = 0.02;
        private readonly byte[] receiveBuffer = new byte[2048];
        private readonly GameNetworkRuntime[] clients = new GameNetworkRuntime[2];
        private readonly System.Collections.Generic.List<TransportConnectionId> serverConnections =
            new System.Collections.Generic.List<TransportConnectionId>();
        private GameNetworkRuntime authority;
        private double accumulatedSeconds;
        private int previousFrameRate;
        private int previousVSync;
        private bool configured;
        private bool stopped;

        public NetworkMovementSessionManager Server { get; private set; }
        public NetworkMovementSessionManager[] Clients { get; } = new NetworkMovementSessionManager[2];
        public Func<int, uint, PlayerInputCommand> Input { get; set; }
        public uint WorldTick { get; private set; }
        public int PumpedFrames { get; private set; }
        public Exception Fault { get; private set; }
        public ushort Port { get; private set; }
        public bool IsStopped => stopped;

        public void Configure(bool host)
        {
            previousFrameRate = Application.targetFrameRate;
            previousVSync = QualitySettings.vSyncCount;
            configured = true;
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            // 系统选临时UDP端口；马上释放后交给UTP绑定。绑定竞争明确失败，不跳过测试或修改防火墙。
            using (var socket = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)))
            {
                Port = (ushort)((IPEndPoint)socket.Client.LocalEndPoint).Port;
            }
            var world = new FixedWorld(null, new[]
            {
                new FixedWorldPlane(1, 0, FixedSurface.Solid | FixedSurface.Ground,
                    FixedAxis.Y, Fix.Zero, true)
            });
            var configuration = new PlayerSimulationConfig(6f, 20f, 20f, 8f);
            var context = new SimulationWorldContext(world, configuration, SimulationStep.Step50Hz);
            authority = StartRuntime(host ? NetworkLaunchMode.Host : NetworkLaunchMode.DedicatedServer);
            Server = CreateManager(authority.ServerTransport, null, context);
            for (int index = 0; index < clients.Length; index++)
            {
                clients[index] = host && index == 0 ? authority : StartRuntime(NetworkLaunchMode.Client);
                Clients[index] = CreateManager(null, clients[index].ClientTransport, context);
            }
        }

        private GameNetworkRuntime StartRuntime(NetworkLaunchMode mode)
        {
            var runtime = new GameNetworkRuntime(() => new UnityGameTransport());
            if (!runtime.TryStart(new NetworkLaunchOptions(mode, "127.0.0.1", Port, 4), out string error))
            {
                runtime.Dispose();
                throw new InvalidOperationException(error);
            }
            return runtime;
        }

        private static NetworkMovementSessionManager CreateManager(
            IGameTransport server, IGameTransport client, SimulationWorldContext context)
        {
            var initial = new PlayerState { PositionFixed = context.SessionSpatial.SpawnOrigin, IsGrounded = true };
            // 只缩短夹具已有的重生配置为0.5秒以减少等待；正式准备倒计时仍完整3秒。
            return new NetworkMovementSessionManager(server, client, initial, initial, context.Configuration,
                0.02f, 128, 3, 32, 2, 2, 0f, 0f, simulationWorld: context,
                deathmatchKillLimit: 99, deathmatchRespawnSeconds: 0.5f);
        }

        private void Update()
        {
            if (Server == null || stopped || Fault != null)
            {
                return;
            }
            try
            {
                authority.Pump();
                for (int index = 0; index < clients.Length; index++)
                {
                    if (!ReferenceEquals(clients[index], authority))
                    {
                        clients[index].Pump();
                    }
                }
                Drain(authority.ServerTransport, Server, NetworkTransportSide.Server);
                for (int index = 0; index < clients.Length; index++)
                {
                    Drain(clients[index].ClientTransport, Clients[index], NetworkTransportSide.Client);
                }
                PumpedFrames++;
                accumulatedSeconds += Time.unscaledDeltaTime;
                // 单帧至多追8Tick，保留积压，不靠加速时钟缩短正式3秒倒计时。
                for (int catchUp = 0; accumulatedSeconds >= StepSeconds && catchUp < 8; catchUp++)
                {
                    accumulatedSeconds -= StepSeconds;
                    WorldTick++;
                    for (int index = 0; index < clients.Length; index++)
                    {
                        var manager = Clients[index];
                        manager.AdvanceServerSessions(WorldTick);
                        if (manager.CanPredictClientInput)
                        {
                            uint tick = unchecked(manager.ClientSession.CurrentState.Tick + 1);
                            var command = Input != null ? Input(index, tick) : default;
                            command.Tick = tick;
                            manager.TryPredictAndSend(command, out _, out _);
                        }
                    }
                    Server.AdvanceServerSessions(WorldTick);
                }
                float alpha = (float)System.Math.Min(1d, accumulatedSeconds / StepSeconds);
                Server.AdvanceTimeSync(WorldTick, alpha);
                foreach (var manager in Clients)
                {
                    manager.AdvanceTimeSync(WorldTick, alpha);
                }
            }
            catch (Exception exception)
            {
                // 留给协程断言原始异常，避免Update反复抛错后只留下等待超时。
                Fault = exception;
            }
        }

        private void Drain(IGameTransport transport, NetworkMovementSessionManager manager, NetworkTransportSide side)
        {
            if (transport == null)
            {
                return;
            }
            while (transport.TryPollEvent(new ArraySegment<byte>(receiveBuffer), out var message))
            {
                switch (message.Type)
                {
                    case TransportEventType.Connected:
                        if (side == NetworkTransportSide.Server)
                        {
                            serverConnections.Add(message.ConnectionId);
                        }
                        manager.HandleConnected(side, message.ConnectionId);
                        break;
                    case TransportEventType.Disconnected:
                        if (side == NetworkTransportSide.Server)
                        {
                            serverConnections.Remove(message.ConnectionId);
                        }
                        manager.HandleDisconnected(side, message.ConnectionId);
                        break;
                    case TransportEventType.Data:
                        manager.TryHandleData(side, message.ConnectionId,
                            new ArraySegment<byte>(receiveBuffer, 0, message.PayloadLength), out _);
                        break;
                }
            }
        }

        public TransportConnectionId FindServerConnection(uint entity)
        {
            foreach (var connection in serverConnections)
            {
                if (Server.TryGetServerEntityId(connection, out uint assigned) && assigned == entity)
                {
                    return connection;
                }
            }
            throw new InvalidOperationException("没有找到权威玩家连接。");
        }

        public void DisconnectClient(int index)
        {
            uint entity = Clients[index].RemotePlayers.LocalEntityId;
            var connection = FindServerConnection(entity);
            authority.ServerTransport.Disconnect(connection);
            // UTP本地主动断开由调用方同步通知，远端必须收到真正的Disconnected事件。
            Server.HandleDisconnected(NetworkTransportSide.Server, connection);
            serverConnections.Remove(connection);
        }

        public void RedialClient(int index)
        {
            if (!clients[index].TryRestartClient(out string error))
            {
                throw new InvalidOperationException(error);
            }
        }

        public IGameTransport ServerTransport => authority.ServerTransport;

        public void Stop()
        {
            if (stopped)
            {
                return;
            }
            stopped = true;
            foreach (var runtime in clients)
            {
                if (runtime != null && !ReferenceEquals(runtime, authority))
                {
                    runtime.Dispose();
                }
            }
            authority?.Dispose();
            if (configured)
            {
                Application.targetFrameRate = previousFrameRate;
                QualitySettings.vSyncCount = previousVSync;
            }
        }

        private void OnDestroy()
        {
            Stop();
        }
    }
}
