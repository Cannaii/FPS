# 远端角色开发资源

`Resources/Characters/RemotePlayer.prefab` 由 AFPS 的 `RemoteCharacterAssetBuilder` 使用 Unity 内置 Cube 网格生成，装甲、关节、护目镜材质均为本项目创建；没有复制或引用 Cowsins 的脚本、模型、Prefab 或材质。

角色原点在脚底，局部 +Z 朝前，尺寸单位为米，站立高度约 1.8 米。它只用于表现，不定义模拟胶囊或命中盒；Prefab 不包含 Collider、Rigidbody 或根运动。

`RemoteCharacterPoseView` 只消费远端采样：脚底根节点跟随位置/Yaw，上半身显示 Pitch，髋/膝关节表现下蹲，实际渲染位移驱动步态；死亡倾倒表现子树，重生恢复。`RemoteWeaponView` 使用同一显示 Tick 消费独立权威装备状态，显示自有 Cube 网格步枪或发射器，提供装备抬枪、换弹左臂及短后坐力姿态；死亡、失活和断更隐藏枪械并恢复节点初始姿态。这些程序化姿态仍是开发占位，不代表正式美术或完整手感验收。

此模型是自有人形开发占位，供训练场辨认瞄准、走动、下蹲和死亡状态。后续可通过 `RemotePlayerViewRegistry` 的 `remotePlayerPrefab` 字段替换；显式配置优先于 Resources 默认资产。菜单 `AFPS > Characters > Create Development Character` 仅在目标 Prefab 不存在时生成，拒绝覆盖人工修改。

已有自有开发模型可运行 `AFPS > Characters > Add Development Weapon Presentation`，只增加发射器节点和武器组件，保留原 Prefab GUID、既有节点及材质；已有组件时不重复修改。自定义角色应自行绑定 `RemoteWeaponView` 的步枪、发射器和左臂引用，升级工具不猜测其他骨架。
