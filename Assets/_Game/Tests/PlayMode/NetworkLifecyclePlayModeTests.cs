using System;
using System.Collections;
using AFPS.Math;
using AFPS.NetCode.Match;
using AFPS.NetCode.Sessions;
using AFPS.Simulation.Characters;
using AFPS.Transport.Unity;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace AFPS.Tests.PlayMode
{
    public sealed class NetworkLifecyclePlayModeTests
    {
        private GameObject root;
        private PlayModeNetworkRig rig;

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // 用真正的Unity销毁回调清理，即使断言失败也释放本例所有原生驱动。
            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
                yield return null;
            }
            if (!ReferenceEquals(rig, null))
            {
                Assert.That(rig.IsStopped, Is.True);
            }
        }

        private void Create(bool host)
        {
            Assert.That(Application.isPlaying, Is.True, "必须在PlayMode运行，不能退化成EditMode循环。");
            root = new GameObject("AFPS PlayMode UTP Rig");
            rig = root.AddComponent<PlayModeNetworkRig>();
            rig.Configure(host);
        }

        private IEnumerator Until(Func<bool> predicate, string stage, double timeoutSeconds = 12d)
        {
            double deadline = Time.realtimeSinceStartupAsDouble + timeoutSeconds;
            while (!predicate())
            {
                Assert.That(rig.Fault, Is.Null, $"{stage}: {rig.Fault}");
                Assert.That(Time.realtimeSinceStartupAsDouble, Is.LessThan(deadline),
                    $"{stage}超时；worldTick={rig.WorldTick}, frames={rig.PumpedFrames}");
                yield return null;
            }
            Assert.That(rig.Fault, Is.Null);
        }

        private bool BothAdmitted()
        {
            return rig.Clients[0].CanPredictClientInput && rig.Clients[1].CanPredictClientInput
                && rig.Clients[0].ClientResumeCredential.IsValid && rig.Clients[1].ClientResumeCredential.IsValid
                && rig.Clients[0].TryGetLocalLobbyPlayer(out _)
                && rig.Clients[1].TryGetLocalLobbyPlayer(out _);
        }

        private IEnumerator StartMatch()
        {
            yield return Until(BothAdmitted, "双端世界准入");
            Assert.That(rig.Clients[0].TrySetLobbyReady(true), Is.True);
            Assert.That(rig.Clients[1].TrySetLobbyReady(true), Is.True);
            yield return Until(() => rig.Clients[0].CanEnterMatch && rig.Clients[1].CanEnterMatch, "权威开局");
        }

        private ServerAuthoritativeMovementSession Authority(int client)
        {
            var connection = rig.FindServerConnection(rig.Clients[client].RemotePlayers.LocalEntityId);
            Assert.That(rig.Server.TryGetServerSession(connection, out var session), Is.True);
            return session;
        }

        [UnityTest]
        public IEnumerator Host_SelectReadyCancelRestart_MoveAndReplicateAcrossRealFrames()
        {
            Create(true);
            yield return Until(BothAdmitted, "Host双端准入");
            var first = rig.Clients[0];
            var second = rig.Clients[1];
            rig.Input = (index, tick) => new PlayerInputCommand { MoveY = index == 0 ? 1f : 0f };
            uint before = rig.WorldTick;
            yield return Until(() => rig.WorldTick >= before + 10, "开局前输入门禁");
            Assert.That(Authority(0).CurrentState.PositionFixed.Z, Is.EqualTo(Fix.Zero));
            Assert.That(first.CanEnterMatch, Is.False);
            Assert.That(first.TrySelectSpawnSlot(3), Is.True);
            yield return Until(() => !first.LobbyReadyPending, "权威选点确认");
            first.TryGetLocalLobbyPlayer(out var selected);
            Assert.That(selected.SpawnSlot, Is.EqualTo(3));
            first.TryGetSpawnPosition(3, out var spawn);
            Assert.That(Authority(0).CurrentState.PositionFixed.X, Is.EqualTo(spawn.X));
            rig.Input = null;
            first.TrySetLobbyReady(true);
            second.TrySetLobbyReady(true);
            yield return Until(() => first.ClientLobby.Phase == LobbyPhase.Countdown
                && !first.LobbyReadyPending, "首次倒计时");
            Assert.That(first.TrySetLobbyReady(false), Is.True);
            yield return Until(() => first.ClientLobby.Phase == LobbyPhase.Waiting
                && !first.LobbyReadyPending, "取消准备确认");
            Assert.That(rig.Server.ServerDeathmatchPhase, Is.EqualTo(DeathmatchPhase.WaitingForPlayers));
            Assert.That(first.TrySetLobbyReady(true), Is.True);
            yield return Until(() => first.ClientLobby.Phase == LobbyPhase.Countdown, "重新倒计时");
            uint countdownTick = first.ClientLobby.ServerTick;
            uint remaining = first.ClientLobby.RemainingTicks;
            yield return Until(() => first.CanEnterMatch && second.CanEnterMatch, "重新开局");
            Assert.That(unchecked(rig.WorldTick - countdownTick), Is.GreaterThanOrEqualTo(remaining));
            rig.Input = (index, tick) => new PlayerInputCommand { MoveY = index == 0 ? 1f : 0f };
            uint entity = first.RemotePlayers.LocalEntityId;
            yield return Until(() => Authority(0).CurrentState.PositionFixed.Z > Fix.One
                && second.RemotePlayers.TrySample(entity, rig.WorldTick, out var remote)
                && remote.Position.z > 0.5f, "权威移动与远端插值");
            yield return Until(() => first.HasTimeSync && second.HasTimeSync, "真实TimeSync响应");
            Assert.That(rig.PumpedFrames, Is.GreaterThan(10));
        }

        [UnityTest]
        public IEnumerator Dedicated_FireReloadKillRespawn_ReplicatesResultsToBothClients()
        {
            Create(false);
            yield return StartMatch();
            var shooter = rig.Clients[0];
            uint victimEntity = rig.Clients[1].RemotePlayers.LocalEntityId;
            float yaw = Authority(0).CurrentState.PositionFixed.X < Authority(1).CurrentState.PositionFixed.X ? 90f : 270f;
            uint sequence = 0;
            bool firing = true;
            bool reload = false;
            rig.Input = (index, tick) =>
            {
                var command = new PlayerInputCommand { LookYaw = yaw };
                if (index == 0)
                {
                    if (firing && tick % 6 == 0)
                    {
                        command.FirePressed = true;
                        command.ShotSequence = ++sequence;
                        command.ShotServerTick = rig.WorldTick;
                    }
                    if (reload)
                    {
                        command.ReloadPressed = true;
                        reload = false;
                    }
                }
                return command;
            };
            yield return Until(() => rig.Server.TryGetServerDeathmatchPlayer(victimEntity, out var player)
                && player.Deaths == 1 && !player.IsAlive, "真实命中致死");
            firing = false;
            yield return Until(() => shooter.ClientWeapon.CanReload, "权威扣弹");
            reload = true;
            yield return Until(() => shooter.ClientWeapon.State.IsReloading, "权威换弹开始");
            yield return Until(() => !shooter.ClientWeapon.State.IsReloading
                && shooter.ClientWeapon.State.Ammo == shooter.ClientWeapon.State.Capacity, "权威换弹结束");
            yield return Until(() => rig.Server.TryGetServerDeathmatchPlayer(victimEntity, out var player)
                && player.IsAlive && player.Deaths == 1, "死亡计时到期重生");
            Assert.That(shooter.PendingShotPresentationCount, Is.GreaterThanOrEqualTo(5));
            Assert.That(rig.Clients[1].PendingShotPresentationCount, Is.GreaterThanOrEqualTo(5));
            Assert.That(shooter.ClientDeathmatchState.GetPlayer(0).Kills
                + shooter.ClientDeathmatchState.GetPlayer(1).Kills, Is.EqualTo(1));
        }

        [UnityTest]
        public IEnumerator Host_ReconnectKeepsSpawnAndEntity_RequiresFreshReadyBeforeMatch()
        {
            Create(true);
            yield return Until(BothAdmitted, "恢复测试准入");
            var client = rig.Clients[0];
            uint entity = client.RemotePlayers.LocalEntityId;
            var originalSession = Authority(0);
            client.TrySelectSpawnSlot(3);
            yield return Until(() => !client.LobbyReadyPending, "恢复前选点");
            client.TrySetLobbyReady(true);
            rig.Clients[1].TrySetLobbyReady(true);
            yield return Until(() => client.ClientLobby.Phase == LobbyPhase.Countdown, "恢复前倒计时");
            rig.DisconnectClient(0);
            yield return Until(() => client.ClientSession == null && rig.Server.RetainedServerSessionCount == 1,
                "真实断线及服务器保留");
            Assert.That(rig.Clients[1].CanEnterMatch, Is.False);
            rig.RedialClient(0);
            yield return Until(() => BothAdmitted() && !client.ClientRecoveryPending
                && client.TryGetLocalLobbyPlayer(out var player) && !player.Recovering, "原凭据恢复确认");
            Assert.That(client.RemotePlayers.LocalEntityId, Is.EqualTo(entity));
            Assert.That(Authority(0), Is.SameAs(originalSession));
            client.TryGetLocalLobbyPlayer(out var restored);
            Assert.That(restored.SpawnSlot, Is.EqualTo(3));
            Assert.That(restored.Ready, Is.False);
            Assert.That(client.CanEnterMatch, Is.False);
            client.TrySetLobbyReady(true);
            yield return Until(() => client.CanEnterMatch && rig.Clients[1].CanEnterMatch, "恢复后重新准备开局");
            rig.DisconnectClient(0);
            yield return Until(() => client.ClientSession == null, "对局中断线");
            rig.RedialClient(0);
            yield return Until(() => client.CanPredictClientInput && client.CanEnterMatch, "对局中恢复");
            Assert.That(client.RemotePlayers.LocalEntityId, Is.EqualTo(entity));
            Assert.That(Authority(0), Is.SameAs(originalSession));
        }

        [UnityTest]
        public IEnumerator DestroyingHost_ReleasesNativeDriverAndPortOnNextFrame()
        {
            Create(true);
            yield return Until(BothAdmitted, "销毁前双端准入");
            ushort port = rig.Port;
            var transport = rig.ServerTransport;
            UnityEngine.Object.Destroy(root);
            yield return null;
            Assert.That(rig.IsStopped, Is.True);
            Assert.That(transport.IsRunning, Is.False);
            rig.Stop();
            using (var replacement = new UnityGameTransport())
            {
                Assert.That(replacement.TryStartServer(port, 4, out string error), Is.True, error);
            }
        }
    }
}
