param(
    [Parameter(Mandatory = $true)][string]$UnityEditorPath
)
$ErrorActionPreference = 'Stop'
$taskProjectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskEditorDirectory = Split-Path -Parent $UnityEditorPath
$taskOutput = Join-Path $taskProjectRoot 'Temp/BakedMapReplay'
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
$taskMonoReferences = Join-Path $taskEditorDirectory 'Data/MonoBleedingEdge/lib/mono/4.7.2-api'
$taskRuntime = Join-Path $taskEditorDirectory 'Data/NetCoreRuntime/dotnet.exe'
$taskSources = @((Get-ChildItem -LiteralPath (Join-Path $taskProjectRoot 'Assets/_Game/Scripts/Math') -Filter '*.cs').FullName)
$taskSources += @('InputAxisGrid.cs', 'InputAngleGrid.cs', 'PlayerInputCommand.cs') | ForEach-Object { Join-Path $taskProjectRoot ('Assets/_Game/Scripts/Simulation/Characters/' + $_) }
$taskSources += @('InputCommandBatch.cs', 'InputCommandBatchCodec.cs') | ForEach-Object { Join-Path $taskProjectRoot ('Assets/_Game/Scripts/NetCode/Messages/' + $_) }
$taskSources += @('PacketHeader.cs', 'PacketHeaderCodec.cs', 'NetworkMessageType.cs') | ForEach-Object { Join-Path $taskProjectRoot ('Assets/_Game/Scripts/NetCode/Protocol/' + $_) }
$taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Scripts/NetCode/Serialization/PacketBuffer.cs'
$taskSources += @('PlayerState.cs','PlayerStateHash.cs','PlayerSimulation.cs','PlayerSimulationConfig.cs','PlayerSprintConfig.cs','PlayerSprintSimulation.cs','PlayerDashConfig.cs','PlayerDashSimulation.cs','PlayerJumpConfig.cs','PlayerJumpSimulation.cs','SimulationStep.cs') | ForEach-Object { Join-Path $taskProjectRoot ('Assets/_Game/Scripts/Simulation/Characters/' + $_) }
$taskSources += @('CharacterCollisionConfig.cs','IFixedCharacterCollisionWorld.cs','FixedFlatGroundCollisionWorld.cs','KinematicCharacterMotor.cs') | ForEach-Object { Join-Path $taskProjectRoot ('Assets/_Game/Scripts/Simulation/Characters/Collision/' + $_) }
$taskSources += @('AuthoritativePlayerState.cs','AuthoritativePlayerStateCodec.cs') | ForEach-Object { Join-Path $taskProjectRoot ('Assets/_Game/Scripts/NetCode/Messages/' + $_) }
$taskSources += @('ClientPredictionComparer.cs','ClientPredictionReconciler.cs','PredictionError.cs','ReconciliationResult.cs','ReconciliationStatus.cs') | ForEach-Object { Join-Path $taskProjectRoot ('Assets/_Game/Scripts/NetCode/Prediction/' + $_) }
$taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Scripts/Core/Collections/TickBuffer.cs'
$taskSources += @((Get-ChildItem -LiteralPath (Join-Path $taskProjectRoot 'Assets/_Game/Scripts/Simulation/World') -Filter '*.cs').FullName)
$taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Scripts/Simulation/Characters/Collision/FixedCharacterCollisionWorld.cs'
$taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Scripts/Simulation/Characters/SimulationWorldContext.cs'
$taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Scripts/Simulation/Characters/SimulationSessionSpatialConfig.cs'
$taskSources += @((Get-ChildItem -LiteralPath (Join-Path $taskProjectRoot 'Assets/_Game/Scripts/Simulation/Weapons') -Filter '*.cs').FullName)
$taskSources += @('AuthoritativeShot.cs', 'ServerWeaponFireController.cs', 'WeaponFireResult.cs', 'WeaponFireRejectReason.cs', 'ServerCombatTarget.cs', 'ServerHitscanResolver.cs', 'ServerWorldShotResolver.cs', 'ServerExplosionResolver.cs', 'AuthoritativeShotResult.cs', 'ServerProjectileSimulation.cs', 'ServerCombatHealth.cs') | ForEach-Object { Join-Path $taskProjectRoot ('Assets/_Game/Scripts/NetCode/Weapons/' + $_) }
$taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Scripts/NetCode/Protocol/SequenceMath.cs'
$taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/DeterministicShotGenerationScenario.cs'
$taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Scripts/NetCode/LagCompensation/RewindHitboxHistory.cs'
$taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/DeterministicCombatScenario.cs'
$taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/DeterministicExplosionScenario.cs'
$taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/DeterministicSprintScenario.cs'
$taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/DeterministicDashScenario.cs'
$taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/DeterministicJumpScenario.cs'
$taskSources += Join-Path $PSScriptRoot 'BakedMapReplay.cs'
$taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests/DeterministicMapReplayScenario.cs'
$taskArguments = @('-nologo', '-langversion:9', '-checked+', '-optimize+', '-target:exe', ('-out:' + (Join-Path $taskOutput 'BakedMapReplay.exe')))
$taskArguments += @('mscorlib.dll', 'System.dll', 'System.Core.dll', 'System.Numerics.dll') | ForEach-Object { '-r:' + (Join-Path $taskMonoReferences $_) }
$taskArguments += $taskSources
$taskResponse = Join-Path $taskOutput 'compile.rsp'
[IO.File]::WriteAllLines($taskResponse, @($taskArguments | ForEach-Object { '"' + $_ + '"' }))
& $taskRuntime (Join-Path $taskEditorDirectory 'Data/DotNetSdkRoslyn/csc.dll') ('@' + $taskResponse)
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
$taskRuntimeLine = & $taskRuntime --list-runtimes | Select-String '^Microsoft.NETCore.App (\S+)' | Select-Object -First 1
if ($null -eq $taskRuntimeLine) {
    throw 'Unity CoreCLR runtime not found.'
}
$taskRuntimeVersion = $taskRuntimeLine.Matches[0].Groups[1].Value
$taskConfig = @{ runtimeOptions = @{ framework = @{ name = 'Microsoft.NETCore.App'; version = $taskRuntimeVersion } } } | ConvertTo-Json -Depth 4
[IO.File]::WriteAllText((Join-Path $taskOutput 'BakedMapReplay.runtimeconfig.json'), $taskConfig)
$taskMonoResult = & (Join-Path $taskEditorDirectory 'Data/MonoBleedingEdge/bin/mono.exe') (Join-Path $taskOutput 'BakedMapReplay.exe') (Join-Path $taskProjectRoot 'Assets/_Game/Worlds/SampleScene.bytes')
if ($LASTEXITCODE -ne 0) {
    Write-Output ($taskMonoResult -join ' ')
    exit $LASTEXITCODE
}
$taskCoreResult = & $taskRuntime (Join-Path $taskOutput 'BakedMapReplay.exe') (Join-Path $taskProjectRoot 'Assets/_Game/Worlds/SampleScene.bytes')
if ($LASTEXITCODE -ne 0) {
    Write-Output ($taskCoreResult -join ' ')
    exit $LASTEXITCODE
}
Write-Output ('Mono: ' + ($taskMonoResult -join ' '))
Write-Output ('CoreCLR: ' + ($taskCoreResult -join ' '))
if (($taskMonoResult -join "`n") -ne ($taskCoreResult -join "`n")) {
    throw 'Baked map runtime hashes differ.'
}
