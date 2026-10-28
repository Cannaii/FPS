param(
    [Parameter(Mandatory = $true)][string]$UnityEditorPath,
    [Parameter(Mandatory = $true)][string]$PackagePath
)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskEditor = Split-Path -Parent $UnityEditorPath
$taskOutput = Join-Path $taskRoot 'Library/AFPSVerification/Headless'
$taskPackage = if ([IO.Path]::IsPathRooted($PackagePath)) { $PackagePath } else { Join-Path $taskRoot $PackagePath }
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
$taskReferences = Join-Path $taskEditor 'Data/MonoBleedingEdge/lib/mono/4.7.2-api'
$taskDotnet = Join-Path $taskEditor 'Data/NetCoreRuntime/dotnet.exe'
# 白名单只含纯定点核心与协议；不引用UnityEngine.dll，也不编译旧引擎兼容门面。
$taskSources = @((Get-ChildItem (Join-Path $taskRoot 'Assets/_Game/Scripts/Math') -Filter '*.cs').FullName)
$taskSources += @((Get-ChildItem (Join-Path $taskRoot 'Assets/_Game/Scripts/Simulation/World') -Filter '*.cs').FullName)
$taskSources += @((Get-ChildItem (Join-Path $taskRoot 'Assets/_Game/Scripts/Simulation/Characters') -Filter '*.cs' |
    Where-Object { $_.Name -notlike '*.Compatibility.cs' }).FullName)
$taskSources += @((Get-ChildItem (Join-Path $taskRoot 'Assets/_Game/Scripts/Simulation/Weapons') -Filter '*.cs').FullName)
$taskSources += @('CharacterCollisionConfig.cs','IFixedCharacterCollisionWorld.cs','FixedFlatGroundCollisionWorld.cs',
    'KinematicCharacterMotor.cs','KinematicCharacterMotor.Contacts.cs','KinematicCharacterMotor.Resize.cs',
    'FixedCharacterContacts.cs','FixedCharacterCollisionWorld.cs') | ForEach-Object {
        Join-Path $taskRoot ('Assets/_Game/Scripts/Simulation/Characters/Collision/' + $_)
    }
$taskSources += @('PacketHeader.cs','PacketHeaderCodec.cs','NetworkMessageType.cs') | ForEach-Object {
    Join-Path $taskRoot ('Assets/_Game/Scripts/NetCode/Protocol/' + $_)
}
$taskSources += @('InputCommandBatch.cs','InputCommandBatchCodec.cs') | ForEach-Object {
    Join-Path $taskRoot ('Assets/_Game/Scripts/NetCode/Messages/' + $_)
}
$taskSources += Join-Path $taskRoot 'Assets/_Game/Scripts/NetCode/Serialization/PacketBuffer.cs'
$taskSources += Join-Path $PSScriptRoot 'WorldPackageProbe.cs'
$taskArguments = @('-nologo','-langversion:9','-checked+','-optimize+','-target:exe',
    ('-out:' + (Join-Path $taskOutput 'WorldPackageProbe.exe')))
$taskArguments += @('mscorlib.dll','System.dll','System.Core.dll','System.Numerics.dll') | ForEach-Object {
    '-r:' + (Join-Path $taskReferences $_)
}
$taskArguments += @($taskSources | Select-Object -Unique)
$taskResponse = Join-Path $taskOutput 'world-package-compile.rsp'
[IO.File]::WriteAllLines($taskResponse, @($taskArguments | ForEach-Object { '"' + $_ + '"' }))
& $taskDotnet (Join-Path $taskEditor 'Data/DotNetSdkRoslyn/csc.dll') ('@' + $taskResponse)
if ($LASTEXITCODE -ne 0)
{
    exit $LASTEXITCODE
}
$taskRuntimeLine = & $taskDotnet --list-runtimes | Select-String '^Microsoft.NETCore.App (\S+)' | Select-Object -First 1
if ($null -eq $taskRuntimeLine)
{
    throw 'CoreCLR runtime unavailable.'
}
$taskVersion = $taskRuntimeLine.Matches[0].Groups[1].Value
$taskRuntimeConfig = @{ runtimeOptions = @{ framework = @{ name = 'Microsoft.NETCore.App'; version = $taskVersion } } }
[IO.File]::WriteAllText((Join-Path $taskOutput 'WorldPackageProbe.runtimeconfig.json'), ($taskRuntimeConfig | ConvertTo-Json -Depth 4))
$taskMono = & (Join-Path $taskEditor 'Data/MonoBleedingEdge/bin/mono.exe') (Join-Path $taskOutput 'WorldPackageProbe.exe') $taskPackage
if ($LASTEXITCODE -ne 0)
{
    throw ('Mono package probe failed: ' + ($taskMono -join ' '))
}
$taskCore = & $taskDotnet (Join-Path $taskOutput 'WorldPackageProbe.exe') $taskPackage
if ($LASTEXITCODE -ne 0)
{
    throw ('CoreCLR package probe failed: ' + ($taskCore -join ' '))
}
Write-Output ('Mono: ' + ($taskMono -join ' '))
Write-Output ('CoreCLR: ' + ($taskCore -join ' '))
if (($taskMono -join "`n") -ne ($taskCore -join "`n"))
{
    throw 'World package runtime identities or state hashes differ.'
}
