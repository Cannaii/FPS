using System;
using System.IO;
using System.Security.Cryptography;
using AFPS.NetCode.Protocol;
using AFPS.NetCode.Replay;
using UnityEngine;

namespace AFPS.Verification
{
    /// <summary>仅验证构建启用；重放包包含同一世界、Raw输入和每Tick期望hash，首差即失败。</summary>
    public sealed class DeterminismPlayerProbe : MonoBehaviour
    {
        [Serializable]
        public sealed class Manifest
        {
            public string suite;
            public string fixtureSha256;
            public string sourceSha256;
        }

        [Serializable]
        public sealed class Result
        {
            public string suite;
            public bool passed;
            public string error;
            public string fixtureSha256;
            public string sourceSha256;
            public string platform;
            public string processor;
            public string backend;
            public string unityVersion;
            public int pointerBits;
            public uint frames;
            public uint resets;
            public uint algorithm;
            public int protocol;
            public string worldHash;
            public string configurationHash;
            public string rollingHash;
            public string finalHash;
        }

        private void Start()
        {
            // 只由独立验证场景的组件启动；普通PlayMode不能启动探针或退出编辑器。
            if (Application.isEditor)
            {
                return;
            }
            var result = new Result
            {
                platform = Application.platform.ToString(),
                processor = SystemInfo.processorType,
                pointerBits = IntPtr.Size * 8,
                unityVersion = Application.unityVersion,
                protocol = PacketHeader.CurrentProtocolVersion,
#if ENABLE_IL2CPP
                backend = "IL2CPP"
#else
                backend = "Mono"
#endif
            };
            try
            {
                var manifestAsset = Resources.Load<TextAsset>("AFPSDeterminismManifest");
                var trace = Resources.Load<TextAsset>("AFPSDeterminismFixture");
                if (manifestAsset == null || trace == null)
                {
                    throw new FormatException("验证构建缺少轨迹或清单。");
                }
                var manifest = JsonUtility.FromJson<Manifest>(manifestAsset.text);
                result.suite = manifest.suite;
                result.sourceSha256 = manifest.sourceSha256;
                using (var sha = SHA256.Create())
                {
                    result.fixtureSha256 = BitConverter.ToString(sha.ComputeHash(trace.bytes)).Replace("-", "");
                }
                if (result.fixtureSha256 != manifest.fixtureSha256)
                {
                    throw new FormatException("验证轨迹SHA256与清单不符。");
                }
                using (var stream = new MemoryStream(trace.bytes, false))
                {
                    var replay = AuthoritativeTraceReplay.Run(stream);
                    result.frames = replay.Frames;
                    result.resets = replay.Resets;
                    result.algorithm = replay.Identity.AlgorithmVersion;
                    result.worldHash = replay.Identity.WorldHash.ToString("X16");
                    result.configurationHash = replay.Identity.ConfigurationHash.ToString("X16");
                    result.rollingHash = replay.RollingHash.ToString("X16");
                    result.finalHash = replay.FinalHash.ToString("X16");
                }
                result.passed = true;
            }
            catch (Exception exception)
            {
                result.error = exception.ToString();
            }
            string json = JsonUtility.ToJson(result, true);
            try
            {
                string path = OutputPath();
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)));
                File.WriteAllText(path, json);
                Debug.Log("[AFPS_DETERMINISM_RESULT] " + JsonUtility.ToJson(result));
            }
            catch (Exception exception)
            {
                result.passed = false;
                Debug.LogError("无法保存验证结果：" + exception);
            }
            Application.Quit(result.passed ? 0 : 2);
        }

        private static string OutputPath()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // 写应用内部files目录，调试包可用adb run-as读取，不请求外部存储权限。
            using (var unity = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
            using (var activity = unity.GetStatic<AndroidJavaObject>("currentActivity"))
            using (var directory = activity.Call<AndroidJavaObject>("getFilesDir"))
            {
                return Path.Combine(directory.Call<string>("getAbsolutePath"), "AFPSDeterminism-result.json");
            }
#else
            string[] arguments = Environment.GetCommandLineArgs();
            for (int index = 0; index + 1 < arguments.Length; index++)
            {
                if (arguments[index] == "-afpsDeterminismOutput")
                {
                    return arguments[index + 1];
                }
            }
            return Path.Combine(Application.persistentDataPath, "AFPSDeterminism-result.json");
#endif
        }
    }
}
