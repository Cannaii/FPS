namespace AFPS.NetCode.Protocol
{
    /// <summary>
    /// 标识一个 AFPS 数据包承载的上层消息类型。
    /// </summary>
    public enum NetworkMessageType : byte
    {
        Unknown = 0,
        InputCommandBatch = 1,
        AuthoritativeMovementState = 2,
        TimeSyncRequest = 3,
        TimeSyncResponse = 4,
        PlayerSessionAssignment = 5,
        RemotePlayerMovementSnapshot = 6,
        PlayerDespawn = 7,
        AuthoritativeShotResult = 8,
        RemotePlayerSnapshotDelta = 9,
        DeathmatchState = 10,
        SimulationHello = 11,
        SimulationWelcome = 12,
        SimulationReady = 13,
        AuthoritativeWeaponState = 14,
        ShotRejection = 15,
        SessionResumeGrant = 16,
        SessionResumeRequest = 17,
        SessionRecoverySeed = 18,
        SessionRecoveryAck = 19,
        SessionRecoveryConfirmed = 20,
        ClientMovementHashReport = 21,
        TimeSyncAcknowledgement = 22,
        LobbyReady = 23,
        LobbySnapshot = 24
    }
}
