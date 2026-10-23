using System.Collections.Generic;
using AFPS.NetCode.SnapshotInterpolation;
using AFPS.NetCode.Match;
using UnityEngine;

namespace AFPS.Presentation.Characters
{
    /// <summary>
    /// 为远端实体创建独立视图，并使用各自的快照时间线更新位置和朝向。
    /// 未配置 Prefab 时会创建无碰撞的胶囊作为开发期占位角色。
    /// </summary>
    public sealed class RemotePlayerWorldView : MonoBehaviour
    {
        [SerializeField] private RemotePlayerView remotePlayerPrefab;
        [SerializeField] private Transform remotePlayerRoot;

        private readonly Dictionary<uint, RemotePlayerView> views = new Dictionary<uint, RemotePlayerView>();
        private readonly List<uint> entityIds = new List<uint>();
        private readonly List<uint> staleEntityIds = new List<uint>();
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

        /// <summary>按照客户端估算的服务器时间渲染所有远端玩家。</summary>
        public void Render(RemotePlayerReplicaSet replicas, double estimatedServerTick, DeathmatchState deathmatchState = null)
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
                    deathmatchState == null || deathmatchState.Phase == DeathmatchPhase.Playing);
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
                Destroy(views[entityId].gameObject);
                views.Remove(entityId);
            }
        }

        public void Clear()
        {
            foreach (RemotePlayerView view in views.Values)
            {
                if (view != null)
                {
                    Destroy(view.gameObject);
                }
            }

            views.Clear();
        }

        private RemotePlayerView CreateView(uint entityId)
        {
            Transform parent = remotePlayerRoot != null ? remotePlayerRoot : transform;
            if (remotePlayerPrefab != null)
            {
                RemotePlayerView instance = Instantiate(remotePlayerPrefab, parent);
                instance.name = $"RemotePlayer_{entityId}";
                DisableColliders(instance.gameObject);
                return instance;
            }

            GameObject placeholder = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            placeholder.name = $"RemotePlayer_{entityId}";
            placeholder.transform.SetParent(parent, false);
            DisableColliders(placeholder);
            Renderer renderer = placeholder.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = new Color(0.15f, 0.65f, 1f);
            }

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
