namespace AFPS.NetCode.SnapshotInterpolation
{
    /// <summary>
    /// 描述一个远端玩家快照写入有序缓冲区后的结果。
    /// </summary>
    public enum SnapshotBufferInsertResult
    {
        Added,
        Replaced,
        TooOld,
        EntityMismatch
    }
}
