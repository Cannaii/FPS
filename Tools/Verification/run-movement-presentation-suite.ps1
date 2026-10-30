param(
    [string]$PlayerPath = 'Library/AFPSVerification/Player/AFPSGame.exe',
    [string]$OutputDirectory = 'Library/AFPSVerification/Results/MovementPresentation',
    [ValidateSet('Sprint', 'Dash', 'Jump', 'Wall', 'Stance', 'WallRun', 'Climb', 'Grapple')]
    [string[]]$Scenarios = @('Sprint', 'Dash', 'Jump', 'Wall', 'Stance', 'WallRun', 'Climb', 'Grapple'),
    [ValidateRange(1, 65463)][int]$BasePort = 18580,
    [ValidateRange(5000, 60000)][int]$TimeoutMilliseconds = 45000,
    [ValidateRange(0, 64)][int]$JobWorkerCount = 2
)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskPlayer = if ([IO.Path]::IsPathRooted($PlayerPath)) { $PlayerPath } else { Join-Path $taskRoot $PlayerPath }
$taskPlayer = [IO.Path]::GetFullPath($taskPlayer)
$taskData = Join-Path (Split-Path -Parent $taskPlayer) (([IO.Path]::GetFileNameWithoutExtension($taskPlayer)) + '_Data')
if (!(Test-Path -LiteralPath $taskPlayer -PathType Leaf) -or !(Test-Path -LiteralPath $taskData -PathType Container))
{
    throw 'Expected a built Windows Unity Player and its matching _Data directory.'
}
if ($Scenarios.Count -eq 0 -or @($Scenarios | Sort-Object -Unique).Count -ne $Scenarios.Count)
{
    throw 'Select at least one movement scenario, without duplicates.'
}
if (![IO.Path]::IsPathRooted($OutputDirectory))
{
    $OutputDirectory = Join-Path $taskRoot $OutputDirectory
}
# 每次新建独立证据目录；不能把旧JSON、旧进程日志误当作当前结果。
$taskRun = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
$taskOutput = Join-Path ([IO.Path]::GetFullPath($OutputDirectory)) $taskRun
New-Item -ItemType Directory -Path $taskOutput | Out-Null

