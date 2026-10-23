namespace AFPS.NetCode.Protocol
{
    /// <summary>
    /// 标识一个 AFPS 数据包承载的上层消息类型。
    /// </summary>
    public enum NetworkMessageType : byte
    {
        Unknown = 0,
        InputCommandBatch = 1,
        AuthoritativePlayerState = 2,
        TimeSyncRequest = 3,
        TimeSyncResponse = 4,
        PlayerSessionAssignment = 5,
        RemotePlayerSnapshot = 6,
        PlayerDespawn = 7,
        AuthoritativeShotResult = 8,
        RemotePlayerSnapshotDelta = 9,
        DeathmatchState = 10,
        SimulationHello = 11,
        SimulationWelcome = 12,
        SimulationReady = 13,
        AuthoritativeWeaponState = 14,
        ShotRejection = 15
    }
}
