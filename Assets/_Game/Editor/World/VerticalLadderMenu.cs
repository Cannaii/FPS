using AFPS.Bootstrap.World;
using AFPS.Simulation.World;
using UnityEditor;
using UnityEngine;

namespace AFPS.Editor.World
{
    public static class VerticalLadderMenu
    {
        [MenuItem("GameObject/AFPS/Vertical Ladder", false, 10)]
        public static void CreateFromMenu()
        {
            var ladder = Create(Vector3.zero);
            Undo.RegisterCreatedObjectUndo(ladder, "创建AFPS竖直梯子");
            Selection.activeGameObject = ladder;
        }

        /// <summary>四米高训练梯，脚底在给定世界位置；保持轴对齐，保存场景并重新烘焙后生效。</summary>
        public static GameObject Create(Vector3 feetPosition)
        {
            var root = new GameObject("AFPS Vertical Ladder");
            root.transform.position = feetPosition;
            root.layer = 8;
            root.isStatic = true;
            var volume = root.AddComponent<BoxCollider>();
            volume.center = new Vector3(0f, 2f, 0f);
            volume.size = new Vector3(1f, 4f, 0.15f);
            volume.isTrigger = true;
            root.AddComponent<WorldSurfaceAuthoring>().Surfaces = FixedSurface.Ladder;
            AddBar(root.transform, new Vector3(-0.45f, 2f, 0f), new Vector3(0.08f, 4f, 0.08f));
            AddBar(root.transform, new Vector3(0.45f, 2f, 0f), new Vector3(0.08f, 4f, 0.08f));
            for (int i = 1; i <= 12; i++)
            {
                AddBar(root.transform, new Vector3(0f, i * 0.3f, 0f), new Vector3(0.9f, 0.06f, 0.08f));
            }
            return root;
        }

        private static void AddBar(Transform root, Vector3 center, Vector3 size)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "Ladder Bar";
            bar.transform.SetParent(root, false);
            bar.transform.localPosition = center;
            bar.transform.localScale = size;
            bar.layer = root.gameObject.layer;
            Object.DestroyImmediate(bar.GetComponent<Collider>());
        }
    }
}