function Get-PlayerManifest
{
    param([string]$Executable, [string]$DataDirectory)
    $taskBuildRoot = Split-Path -Parent $Executable
    $taskFiles = @((Get-Item -LiteralPath $Executable))
    $taskFiles += @(Get-ChildItem -LiteralPath $DataDirectory -Recurse -File)
    # exe只是Unity启动壳；同时覆盖Managed DLL、资源、UnityPlayer及Mono，才能识别实际运行产物。
    foreach ($taskName in @('UnityPlayer.dll', 'MonoBleedingEdge', 'UnityCrashHandler64.exe', 'WinPixEventRuntime.dll'))
    {
        $taskPath = Join-Path $taskBuildRoot $taskName
        if (Test-Path -LiteralPath $taskPath -PathType Container)
        {
            $taskFiles += @(Get-ChildItem -LiteralPath $taskPath -Recurse -File)
        }
        elseif (Test-Path -LiteralPath $taskPath -PathType Leaf)
        {
            $taskFiles += Get-Item -LiteralPath $taskPath
        }
    }
    $taskEntries = @($taskFiles | Sort-Object FullName -Unique | ForEach-Object {
        [ordered]@{
            Path = $_.FullName.Substring($taskBuildRoot.Length + 1).Replace('\', '/')
            Length = $_.Length
            Sha256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        }
    })
    $taskHash = [Security.Cryptography.SHA256]::Create()
    try
    {
        $taskBytes = [Text.Encoding]::UTF8.GetBytes((($taskEntries | ForEach-Object { $_.Path + ':' + $_.Sha256 }) -join "`n"))
        $taskDigest = [BitConverter]::ToString($taskHash.ComputeHash($taskBytes)).Replace('-', '')
    }
    finally
    {
        $taskHash.Dispose()
    }
    return [ordered]@{ Sha256 = $taskDigest; Files = $taskEntries }
}

$taskManifest = Get-PlayerManifest $taskPlayer $taskData
$taskManifest | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $taskOutput 'player-manifest.json') -Encoding UTF8
$taskSmoke = Join-Path $PSScriptRoot 'run-player-smoke.ps1'
$taskSummary = [ordered]@{
    FormatVersion = 1
    StartedUtc = [DateTime]::UtcNow.ToString('o')
    CompletedUtc = $null
    PlayerPath = $taskPlayer
    PlayerSha256 = $taskManifest.Sha256
    PlayerFileCount = $taskManifest.Files.Count
    PlayerUnchanged = $false
    RunnerSha256 = (Get-FileHash -LiteralPath $taskSmoke -Algorithm SHA256).Hash
    SuiteSha256 = (Get-FileHash -LiteralPath $PSCommandPath -Algorithm SHA256).Hash
    Scope = 'Automated movement and remote presentation regression; excludes manual feel, audio audition, Android and WAN.'
    JobWorkerCount = $JobWorkerCount
    Results = @()
    Passed = $false
    Error = $null
}
try
{
    for ($taskIndex = 0; $taskIndex -lt $Scenarios.Count; $taskIndex++)
    {
        $taskScenario = $Scenarios[$taskIndex]
        $taskCaseOutput = Join-Path $taskOutput $taskScenario
        New-Item -ItemType Directory -Path $taskCaseOutput | Out-Null
        # 每项占用Host/独服两个端口；预留10端口步距，所有场景顺序运行，不改变正式Tick速度。
        $taskPort = $BasePort + $taskIndex * 10
        $taskCase = [ordered]@{
            Scenario = $taskScenario
            BasePort = $taskPort
            Passed = $false
            ProcessCount = 0
            ElapsedSeconds = 0
            Report = Join-Path $taskCaseOutput ($taskScenario + '-results.json')
            Error = $null
        }
        $taskWatch = [Diagnostics.Stopwatch]::StartNew()
        try
        {
            & $taskSmoke -PlayerPath $taskPlayer -Scenario $taskScenario -OutputDirectory $taskCaseOutput `
                -BasePort $taskPort -TimeoutMilliseconds $TimeoutMilliseconds `
                -JobWorkerCount $JobWorkerCount -GracefulServerExit *> (Join-Path $taskCaseOutput 'runner.log')
            $taskReport = Get-Content -LiteralPath $taskCase.Report -Raw | ConvertFrom-Json
            $taskCase.ProcessCount = @($taskReport.Players).Count
            if (!$taskReport.Passed -or $taskCase.ProcessCount -ne 5 -or
                @($taskReport.Players | Where-Object { $_.ExitCode -ne 0 -or @($_.NetworkStopped).Count -ne 1 }).Count -ne 0)
            {
                throw 'Expected Host/Client and dedicated/two clients: five passing processes with clean shutdown.'
            }
            $taskCase.Passed = $true
        }
        catch
        {
            # 单项失败保留其日志，继续其他独立场景；总结果仍失败，不能用部分通过掩盖失败项。
            $taskCase.Error = $_.Exception.Message
        }
        finally
        {
            $taskWatch.Stop()
            $taskCase.ElapsedSeconds = [Math]::Round($taskWatch.Elapsed.TotalSeconds, 3)
            $taskSummary.Results += $taskCase
            $taskSummary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskOutput 'suite-summary.json') -Encoding UTF8
        }
        Write-Output ('{0}: {1}; processes={2}; seconds={3}' -f $taskScenario,
            $(if ($taskCase.Passed) { 'PASS' } else { 'FAIL' }), $taskCase.ProcessCount, $taskCase.ElapsedSeconds)
    }
    $taskFinalManifest = Get-PlayerManifest $taskPlayer $taskData
    $taskSummary.PlayerUnchanged = $taskFinalManifest.Sha256 -eq $taskManifest.Sha256
    $taskSummary.Passed = $taskSummary.PlayerUnchanged `
        -and @($taskSummary.Results | Where-Object { !$_.Passed }).Count -eq 0
    if (!$taskSummary.Passed)
    {
        throw 'Movement suite failed, or the Player artifact changed during verification; inspect suite-summary.json.'
    }
}
catch
{
    $taskSummary.Error = $_.Exception.Message
    throw
}
finally
{
    $taskSummary.CompletedUtc = [DateTime]::UtcNow.ToString('o')
    $taskSummary | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskOutput 'suite-summary.json') -Encoding UTF8
    Write-Output ('Movement suite evidence: ' + $taskOutput)
}
