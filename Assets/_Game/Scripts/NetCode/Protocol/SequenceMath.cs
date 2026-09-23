namespace AFPS.NetCode.Protocol
{
    /// <summary>
    /// 比较允许 uint 自然回绕的网络 Tick 和包序号。
    /// 两个值之间的有效距离必须小于 uint 范围的一半。
    /// </summary>
    public static class SequenceMath
    {
        public static bool IsNewer(uint candidate, uint reference) => unchecked((int)(candidate - reference)) > 0;

        public static bool IsOlder(uint candidate, uint reference) => IsNewer(reference, candidate);

        public static bool IsNewerOrEqual(uint candidate, uint reference) => candidate == reference || IsNewer(candidate, reference);
    }
}
