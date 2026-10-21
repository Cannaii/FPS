param(
    [Parameter(Mandatory = $true)][string]$UnityEditorPath
)
$ErrorActionPreference = 'Stop'
$taskProjectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskEditorDirectory = Split-Path -Parent $UnityEditorPath
$taskOutput = Join-Path $taskProjectRoot 'Temp/WorldQueryReplay'
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
$taskMonoReferences = Join-Path $taskEditorDirectory 'Data/MonoBleedingEdge/lib/mono/4.7.2-api'
$taskRuntime = Join-Path $taskEditorDirectory 'Data/NetCoreRuntime/dotnet.exe'
$taskSources = @((Get-ChildItem -LiteralPath (Join-Path $taskProjectRoot 'Assets/_Game/Scripts/Math') -Filter '*.cs').FullName)
$taskSources += @((Get-ChildItem -LiteralPath (Join-Path $taskProjectRoot 'Assets/_Game/Scripts/Simulation/World') -Filter '*.cs').FullName)
$taskSources += Join-Path $PSScriptRoot 'WorldQueryReplay.cs'
$taskArguments = @('-nologo', '-langversion:9', '-checked+', '-optimize+', '-target:exe', ('-out:' + (Join-Path $taskOutput 'WorldQueryReplay.exe')))
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
[IO.File]::WriteAllText((Join-Path $taskOutput 'WorldQueryReplay.runtimeconfig.json'), $taskConfig)
$taskMonoResult = & (Join-Path $taskEditorDirectory 'Data/MonoBleedingEdge/bin/mono.exe') (Join-Path $taskOutput 'WorldQueryReplay.exe')
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
$taskCoreResult = & $taskRuntime (Join-Path $taskOutput 'WorldQueryReplay.exe')
if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
}
Write-Output ('Mono: ' + ($taskMonoResult -join ' '))
Write-Output ('CoreCLR: ' + ($taskCoreResult -join ' '))
if (($taskMonoResult -join "`n") -ne ($taskCoreResult -join "`n")) {
    throw 'World query runtime hashes differ.'
}
