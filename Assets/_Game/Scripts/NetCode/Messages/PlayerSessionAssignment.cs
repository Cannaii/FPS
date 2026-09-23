namespace AFPS.NetCode.Messages
{
    /// <summary>
    /// 服务器在连接建立后可靠发送给客户端的本地玩家身份。
    /// 客户端使用该 ID 排除自己的远端快照，0 表示无效实体。
    /// </summary>
    public readonly struct PlayerSessionAssignment
    {
        public readonly uint EntityId;

        public PlayerSessionAssignment(uint entityId)
        {
            EntityId = entityId;
        }
    }
}
