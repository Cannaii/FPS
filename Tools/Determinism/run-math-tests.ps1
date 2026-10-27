param(
    [Parameter(Mandatory = $true)][string]$UnityEditorPath,
    [switch]$IncludeWorld,
    [switch]$IncludeInput,
    [switch]$IncludeMovement,
    [switch]$IncludeCollision,
    [switch]$IncludeReplication,
    [switch]$IncludeCombat
)
$ErrorActionPreference = 'Stop'
if ($IncludeCombat) {
    $IncludeCollision = $true
}
if ($IncludeReplication) {
    $IncludeInput = $true
}
if ($IncludeMovement) {
    # 移动能力已依赖世界上下文指纹与几何查询，独立入口也验证对应碰撞边界。
    $IncludeCollision = $true
}
if ($IncludeCollision) {
    $IncludeMovement = $true
    $IncludeWorld = $true
}
if ($IncludeMovement) {
    $IncludeInput = $true
    # 墙跑通过IWorldQuery读取规范几何；单独选择移动验证也必须带上纯定点查询源文件。
    $IncludeWorld = $true
}
$taskProjectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskEditorDirectory = Split-Path -Parent $UnityEditorPath
$taskOutputDirectory = Join-Path $taskProjectRoot 'Temp/FixedMathVerification'
New-Item -ItemType Directory -Force -Path $taskOutputDirectory | Out-Null
$taskMonoReferences = Join-Path $taskEditorDirectory 'Data/MonoBleedingEdge/lib/mono/4.7.2-api'
$taskNUnitPackage = Get-ChildItem -LiteralPath (Join-Path $taskProjectRoot 'Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*' | Select-Object -First 1
if ($null -eq $taskNUnitPackage) {
    throw 'Open the project in Unity first to restore its NUnit package.'
}
$taskNUnit = Join-Path $taskNUnitPackage.FullName 'net35/unity-custom/nunit.framework.dll'
$taskSources = @((Get-ChildItem -LiteralPath (Join-Path $taskProjectRoot 'Assets/_Game/Scripts/Math') -Filter '*.cs').FullName)
$taskSources += @((Get-ChildItem -LiteralPath (Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests') -Filter 'Fix*Tests.cs' | Where-Object { $_.Name -notin @('FixedWorldTests.cs', 'FixedPlayerSimulationTests.cs', 'FixedRemoteSnapshotTests.cs', 'FixedCharacterContactTests.cs') }).FullName)
if ($IncludeWorld) {
    $taskSources += @((Get-ChildItem -LiteralPath (Join-Path $taskProjectRoot 'Assets/_Game/Scripts/Simulation/World') -Filter '*.cs').FullName)
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/FixedWorldTests.cs'
}
if ($IncludeInput) {
    $taskSources += @('InputAxisGrid.cs', 'InputAngleGrid.cs', 'PlayerInputCommand.cs') | ForEach-Object { Join-Path $taskProjectRoot ('Assets/_Game/Scripts/Simulation/Characters/' + $_) }
    $taskSources += @('InputCommandBatch.cs', 'InputCommandBatchCodec.cs') | ForEach-Object { Join-Path $taskProjectRoot ('Assets/_Game/Scripts/NetCode/Messages/' + $_) }
    $taskSources += @('PacketHeader.cs', 'PacketHeaderCodec.cs', 'NetworkMessageType.cs') | ForEach-Object { Join-Path $taskProjectRoot ('Assets/_Game/Scripts/NetCode/Protocol/' + $_) }
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Scripts/NetCode/Serialization/PacketBuffer.cs'
    $taskSources += @('InputGridTests.cs', 'InputCommandBatchCodecTests.cs') | ForEach-Object { Join-Path $taskProjectRoot ('Assets/_Game/Tests/EditMode/Tests/' + $_) }
}
if ($IncludeMovement) {
    $taskSources += @('PlayerState.cs','PlayerStateHash.cs','PlayerSimulation.cs','PlayerSimulationConfig.cs','PlayerSprintConfig.cs','PlayerSprintSimulation.cs','PlayerDashConfig.cs','PlayerDashSimulation.cs','PlayerJumpConfig.cs','PlayerJumpSimulation.cs','PlayerWallConfig.cs','PlayerWallSimulation.cs','PlayerStanceConfig.cs','PlayerStanceSimulation.cs','PlayerWallRunConfig.cs','PlayerWallRunSimulation.cs','PlayerClimbConfig.cs','PlayerClimbSimulation.cs','PlayerGrappleConfig.cs','PlayerGrappleSimulation.cs','SimulationStep.cs') | ForEach-Object { Join-Path $taskProjectRoot ('Assets/_Game/Scripts/Simulation/Characters/' + $_) }
    $taskSources += @('CharacterCollisionConfig.cs','IFixedCharacterCollisionWorld.cs','FixedFlatGroundCollisionWorld.cs','KinematicCharacterMotor.cs','KinematicCharacterMotor.Contacts.cs','KinematicCharacterMotor.Resize.cs','FixedCharacterContacts.cs') | ForEach-Object { Join-Path $taskProjectRoot ('Assets/_Game/Scripts/Simulation/Characters/Collision/' + $_) }
    $taskSources += @('AuthoritativePlayerState.cs','AuthoritativePlayerStateCodec.cs') | ForEach-Object { Join-Path $taskProjectRoot ('Assets/_Game/Scripts/NetCode/Messages/' + $_) }
    $taskSources += @('ClientPredictionComparer.cs','ClientPredictionReconciler.cs','PredictionError.cs','ReconciliationResult.cs','ReconciliationStatus.cs') | ForEach-Object { Join-Path $taskProjectRoot ('Assets/_Game/Scripts/NetCode/Prediction/' + $_) }
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Scripts/Core/Collections/TickBuffer.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/FixedPlayerSimulationTests.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/JumpSimulationTests.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/SprintSimulationTests.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/DashSimulationTests.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/DeterministicSprintScenario.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/DeterministicDashScenario.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/DeterministicJumpScenario.cs'
}
if ($IncludeCollision) {
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Scripts/Simulation/Characters/Collision/FixedCharacterCollisionWorld.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Scripts/Simulation/Characters/SimulationWorldContext.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Scripts/Simulation/Characters/SimulationSessionSpatialConfig.cs'
    $taskSources += @((Get-ChildItem -LiteralPath (Join-Path $taskProjectRoot 'Assets/_Game/Scripts/Simulation/Weapons') -Filter '*.cs').FullName)
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/SimulationWorldContextTests.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/SimulationSessionSpatialConfigTests.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/WeaponSimulationConfigTests.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/CapsuleWorldTests.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/FixedCharacterContactTests.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/CharacterResizeTests.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/TraversalQueryBoundaryTests.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/WallRunSimulationTests.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/ClimbSimulationTests.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/GrappleSimulationTests.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/WallSimulationTests.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/DeterministicContactScenario.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/DeterministicWallScenario.cs'
$taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/DeterministicStanceScenario.cs'
$taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/DeterministicWallRunScenario.cs'
$taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/DeterministicClimbScenario.cs'
$taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/DeterministicGrappleScenario.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/TriangleWorldTests.cs'
}
if ($IncludeReplication) {
    # Exclude UnityFacade so DTO and Raw codecs compile without engine references in checked mode.
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Scripts/NetCode/SnapshotInterpolation/RemotePlayerSnapshot.cs'
    $taskSources += @('RemotePlayerSnapshotCodec.cs', 'RemotePlayerSnapshotDeltaCodec.cs') | ForEach-Object { Join-Path $taskProjectRoot ('Assets/_Game/Scripts/NetCode/Messages/' + $_) }
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/FixedRemoteSnapshotTests.cs'
}
if ($IncludeCombat) {
    $taskSources += @('AuthoritativeShot.cs', 'ServerWeaponFireController.cs', 'WeaponFireResult.cs', 'WeaponFireRejectReason.cs', 'ServerCombatTarget.cs', 'ServerHitscanResolver.cs', 'ServerWorldShotResolver.cs', 'ServerExplosionResolver.cs', 'AuthoritativeShotResult.cs', 'ServerProjectileSimulation.cs', 'ServerCombatHealth.cs') | ForEach-Object { Join-Path $taskProjectRoot ('Assets/_Game/Scripts/NetCode/Weapons/' + $_) }
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Scripts/NetCode/Protocol/SequenceMath.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Scripts/NetCode/LagCompensation/RewindHitboxHistory.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Scripts/NetCode/Messages/AuthoritativeShotResultCodec.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/AuthoritativeShotResultCodecTests.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Scripts/NetCode/Match/DeathmatchState.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Scripts/NetCode/Messages/DeathmatchStateCodec.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/DeathmatchRawCodecTests.cs'
    $taskSources += @('AuthoritativeWeaponState.cs', 'ClientWeaponPrediction.cs') | ForEach-Object { Join-Path $taskProjectRoot ('Assets/_Game/Scripts/NetCode/Weapons/' + $_) }
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Scripts/NetCode/Messages/AuthoritativeWeaponStateCodec.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/WeaponStateTests.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/WeaponReloadTests.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/StanceSimulationTests.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/DeterministicCombatTests.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/DeterministicCombatScenario.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/WorldCombatTests.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/ExplosionTests.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/AuthoritativeShotGenerationTests.cs'
    $taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/DeterministicShotGenerationScenario.cs'
}
$taskSources += Join-Path $PSScriptRoot 'MathTestRunner.cs'
$taskCompilerArguments = @('-nologo', '-langversion:9', '-checked+', '-optimize+', '-target:exe', ('-out:' + (Join-Path $taskOutputDirectory 'VerifyMono.exe')))
$taskCompilerArguments += @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Numerics.dll') | ForEach-Object { '-r:' + (Join-Path $taskMonoReferences $_) }
$taskCompilerArguments += '-r:' + $taskNUnit
$taskCompilerArguments += $taskSources
$taskResponse = Join-Path $taskOutputDirectory 'mono.rsp'
[IO.File]::WriteAllLines($taskResponse, @($taskCompilerArguments | ForEach-Object { '"' + $_ + '"' }))
Copy-Item -LiteralPath $taskNUnit -Destination (Join-Path $taskOutputDirectory 'nunit.framework.dll') -Force
& (Join-Path $taskEditorDirectory 'Data/NetCoreRuntime/dotnet.exe') (Join-Path $taskEditorDirectory 'Data/DotNetSdkRoslyn/csc.dll') ('@' + $taskResponse)
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
& (Join-Path $taskEditorDirectory 'Data/MonoBleedingEdge/bin/mono.exe') (Join-Path $taskOutputDirectory 'VerifyMono.exe')
exit $LASTEXITCODE
