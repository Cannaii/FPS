using System.Collections.Generic;
using AFPS.NetCode.SnapshotInterpolation;
using AFPS.NetCode.Match;
using UnityEngine;

namespace AFPS.Presentation.Characters
{
    /// <summary>
    /// 为远端实体创建独立视图，并使用各自的快照时间线更新位置和朝向。
    /// 优先使用显式Prefab，其次加载自有开发角色；资产缺失时保留无碰撞胶囊兜底。
    /// </summary>
    // 保留旧组件类型的迁移信息；文件与.meta成对移动，场景/Prefab继续沿用原GUID。
    [UnityEngine.Scripting.APIUpdating.MovedFrom(
        true, sourceNamespace: "AFPS.Presentation.Characters", sourceAssembly: "AFPS.Presentation",
        sourceClassName: "RemotePlayerWorldView")]
    public sealed class RemotePlayerViewRegistry : MonoBehaviour
    {
        [SerializeField] private RemotePlayerView remotePlayerPrefab;
        [SerializeField] private Transform remotePlayerRoot;

        private readonly Dictionary<uint, RemotePlayerView> views = new Dictionary<uint, RemotePlayerView>();
        private readonly List<uint> entityIds = new List<uint>();
        private readonly List<uint> staleEntityIds = new List<uint>();
        private RemotePlayerView defaultPlayerPrefab;
        public int CharacterVisualCount
        {
            get
            {
                int count = 0;
                foreach (RemotePlayerView view in views.Values)
                {
                    if (view != null && view.HasCharacterVisual)
                    {
                        count++;
                    }
                }
                return count;
            }
        }
        public ulong FootstepCount
        {
            get
            {
                ulong result = 0;
                foreach (var view in views.Values)
                {
                    result += view.FootstepCount;
                }
                return result;
            }
        }
        public int VisibleGrappleRopeCount
        {
            get
            {
                int count = 0;
                foreach (var view in views.Values)
                {
                    if (view != null && view.HasGrappleRope)
                    {
                        count++;
                    }
                }
                return count;
            }
        }

        /// <summary>按照客户端估算的服务器时间渲染所有远端玩家。</summary>
        public void Render(RemotePlayerMovementReplicas replicas, double estimatedServerTick,
            DeathmatchState deathmatchState = null, float crouchedHeightRatio = 0.6f, float grappleOriginHeight = 1.6f)
        {
            if (replicas == null)
            {
                Clear();
                return;
            }

            replicas.CopyEntityIds(entityIds);
            for (int i = 0; i < entityIds.Count; i++)
            {
                uint entityId = entityIds[i];
                if (!replicas.TrySample(entityId, estimatedServerTick, out RemotePlayerRenderState state))
                {
                    continue;
                }

                if (!views.TryGetValue(entityId, out RemotePlayerView view))
                {
                    view = CreateView(entityId);
                    views.Add(entityId, view);
                }

                bool isAlive = deathmatchState == null
                    || !deathmatchState.TryGetPlayer(entityId, out DeathmatchPlayerState playerState) || playerState.IsAlive;
                view.ApplyRenderState(state, isAlive,
                    deathmatchState == null || deathmatchState.Phase == DeathmatchPhase.Playing,
                    crouchedHeightRatio, grappleOriginHeight);
            }

            staleEntityIds.Clear();
            foreach (uint entityId in views.Keys)
            {
                if (!entityIds.Contains(entityId))
                {
                    staleEntityIds.Add(entityId);
                }
            }

            for (int i = 0; i < staleEntityIds.Count; i++)
            {
                uint entityId = staleEntityIds[i];
                RemoveView(views[entityId]);
                views.Remove(entityId);
            }
        }

        public void Clear()
        {
            foreach (RemotePlayerView view in views.Values)
            {
                if (view != null)
                {
                    RemoveView(view);
                }
            }

            views.Clear();
        }

        private static void RemoveView(RemotePlayerView view)
        {
            // 运行时Destroy延迟到帧末，先隐藏绳线，不能让已移出同步范围的实体再画一帧。
            view.HideGrappleRope();
            if (Application.isPlaying)
            {
                Destroy(view.gameObject);
            }
            else
            {
                DestroyImmediate(view.gameObject);
            }
        }

        private RemotePlayerView CreateView(uint entityId)
        {
            Transform parent = remotePlayerRoot != null ? remotePlayerRoot : transform;
            RemotePlayerView prefab = remotePlayerPrefab;
            if (prefab == null)
            {
                if (defaultPlayerPrefab == null)
                {
                    defaultPlayerPrefab = Resources.Load<RemotePlayerView>("Characters/RemotePlayer");
                }
                prefab = defaultPlayerPrefab;
            }
            if (prefab != null)
            {
                RemotePlayerView instance = Instantiate(prefab, parent);
                instance.name = $"RemotePlayer_{entityId}";
                DisableColliders(instance.gameObject);
                return instance;
            }

            GameObject placeholder = new GameObject($"RemotePlayer_{entityId}");
            placeholder.transform.SetParent(parent, false);
            // 模拟根节点位于脚底，胶囊网格的原点在中心；子节点偏移保证下蹲缩放不钻入地面。
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body";
            body.transform.SetParent(placeholder.transform, false);
            body.transform.localPosition = Vector3.up;
            DisableColliders(placeholder);
            return placeholder.AddComponent<RemotePlayerView>();
        }

        private static void DisableColliders(GameObject target)
        {
            Collider[] colliders = target.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                colliders[i].enabled = false;
            }
        }
    }
}
