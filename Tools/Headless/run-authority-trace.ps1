param(
    [Parameter(Mandatory = $true)][string]$UnityEditorPath,
    [Parameter(Mandatory = $true)][string]$TracePath
)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskEditor = Split-Path -Parent $UnityEditorPath
$taskOutput = Join-Path $taskRoot 'Library/AFPSVerification/Headless'
$taskInput = if ([IO.Path]::IsPathRooted($TracePath)) { $TracePath } else { Join-Path $taskRoot $TracePath }
$taskFiles = if (Test-Path -LiteralPath $taskInput -PathType Container) {
    @(Get-ChildItem -LiteralPath $taskInput -Recurse -Filter '*.afpt' | Sort-Object FullName)
} else { @(Get-Item -LiteralPath $taskInput) }
if ($taskFiles.Count -eq 0 -or $taskFiles.Count -gt 1024)
{
    throw 'Expected 1..1024 finalized authority traces.'
}
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
. (Join-Path $PSScriptRoot 'PureSimulationSources.ps1')
$taskSources = @(Get-AfpsPureSimulationSources -ProjectRoot $taskRoot)
$taskSources += @('AuthoritativeMovementState.cs','AuthoritativeMovementStateCodec.cs') | ForEach-Object {
    Join-Path $taskRoot ('Assets/_Game/Scripts/NetCode/Messages/' + $_)
}
$taskSources += @((Get-ChildItem (Join-Path $taskRoot 'Assets/_Game/Scripts/NetCode/Replay') -Filter '*.cs').FullName)
$taskSources += Join-Path $PSScriptRoot 'AuthorityTraceReplay.cs'
$taskReferences = Join-Path $taskEditor 'Data/MonoBleedingEdge/lib/mono/4.7.2-api'
$taskDotnet = Join-Path $taskEditor 'Data/NetCoreRuntime/dotnet.exe'
$taskExe = Join-Path $taskOutput 'AuthorityTraceReplay.exe'
$taskArguments = @('-nologo','-langversion:9','-checked+','-optimize+','-target:exe',('-out:' + $taskExe))
$taskArguments += @('mscorlib.dll','System.dll','System.Core.dll','System.Numerics.dll') | ForEach-Object {
    '-r:' + (Join-Path $taskReferences $_)
}
$taskArguments += @($taskSources | Select-Object -Unique)
$taskResponse = Join-Path $taskOutput 'authority-trace-compile.rsp'
[IO.File]::WriteAllLines($taskResponse, @($taskArguments | ForEach-Object { '"' + $_ + '"' }))
& $taskDotnet (Join-Path $taskEditor 'Data/DotNetSdkRoslyn/csc.dll') ('@' + $taskResponse)
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
$taskLine = & $taskDotnet --list-runtimes | Select-String '^Microsoft.NETCore.App (\S+)' | Select-Object -First 1
if ($null -eq $taskLine) { throw 'CoreCLR unavailable.' }
$taskConfig = @{ runtimeOptions = @{ framework = @{ name = 'Microsoft.NETCore.App'; version = $taskLine.Matches[0].Groups[1].Value } } }
[IO.File]::WriteAllText((Join-Path $taskOutput 'AuthorityTraceReplay.runtimeconfig.json'), ($taskConfig | ConvertTo-Json -Depth 4))
foreach ($taskFile in $taskFiles)
{
    $taskMono = & (Join-Path $taskEditor 'Data/MonoBleedingEdge/bin/mono.exe') $taskExe $taskFile.FullName
    if ($LASTEXITCODE -ne 0) { throw ('Mono replay failed: ' + $taskFile.FullName) }
    $taskCore = & $taskDotnet $taskExe $taskFile.FullName
    if ($LASTEXITCODE -ne 0) { throw ('CoreCLR replay failed: ' + $taskFile.FullName) }
    Write-Output ('Trace: ' + $taskFile.FullName)
    Write-Output ('Mono: ' + ($taskMono -join ' '))
    Write-Output ('CoreCLR: ' + ($taskCore -join ' '))
    if (($taskMono -join "`n") -ne ($taskCore -join "`n")) { throw 'Authority trace runtime hashes differ.' }
}
