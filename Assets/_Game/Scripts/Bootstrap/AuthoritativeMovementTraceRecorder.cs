using System;
using System.Collections.Generic;
using System.IO;
using AFPS.NetCode.Replay;
using AFPS.NetCode.Sessions;
using UnityEngine;

namespace AFPS.Bootstrap
{
    /// <summary>仅显式-afpsAuthorityTrace目录参数启用；记录故障停止捕获，不中断对局。</summary>
    // 保留旧组件类型的迁移信息；文件与.meta成对移动，场景/Prefab继续沿用原GUID。
    [UnityEngine.Scripting.APIUpdating.MovedFrom(
        true, sourceNamespace: "AFPS.Bootstrap", sourceAssembly: "_Game.Bootstrap",
        sourceClassName: "AuthorityTraceRecorder")]
    public sealed class AuthoritativeMovementTraceRecorder : MonoBehaviour
    {
        private readonly Dictionary<uint, AuthoritativeMovementTraceWriter> writers = new Dictionary<uint, AuthoritativeMovementTraceWriter>();
        private readonly HashSet<uint> completed = new HashSet<uint>();
        private NetworkSessionCoordinator manager;
        private string directory;
        private bool stopped;

        public static bool IsRequested => Array.Exists(Environment.GetCommandLineArgs(),
            value => value.Equals("-afpsAuthorityTrace", StringComparison.OrdinalIgnoreCase));

        public void Configure(NetworkSessionCoordinator owner)
        {
            try
            {
                if (owner.WorldContext == null)
                {
                    throw new InvalidOperationException("权威轨迹必须绑定显式FixedWorld。");
                }
                string[] arguments = Environment.GetCommandLineArgs();
                int index = Array.FindIndex(arguments,
                    value => value.Equals("-afpsAuthorityTrace", StringComparison.OrdinalIgnoreCase));
                if (index < 0 || index + 1 >= arguments.Length || arguments[index + 1].StartsWith("-"))
                {
                    throw new ArgumentException("-afpsAuthorityTrace需要输出目录。");
                }
                // 每次捕获独立子目录并CreateNew，不能覆盖前一次验收或用户已有文件。
                directory = Path.Combine(Path.GetFullPath(arguments[index + 1]), "capture-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(directory);
                manager = owner;
                manager.AuthoritativeMovementStepCompleted += Record;
                Debug.Log("AFPS权威轨迹目录：" + directory);
            }
            catch (Exception exception)
            {
                Debug.LogError("无法开始权威轨迹记录：" + exception.Message);
                stopped = true;
            }
        }

        private void Record(AuthoritativeMovementStep step)
        {
            if (stopped || completed.Contains(step.EntityId))
            {
                return;
            }
            try
            {
                if (!writers.TryGetValue(step.EntityId, out var writer))
                {
                    var stream = new FileStream(Path.Combine(directory, "entity-" + step.EntityId + ".afpt"),
                        FileMode.CreateNew, FileAccess.Write, FileShare.Read);
                    try
                    {
                        writer = new AuthoritativeMovementTraceWriter(stream, manager.WorldContext, step.EntityId);
                    }
                    catch
                    {
                        stream.Dispose();
                        throw;
                    }
                    writers.Add(step.EntityId, writer);
                }
                writer.Append(step);
                if (writer.Frames == AuthoritativeMovementTraceWriter.MaximumFrames)
                {
                    writer.Dispose();
                    completed.Add(step.EntityId);
                }
            }
            catch (Exception exception)
            {
                Debug.LogError("权威轨迹记录失败，已停止捕获：" + exception.Message);
                StopCapture();
            }
        }

        private void OnApplicationQuit() => StopCapture();
        private void OnDestroy() => StopCapture();

        private void StopCapture()
        {
            if (stopped)
            {
                return;
            }
            stopped = true;
            if (manager != null)
            {
                manager.AuthoritativeMovementStepCompleted -= Record;
            }
            foreach (var writer in writers.Values)
            {
                try
                {
                    writer.Dispose();
                }
                catch (Exception exception)
                {
                    Debug.LogError("权威轨迹未能完成页脚：" + exception.Message);
                }
            }
            writers.Clear();
        }
    }
}
