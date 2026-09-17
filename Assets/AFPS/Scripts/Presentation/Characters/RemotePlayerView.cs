using AFPS.NetCode.SnapshotInterpolation;
using UnityEngine;

namespace AFPS.Presentation.Characters
{
    /// <summary>
    /// 只显示远端玩家快照采样结果，不执行本地输入预测、回滚或校正平滑。
    /// </summary>
    public sealed class RemotePlayerView : MonoBehaviour
    {
        [SerializeField] private Transform presentationTransform;

        private void Awake()
        {
            if (presentationTransform == null)
            {
                presentationTransform = transform;
            }
        }

        public void ApplyRenderState(in RemotePlayerRenderState state)
        {
            presentationTransform.SetPositionAndRotation(state.Position, state.Rotation);
        }
    }
}
