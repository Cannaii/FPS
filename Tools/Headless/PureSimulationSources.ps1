function Get-AfpsPureSimulationSources
{
    param([Parameter(Mandatory = $true)][string]$ProjectRoot)
    # 目录白名单和兼容门面排除。新增核心的Unity依赖会使引擎外编译失败，不能靠引用引擎DLL掩盖。
    $taskSources = @((Get-ChildItem (Join-Path $ProjectRoot 'Assets/_Game/Scripts/Math') -Filter '*.cs').FullName)
    $taskSources += @((Get-ChildItem (Join-Path $ProjectRoot 'Assets/_Game/Scripts/Simulation/World') -Filter '*.cs').FullName)
    $taskSources += @((Get-ChildItem (Join-Path $ProjectRoot 'Assets/_Game/Scripts/Simulation/Characters') -Filter '*.cs' |
        Where-Object { $_.Name -notlike '*.Compatibility.cs' }).FullName)
    $taskSources += @((Get-ChildItem (Join-Path $ProjectRoot 'Assets/_Game/Scripts/Simulation/Weapons') -Filter '*.cs').FullName)
    $taskSources += @('CharacterCollisionConfig.cs','IFixedCharacterCollisionWorld.cs','FixedFlatGroundCollisionWorld.cs',
        'KinematicCharacterMotor.cs','KinematicCharacterMotor.Contacts.cs','KinematicCharacterMotor.Resize.cs',
        'FixedCharacterContacts.cs','FixedCharacterCollisionWorld.cs') | ForEach-Object {
            Join-Path $ProjectRoot ('Assets/_Game/Scripts/Simulation/Characters/Collision/' + $_)
        }
    $taskSources += @('PacketHeader.cs','PacketHeaderCodec.cs','NetworkMessageType.cs','SequenceMath.cs') | ForEach-Object {
        Join-Path $ProjectRoot ('Assets/_Game/Scripts/NetCode/Protocol/' + $_)
    }
    $taskSources += @('InputCommandBatch.cs','InputCommandBatchCodec.cs') | ForEach-Object {
        Join-Path $ProjectRoot ('Assets/_Game/Scripts/NetCode/Messages/' + $_)
    }
    $taskSources += Join-Path $ProjectRoot 'Assets/_Game/Scripts/NetCode/Serialization/PacketBuffer.cs'
    return $taskSources
}
