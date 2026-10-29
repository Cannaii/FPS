using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AFPS.Verification.Editor
{
    public static class ServerLoadVerificationBuild
    {
        public static void Build()
        {
            if (!Application.isBatchMode || !File.Exists("AFPSVerificationWorkspace.marker"))
            {
                throw new InvalidOperationException("32人压测包只允许在标记的隔离工程构建。");
            }
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-afpsVerificationBuildOutput");
            if (index < 0 || index + 1 == args.Length)
            {
                throw new ArgumentException("缺少压测包输出路径。");
            }
            string output = Path.GetFullPath(args[index + 1]);
            const string generated = "Assets/AFPSVerificationGenerated";
            Directory.CreateDirectory(generated + "/Resources");
            File.Copy("Assets/_Game/Worlds/TrainingGround.bytes", generated + "/Resources/AFPSLoadWorld.bytes", true);
            var type = Type.GetType("AFPS.Verification.ServerLoadProbe, AFPS.Verification");
            if (type == null)
            {
                throw new InvalidOperationException("压测程序集尚未导入。");
            }
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Server Load Probe").AddComponent(type);
            EditorSceneManager.SaveScene(scene, generated + "/LoadProbe.unity");
            AssetDatabase.Refresh();
            PlayerSettings.SetScriptingBackend(BuildTargetGroup.Standalone, ScriptingImplementation.Mono2x);
            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { generated + "/LoadProbe.unity" },
                    target = BuildTarget.StandaloneWindows64,
                    locationPathName = output,
                    options = BuildOptions.Development,
                    extraScriptingDefines = new[] { "AFPS_DETERMINISM_VERIFICATION" }
                });
            }
            finally
            {
                DeterminismVerificationBuild.Cleanup();
            }
            File.WriteAllText(output + ".build.json", "{\"result\":\"" + report.summary.result
                + "\",\"errors\":" + report.summary.totalErrors + ",\"warnings\":" + report.summary.totalWarnings + "}");
            EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }
    }
}
