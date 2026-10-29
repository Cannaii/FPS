using System.Reflection;
using AFPS.NetCode.Match;
using AFPS.NetCode.Sessions;

namespace AFPS.Tests.EditMode
{
    /// <summary>旧移动/战斗单元夹具的已开局前置状态；不用于LobbySessionTests或真实进程验收。</summary>
    internal static class RunningMatchFixture
    {
        internal static int Advance(NetworkMovementSessionManager manager, uint tick)
        {
            if (manager.ServerSessionCount >= 2 && manager.ServerDeathmatchPhase == DeathmatchPhase.WaitingForPlayers)
            {
                Start(manager, tick);
            }
            return manager.AdvanceServerSessions(tick);
        }

        internal static void Start(NetworkMovementSessionManager manager, uint tick)
        {
            // 这些用例断言战斗/回滚本身，从已有对局起点隔离验证；不把反射置状态当准备接线证据。
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            typeof(NetworkMovementSessionManager).GetField("deathmatchPhase", flags).SetValue(manager, DeathmatchPhase.Playing);
            typeof(NetworkMovementSessionManager).GetField("deathmatchStartTick", flags).SetValue(manager, tick);
            var gate = (LobbyStartGate)typeof(NetworkMovementSessionManager).GetField("lobby", flags).GetValue(manager);
            typeof(LobbyStartGate).GetProperty(nameof(LobbyStartGate.Phase)).SetValue(gate, LobbyPhase.Playing);
            typeof(LobbyStartGate).GetProperty(nameof(LobbyStartGate.PlayingStartedTick)).SetValue(gate, tick);
        }
    }
}
