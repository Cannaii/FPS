using AFPS.NetCode.Match;
using UnityEngine;
using UnityEngine.UI;

namespace AFPS.Bootstrap
{
    /// <summary>仅显式冒烟驱动调用；通过真实UI准备，不改变服务器门禁或压缩倒计时。</summary>
    internal static class SmokeTestLobby
    {
        public static bool EnsurePlaying(UnityNetworkGameplayController movement)
        {
            var manager = movement?.SessionCoordinator;
            if (manager == null)
            {
                return false;
            }
            if (manager.IsLocalPractice)
            {
                return true;
            }
            if (manager.ClientLobby == null || !manager.TryGetLocalLobbyPlayer(out var player))
            {
                return false;
            }
            bool playing = manager.ClientLobby.Phase == LobbyPhase.Playing;
            bool prepare = !player.Ready && !player.Recovering && !manager.ClientRecoveryPending
                && !manager.LobbyReadyPending && manager.ClientLobby.Phase == LobbyPhase.Waiting;
            if (prepare || (playing && movement.GetComponent<NetworkLobbyMenu>() != null))
            {
                foreach (var button in Object.FindObjectsOfType<Button>())
                {
                    if (button.name == "Enter Button" && button.interactable)
                    {
                        button.onClick.Invoke();
                        break;
                    }
                }
            }
            return playing;
        }
    }
}
