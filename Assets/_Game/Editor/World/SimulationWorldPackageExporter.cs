using System;
using System.IO;
using AFPS.Bootstrap;
using AFPS.Simulation.Characters;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AFPS.Editor.World
{
    public static class SimulationWorldPackageExporter
    {
        [MenuItem("AFPS/World/Export Runtime World Package")]
        public static void ExportActiveScene()
        {
            var context = ReadActiveContext();
            string path = EditorUtility.SaveFilePanel("导出独立服务器世界包",
                Path.Combine(Application.dataPath, "_Game/Worlds"), SceneManager.GetActiveScene().name, "afsp");
            if (!string.IsNullOrEmpty(path))
            {
                File.WriteAllBytes(path, SimulationWorldPackageCodec.Serialize(context));
                Debug.Log($"独立世界包已导出：{path}；世界={context.Identity.WorldHash}，配置={context.Identity.ConfigurationHash}。");
            }
        }

        /// <summary>批处理验证入口；仅在调用方指定的隔离Unity项目打开已保存训练场，不写回场景。</summary>
        public static void ExportTrainingForVerification()
        {
            try
            {
                EditorSceneManager.OpenScene(TrainingGroundBuilder.ScenePath);
                var context = ReadActiveContext();
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../TrainingGround.afsp"));
                File.WriteAllBytes(path, SimulationWorldPackageCodec.Serialize(context));
                File.WriteAllText(path + ".identity.txt", $"Algorithm={context.Identity.AlgorithmVersion};"
                    + $"World={context.Identity.WorldHash};Configuration={context.Identity.ConfigurationHash}");
                Debug.Log("独立世界包验证产物：" + path);
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static SimulationWorldContext ReadActiveContext()
        {
            var controllers = UnityEngine.Object.FindObjectsOfType<UnityNetworkMovementController>();
            if (controllers.Length != 1)
            {
                throw new InvalidOperationException("导出场景必须只有一个网络移动控制器，不能猜测服务器所用世界。");
            }
            var controller = controllers[0];
            if (controller.RuntimeWorld != null)
            {
                // Play期间取实际已准入世界，不能用刚改的Inspector字段导出另一个身份。
                return controller.RuntimeWorld;
            }
            if (!controller.TryPrepareSimulation(out string error))
            {
                throw new InvalidOperationException(error);
            }
            return controller.PreparedWorld;
        }
    }
}
