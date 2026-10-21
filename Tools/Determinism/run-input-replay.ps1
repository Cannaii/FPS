param(
    [Parameter(Mandatory = $true)][string]$UnityEditorPath
)
$ErrorActionPreference = 'Stop'
$taskProjectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskEditorDirectory = Split-Path -Parent $UnityEditorPath
$taskOutput = Join-Path $taskProjectRoot 'Temp/InputCommandReplay'
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
$taskMonoReferences = Join-Path $taskEditorDirectory 'Data/MonoBleedingEdge/lib/mono/4.7.2-api'
$taskRuntime = Join-Path $taskEditorDirectory 'Data/NetCoreRuntime/dotnet.exe'
$taskSources = @((Get-ChildItem -LiteralPath (Join-Path $taskProjectRoot 'Assets/_Game/Scripts/Math') -Filter '*.cs').FullName)
$taskSources += @('InputAxisGrid.cs', 'InputAngleGrid.cs', 'PlayerInputCommand.cs') | ForEach-Object { Join-Path $taskProjectRoot ('Assets/_Game/Scripts/Simulation/Characters/' + $_) }
$taskSources += @('InputCommandBatch.cs', 'InputCommandBatchCodec.cs') | ForEach-Object { Join-Path $taskProjectRoot ('Assets/_Game/Scripts/NetCode/Messages/' + $_) }
$taskSources += @('PacketHeader.cs', 'PacketHeaderCodec.cs', 'NetworkMessageType.cs') | ForEach-Object { Join-Path $taskProjectRoot ('Assets/_Game/Scripts/NetCode/Protocol/' + $_) }
$taskSources += Join-Path $taskProjectRoot 'Assets/_Game/Scripts/NetCode/Serialization/PacketBuffer.cs'
$taskSources += Join-Path $PSScriptRoot 'InputCommandReplay.cs'
$taskArguments = @('-nologo', '-langversion:9', '-checked+', '-optimize+', '-target:exe', ('-out:' + (Join-Path $taskOutput 'InputCommandReplay.exe')))
$taskArguments += @('mscorlib.dll', 'System.dll', 'System.Core.dll') | ForEach-Object { '-r:' + (Join-Path $taskMonoReferences $_) }
$taskArguments += $taskSources
$taskResponse = Join-Path $taskOutput 'compile.rsp'
[IO.File]::WriteAllLines($taskResponse, @($taskArguments | ForEach-Object { '"' + $_ + '"' }))
& $taskRuntime (Join-Path $taskEditorDirectory 'Data/DotNetSdkRoslyn/csc.dll') ('@' + $taskResponse)
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$taskRuntimeLine = & $taskRuntime --list-runtimes | Select-String '^Microsoft.NETCore.App (\S+)' | Select-Object -First 1
if ($null -eq $taskRuntimeLine) { throw 'Unity CoreCLR runtime not found.' }
$taskRuntimeVersion = $taskRuntimeLine.Matches[0].Groups[1].Value
$taskConfig = @{ runtimeOptions = @{ framework = @{ name = 'Microsoft.NETCore.App'; version = $taskRuntimeVersion } } } | ConvertTo-Json -Depth 4
[IO.File]::WriteAllText((Join-Path $taskOutput 'InputCommandReplay.runtimeconfig.json'), $taskConfig)
$taskMonoResult = & (Join-Path $taskEditorDirectory 'Data/MonoBleedingEdge/bin/mono.exe') (Join-Path $taskOutput 'InputCommandReplay.exe')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$taskCoreResult = & $taskRuntime (Join-Path $taskOutput 'InputCommandReplay.exe')
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
Write-Output ('Mono: ' + ($taskMonoResult -join ' '))
Write-Output ('CoreCLR: ' + ($taskCoreResult -join ' '))
if (($taskMonoResult -join "`n") -ne ($taskCoreResult -join "`n")) { throw 'Input command runtime hashes differ.' }
