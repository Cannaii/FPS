using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace AFPS.Bootstrap
{
    /// <summary>显式自动验证退出屏障；全部客户端完成断言后由外部工具放行，不改变游戏网络协议。</summary>
    internal static class SmokeTestExit
    {
        private const double BarrierTimeoutSeconds = 30;

        public static void Quit(MonoBehaviour owner, int exitCode)
        {
            string[] arguments = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < arguments.Length; i++)
            {
                if (exitCode == 0 && arguments[i].Equals(
                    "-afpsSmokeExitBarrier", StringComparison.OrdinalIgnoreCase))
                {
                    owner.StartCoroutine(WaitForRelease(arguments[i + 1]));
                    return;
                }
            }
            // 失败立即退出，不能等待其他端成功来掩盖本端错误；无显式屏障时保持原行为。
            Application.Quit(exitCode);
        }

        private static IEnumerator WaitForRelease(string path)
        {
            double started = Time.realtimeSinceStartupAsDouble;
            while (!File.Exists(path))
            {
                if (Time.realtimeSinceStartupAsDouble - started >= BarrierTimeoutSeconds)
                {
                    Debug.LogError("[AFPS_EXIT_BARRIER] FAIL timeout=True");
                    Application.Quit(2);
                    yield break;
                }
                yield return null;
            }
            Debug.Log("[AFPS_EXIT_BARRIER] PASS");
            Application.Quit(0);
        }
    }
}
