using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using AFPS.Math;
using AFPS.NetCode.Replay;
using AFPS.Simulation.Characters;
using AFPS.Simulation.World;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AFPS.Verification.Editor
{
    /// <summary>只在显式调用时生成独立验证场景/资源；请在关闭的隔离工程中运行。</summary>
    public static class DeterminismVerificationBuild
    {
        private const string Generated = "Assets/AFPSVerificationGenerated";
        public const string Suite = "AFPS-DET-1";
        public const int Frames = 2048;

        public static byte[] CreateFixture()
        {
            var world = new FixedWorld(new[]
            {
                Box(2, -4, 0, 5, 4, 4, 6),
                Box(3, 5, 0, -4, 6, 5, 4),
                Box(4, -3, 0, -4, 3, 1, -3)
            }, new[]
            {
                new FixedWorldPlane(1, 0, FixedSurface.Solid | FixedSurface.Ground, FixedAxis.Y, Fix.Zero, true)
            });
            var configuration = new PlayerMovementConfig(6f, 20f, 20f, 8f);
            var context = new SimulationWorldContext(world, configuration, SimulationStep.Step50Hz);
            var state = new PlayerMovementState { Tick = uint.MaxValue - 1024, IsGrounded = true };
            using (var stream = new MemoryStream())
            {
                using (var writer = new AuthoritativeMovementTraceWriter(stream, context, 1, true))
                {
                    for (int index = 0; index < Frames; index++)
                    {
                        var input = new PlayerInputCommand
                        {
                            Tick = unchecked(state.Tick + 1),
                            JumpPressed = index % 71 == 0,
                            SprintHeld = index % 200 < 120,
                            DashPressed = index % 79 == 0,
                            CrouchHeld = index % 180 < 50,
                            WallBouncePressed = index % 89 == 0,
                            GrappleHeld = index % 240 < 80
                        };
                        input.TrySetEncodedControls((sbyte)(index % 400 < 200 ? 127 : -127),
                            127, (ushort)((index / 200 * 3700) % 36000), 0);
                        var next = PlayerMovementSimulation.Simulate(state, input, configuration,
                            context.TickDeltaTime, context.CollisionWorld);
                        writer.Append(new AuthoritativeMovementStep(1, unchecked((uint.MaxValue - 1024) + (uint)index),
                            state, input, next));
                        state = next;
                    }
                }
                return stream.ToArray();
            }
        }

        private static FixedWorldBox Box(uint id, int x1, int y1, int z1, int x2, int y2, int z2)
        {
            return new FixedWorldBox(id, 0, FixedSurface.Solid | FixedSurface.Ground | FixedSurface.Wall
                | FixedSurface.Grapple, new FixedAabb(
                    new FixVec3(Fix.FromInt(x1), Fix.FromInt(y1), Fix.FromInt(z1)),
                    new FixVec3(Fix.FromInt(x2), Fix.FromInt(y2), Fix.FromInt(z2))));
        }

        public static void Build()
        {
            if (!Application.isBatchMode || !File.Exists("AFPSVerificationWorkspace.marker"))
            {
                throw new InvalidOperationException("只能在验证脚本标记的隔离工程中构建，禁止修改用户当前场景/设置。");
            }
            string output = Argument("-afpsVerificationBuildOutput");
            bool android = EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android;
            Directory.CreateDirectory(Generated + "/Resources");
            byte[] fixture = CreateFixture();
            File.WriteAllBytes(Generated + "/Resources/AFPSDeterminismFixture.bytes", fixture);
            // 源指纹覆盖正式纯核心及探针；不使用目标平台或本地时间，确保不同构建可比较。
            var sources = new[] { "Math", "Core", "Simulation", "NetCode" }
                .SelectMany(name => Directory.GetFiles("Assets/_Game/Scripts/" + name, "*", SearchOption.AllDirectories))
                .Concat(Directory.GetFiles("Assets/_Game/Verification/Runtime", "*", SearchOption.AllDirectories))
                .Where(path => path.EndsWith(".cs", StringComparison.Ordinal) || path.EndsWith(".asmdef", StringComparison.Ordinal))
                .OrderBy(path => path.Replace('\\', '/'), StringComparer.Ordinal);
            string fingerprint = string.Join("\n", sources.Select(path => path.Replace('\\', '/') + ":" + Hash(File.ReadAllBytes(path))));
            string manifest = "{\"suite\":\"" + Suite + "\",\"fixtureSha256\":\"" + Hash(fixture)
                + "\",\"sourceSha256\":\"" + Hash(Encoding.UTF8.GetBytes(fingerprint)) + "\"}";
            File.WriteAllText(Generated + "/Resources/AFPSDeterminismManifest.json", manifest);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var probeType = Type.GetType("AFPS.Verification.DeterminismPlayerProbe, AFPS.Verification");
            if (probeType == null)
            {
                throw new InvalidOperationException("探针程序集尚未导入，请在隔离工程重新运行构建。");
            }
            new GameObject("Determinism Player Probe").AddComponent(probeType);
            EditorSceneManager.SaveScene(scene, Generated + "/Probe.unity");
            AssetDatabase.Refresh();
            if (android)
            {
                // 仅隔离验证包：ARM64/IL2CPP、API32，不自动下载SDK或使用项目正式签名。
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevel32;
                PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.afps.determinismprobe");
                PlayerSettings.Android.useCustomKeystore = false;
                EditorUserBuildSettings.buildAppBundle = false;
            }
            else
            {
                PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            }
            File.WriteAllBytes(output + ".fixture.bytes", fixture);
            File.WriteAllText(output + ".manifest.json", manifest);
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { Generated + "/Probe.unity" },
                    target = android ? BuildTarget.Android : BuildTarget.StandaloneWindows64,
                    locationPathName = output,
                    options = BuildOptions.Development,
                    extraScriptingDefines = new[] { "AFPS_DETERMINISM_VERIFICATION" }
                });
            }
            finally
            {
                // Resources会进入任何后续普通构建；验证结束必须清走本工具生成的场景和资源。
                Cleanup();
            }
            File.WriteAllText(output + ".build.json", "{\"result\":\"" + report.summary.result
                + "\",\"errors\":" + report.summary.totalErrors + ",\"warnings\":" + report.summary.totalWarnings + "}");
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        public static void Cleanup()
        {
            if (!Application.isBatchMode || !File.Exists("AFPSVerificationWorkspace.marker"))
            {
                throw new InvalidOperationException("只允许清理显式隔离验证工程。");
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(Generated);
            AssetDatabase.Refresh();
        }

        private static string Argument(string key)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, key);
            if (index < 0 || index + 1 == args.Length)
            {
                throw new ArgumentException("缺少输出参数：" + key);
            }
            return Path.GetFullPath(args[index + 1]);
        }

        private static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create())
            {
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "");
            }
        }
    }
}
