using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using AFPS.NetCode.Match;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Runtime;
using AFPS.Simulation.World;
using AFPS.Transport.Unity;
using NUnit.Framework;

namespace AFPS.Tests.EditMode
{
    public sealed class LanRoomDiscoverySocketTests
    {
        private static LanRoomAdvertisement Room(LanRoomDiscovery owner, ushort port)
        {
            return new LanRoomAdvertisement(owner.InstanceId, PacketHeader.CurrentProtocolVersion,
                port, 1, 32, DeathmatchPhase.WaitingForPlayers, new SimulationWorldIdentity(21, 2, 3));
        }

        [Test]
        public void TwoSameMachineAnnouncersAreDiscoveredAndStoppedRoomExpires()
        {
            using (var first = new LanRoomDiscovery())
            using (var second = new LanRoomDiscovery())
            using (var browser = new LanRoomDiscovery())
            {
                Assert.That(first.TryStartAnnouncer(out string error), Is.True, error);
                Assert.That(second.TryStartAnnouncer(out error), Is.True, error);
                Assert.That(browser.TryStartBrowser(out error), Is.True, error);
                var firstRoom = Room(first, 18400);
                var secondRoom = Room(second, 18401);
                var clock = Stopwatch.StartNew();
                while (clock.Elapsed.TotalSeconds < 3
                    && (!Contains(browser, first.InstanceId) || !Contains(browser, second.InstanceId)))
                {
                    double now = clock.Elapsed.TotalSeconds;
                    browser.PumpBrowser(now);
                    first.PumpAnnouncer(firstRoom, now);
                    second.PumpAnnouncer(secondRoom, now);
                    browser.PumpBrowser(now);
                    Thread.Sleep(1);
                }
                Assert.That(Contains(browser, first.InstanceId) && Contains(browser, second.InstanceId), Is.True, browser.LastError);
                first.Stop();
                // 使用单调测试时钟跳过过期窗口，第二间房由真实UDP响应刷新。
                double future = clock.Elapsed.TotalSeconds + LanRoomDirectory.ExpirySeconds;
                browser.PumpBrowser(future);
                Assert.That(Contains(browser, first.InstanceId), Is.False);
                for (int attempt = 0; attempt < 100 && !Contains(browser, second.InstanceId); attempt++)
                {
                    second.PumpAnnouncer(secondRoom, future);
                    browser.PumpBrowser(future);
                    Thread.Sleep(1);
                }
                Assert.That(Contains(browser, second.InstanceId), Is.True);
                Assert.That(Contains(browser, first.InstanceId), Is.False);
                var rows = new List<LanRoomEntry>();
                browser.Rooms.CopyTo(rows);
                Assert.That(rows.Find(row => row.Advertisement.InstanceId == second.InstanceId).Advertisement.Port,
                    Is.EqualTo(18401));
            }
        }

        private static bool Contains(LanRoomDiscovery browser, ulong instance)
        {
            // 仅断言本测试拥有的实例，用户正在运行的其他局域网房间不应让验证失败。
            var rows = new List<LanRoomEntry>();
            browser.Rooms.CopyTo(rows);
            return rows.Exists(room => room.Advertisement.InstanceId == instance);
        }

