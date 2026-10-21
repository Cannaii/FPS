param(
    [Parameter(Mandatory = $true)][string]$UnityEditorPath,
    [switch]$IncludeWorld,
    [switch]$IncludeInput
)
$ErrorActionPreference = 'Stop'
$taskProjectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskEditorDirectory = Split-Path -Parent $UnityEditorPath
$taskOutputDirectory = Join-Path $taskProjectRoot 'Temp/FixedMathVerification'
New-Item -ItemType Directory -Force -Path $taskOutputDirectory | Out-Null
$taskMonoReferences = Join-Path $taskEditorDirectory 'Data/MonoBleedingEdge/lib/mono/4.7.2-api'
$taskNUnitPackage = Get-ChildItem -LiteralPath (Join-Path $taskProjectRoot 'Library/PackageCache') -Directory -Filter 'com.unity.ext.nunit@*' | Select-Object -First 1
if ($null -eq $taskNUnitPackage) { throw 'Open the project in Unity first to restore its NUnit package.' }
$taskNUnit = Join-Path $taskNUnitPackage.FullName 'net35/unity-custom/nunit.framework.dll'
$taskSources = @((Get-ChildItem -LiteralPath (Join-Path $taskProjectRoot 'Assets/_Game/Scripts/Math') -Filter '*.cs').FullName)
$taskSources += @((Get-ChildItem -LiteralPath (Join-Path $taskProjectRoot 'Assets/_Game/Tests/EditMode/Tests') -Filter 'Fix*Tests.cs' | Where-Object { $_.Name -ne 'FixedWorldTests.cs' }).FullName)
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
$taskSources += Join-Path $PSScriptRoot 'MathTestRunner.cs'
$taskCompilerArguments = @('-nologo', '-langversion:9', '-checked+', '-optimize+', '-target:exe', ('-out:' + (Join-Path $taskOutputDirectory 'VerifyMono.exe')))
$taskCompilerArguments += @('mscorlib.dll', 'System.dll', 'System.Core.dll') | ForEach-Object { '-r:' + (Join-Path $taskMonoReferences $_) }
$taskCompilerArguments += '-r:' + $taskNUnit
$taskCompilerArguments += $taskSources
$taskResponse = Join-Path $taskOutputDirectory 'mono.rsp'
[IO.File]::WriteAllLines($taskResponse, @($taskCompilerArguments | ForEach-Object { '"' + $_ + '"' }))
Copy-Item -LiteralPath $taskNUnit -Destination (Join-Path $taskOutputDirectory 'nunit.framework.dll') -Force
& (Join-Path $taskEditorDirectory 'Data/NetCoreRuntime/dotnet.exe') (Join-Path $taskEditorDirectory 'Data/DotNetSdkRoslyn/csc.dll') ('@' + $taskResponse)
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& (Join-Path $taskEditorDirectory 'Data/MonoBleedingEdge/bin/mono.exe') (Join-Path $taskOutputDirectory 'VerifyMono.exe')
exit $LASTEXITCODE