        [Test]
        public void BrowserRejectsUnsolicitedAndOversizedRepliesAndUsesUdpSourceAddress()
        {
            using (var browser = new LanRoomDiscovery())
            using (var sender = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
            {
                Assert.That(browser.TryStartBrowser(out string error), Is.True, error);
                sender.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                browser.PumpBrowser(0);
                var endpoint = new IPEndPoint(IPAddress.Loopback, browser.LocalPort);
                var bytes = new byte[50];
                LanRoomDiscoveryProtocol.TryWriteAdvertisement(browser.CurrentQueryNonce ^ 1UL,
                    Room(browser, 18402), new ArraySegment<byte>(bytes));
                sender.SendTo(bytes, endpoint);
                for (int attempt = 0; attempt < 20; attempt++)
                {
                    browser.PumpBrowser(attempt * 0.001);
                    Thread.Sleep(1);
                }
                Assert.That(browser.Rooms.Count, Is.Zero);
                // 超长响应即使以合法包为前缀，也不能截断后接受。
                var oversized = new byte[4096];
                LanRoomDiscoveryProtocol.TryWriteAdvertisement(browser.CurrentQueryNonce,
                    Room(browser, 18402), new ArraySegment<byte>(oversized));
                sender.SendTo(oversized, endpoint);
                browser.PumpBrowser(0.03);
                Assert.That(browser.Rooms.Count, Is.Zero);
                LanRoomDiscoveryProtocol.TryWriteAdvertisement(browser.CurrentQueryNonce,
                    Room(browser, 18402), new ArraySegment<byte>(bytes));
                sender.SendTo(bytes, endpoint);
                for (int attempt = 0; attempt < 100 && browser.Rooms.Count == 0; attempt++)
                {
                    browser.PumpBrowser(0.04 + attempt * 0.001);
                    Thread.Sleep(1);
                }
                var rows = new List<LanRoomEntry>();
                browser.Rooms.CopyTo(rows);
                Assert.That(rows.Count, Is.EqualTo(1));
                Assert.That(rows[0].Address, Is.EqualTo("127.0.0.1"));
            }
        }

        [Test]
        public void AnnouncerContinuesAfterQuerySenderClosesItsTemporaryPort()
        {
            using (var announcer = new LanRoomDiscovery())
            using (var healthy = new LanRoomDiscovery())
            {
                Assert.That(announcer.TryStartAnnouncer(out string error), Is.True, error);
                var room = Room(announcer, 18403);
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    using (var abandoned = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp))
                    {
                        abandoned.Bind(new IPEndPoint(IPAddress.Loopback, 0));
                        var query = new byte[16];
                        LanRoomDiscoveryProtocol.TryWriteQuery((ulong)attempt + 1, new ArraySegment<byte>(query));
                        abandoned.SendTo(query, new IPEndPoint(IPAddress.Loopback, LanRoomDiscovery.DiscoveryPort));
                    }
                    // 对端已关闭；某些系统会把回复触发的ICMP端口不可达交给下一次ReceiveFrom。
                    for (int step = 0; step < 10; step++)
                    {
                        announcer.PumpAnnouncer(room, attempt * 0.02 + step * 0.001);
                        Thread.Sleep(1);
                    }
                    Assert.That(announcer.IsRunning, Is.True);
                }
                Assert.That(healthy.TryStartBrowser(out error), Is.True, error);
                for (int attempt = 0; attempt < 200 && !Contains(healthy, announcer.InstanceId); attempt++)
                {
                    double now = 0.1 + attempt * 0.001;
                    healthy.PumpBrowser(now);
                    announcer.PumpAnnouncer(room, now);
                    healthy.PumpBrowser(now);
                    Thread.Sleep(1);
                }
                Assert.That(Contains(healthy, announcer.InstanceId), Is.True);
            }
        }

        [Test]
        public void RefreshCannotBypassQueryRateAndStopCanBeRepeatedAndRestarted()
        {
            using (var browser = new LanRoomDiscovery())
            {
                Assert.That(browser.TryStartBrowser(out string error), Is.True, error);
                browser.PumpBrowser(0);
                ulong nonce = browser.CurrentQueryNonce;
                browser.Refresh();
                browser.PumpBrowser(0.5);
                Assert.That(browser.CurrentQueryNonce, Is.EqualTo(nonce));
                browser.PumpBrowser(1);
                Assert.That(browser.CurrentQueryNonce, Is.Not.EqualTo(nonce));
                nonce = browser.CurrentQueryNonce;
                browser.PumpBrowser(0.25);
                Assert.That(browser.CurrentQueryNonce, Is.Not.EqualTo(nonce));
                Assert.That(browser.TryStartBrowser(out _), Is.False);
                browser.Stop();
                browser.Stop();
                browser.PumpBrowser(2);
                Assert.That(browser.LocalPort, Is.Zero);
                Assert.That(browser.TryStartAnnouncer(out error), Is.True, error);
            }
        }
    }
}
