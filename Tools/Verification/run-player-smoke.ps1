param(
    [string]$PlayerPath = 'Library/AFPSVerification/Player/AFPSGame.exe',
    [ValidateSet('Baseline', 'Combat', 'Sprint', 'Dash', 'Jump', 'Wall', 'Practice')]
    [string]$Scenario = 'Baseline',
    [string]$OutputDirectory,
    [ValidateRange(1, 65534)][int]$BasePort = 18100,
    [ValidateRange(5000, 60000)][int]$TimeoutMilliseconds = 45000,
    [switch]$Capture
)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskPlayer = if ([IO.Path]::IsPathRooted($PlayerPath)) { $PlayerPath } else { Join-Path $taskRoot $PlayerPath }
if (!(Test-Path -LiteralPath $taskPlayer))
{
    throw ('Player does not exist: ' + $taskPlayer)
}
if ($Capture -and $Scenario -ne 'Practice')
{
    throw 'Capture is supported only by the Practice scenario.'
}
if (!$OutputDirectory)
{
    $OutputDirectory = Join-Path $taskRoot ('Library/AFPSVerification/Results/' + $Scenario)
}
elseif (![IO.Path]::IsPathRooted($OutputDirectory))
{
    $OutputDirectory = Join-Path $taskRoot $OutputDirectory
}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$taskFlags = @{
    Baseline = @('-afpsSmokeTest', 'AFPS_SMOKE_RESULT')
    Combat = @('-afpsCombatSmoke', 'AFPS_COMBAT_RESULT')
    Sprint = @('-afpsSprintSmoke', 'AFPS_SPRINT_RESULT')
    Dash = @('-afpsDashSmoke', 'AFPS_DASH_RESULT')
    Jump = @('-afpsJumpSmoke', 'AFPS_JUMP_RESULT')
    Wall = @('-afpsWallSmoke', 'AFPS_WALL_RESULT')
    Practice = @('-afpsPracticeSmoke', 'AFPS_PRACTICE_RESULT')
}
$taskResults = @()
$taskSuccess = $false
$taskFailure = $null
try
{
    $taskRooms = if ($Scenario -eq 'Practice') { @('Practice') } else { @('Host', 'Dedicated') }
    foreach ($taskRoom in $taskRooms)
    {
        $taskEntries = @()
        try
        {
            $taskSpecs = @()
            if ($taskRoom -eq 'Practice')
            {
                $taskSpecs += @{ Mode = ''; Name = 'practice'; Port = 0 }
            }
            elseif ($taskRoom -eq 'Host')
            {
                $taskSpecs += @{ Mode = 'Host'; Name = 'host'; Port = $BasePort }
                $taskSpecs += @{ Mode = 'Client'; Name = 'host-client'; Port = $BasePort }
            }
            else
            {
                $taskSpecs += @{ Mode = 'Server'; Name = 'server'; Port = $BasePort + 1 }
                $taskSpecs += @{ Mode = 'Client'; Name = 'client1'; Port = $BasePort + 1 }
                $taskSpecs += @{ Mode = 'Client'; Name = 'client2'; Port = $BasePort + 1 }
            }
            foreach ($taskSpec in $taskSpecs)
            {
                $taskLog = Join-Path $OutputDirectory ($Scenario.ToLowerInvariant() + '-' + $taskSpec.Name + '.log')
                # 两工作线程/进程，并按房间串行；避免测试进程争抢主编辑器资源。
                $taskArguments = @('-batchmode', '-job-worker-count', '2', '-logFile', $taskLog)
                if ($taskRoom -eq 'Practice')
                {
                    $taskArguments += @('-force-d3d11', '-screen-width', '1280', '-screen-height', '720')
                    if ($Capture)
                    {
                        $taskArguments += '-afpsPracticeCapture'
                    }
                }
                else
                {
                    $taskArguments += @('-nographics', '-afpsMode', $taskSpec.Mode,
                        '-afpsAddress', '127.0.0.1', '-afpsPort', $taskSpec.Port)
                }
                # 墙专项的独服也必须加载相同夹具，否则入局世界指纹必然不同。
                if ($taskSpec.Mode -ne 'Server' -or $Scenario -eq 'Wall')
                {
                    $taskArguments += $taskFlags[$Scenario][0]
                }
                $taskProcess = Start-Process $taskPlayer -WindowStyle Hidden -WorkingDirectory $taskRoot -ArgumentList $taskArguments -PassThru
                $taskEntries += @{ Process = $taskProcess; Log = $taskLog; Mode = $taskSpec.Mode; Name = $taskSpec.Name }
            }
            foreach ($taskEntry in $taskEntries)
            {
                if ($taskEntry.Mode -eq 'Server')
                {
                    continue
                }
                $taskFinished = $taskEntry.Process.WaitForExit($TimeoutMilliseconds)
                $taskEntry.Process.Refresh()
                if (!$taskFinished -and !$taskEntry.Process.HasExited)
                {
                    throw ('Player exit timeout: ' + $taskEntry.Name)
                }
                $taskLines = @(Select-String -LiteralPath $taskEntry.Log -Pattern $taskFlags[$Scenario][1] | ForEach-Object { $_.Line })
                $taskResults += @{ Player = $taskEntry.Name; ExitCode = $taskEntry.Process.ExitCode; Results = $taskLines; Log = $taskEntry.Log }
                Write-Output ($taskEntry.Name + ': exit=' + $taskEntry.Process.ExitCode + '; ' + ($taskLines -join ' '))
                if ($taskEntry.Process.ExitCode -ne 0 -or !($taskLines -match 'RESULT\] PASS'))
                {
                    throw ('Player smoke failed: ' + $taskEntry.Name)
                }
            }
        }
        finally
        {
            foreach ($taskEntry in $taskEntries)
            {
                $taskEntry.Process.Refresh()
                if (!$taskEntry.Process.HasExited)
                {
                    Stop-Process -Id $taskEntry.Process.Id -Force -ErrorAction SilentlyContinue
                }
            }
        }
    }
    $taskSuccess = $true
}
catch
{
    $taskFailure = $_.Exception.Message
    throw
}
finally
{
    $taskReport = @{ Scenario = $Scenario; Passed = $taskSuccess; Error = $taskFailure; Players = $taskResults }
    [IO.File]::WriteAllText((Join-Path $OutputDirectory ($Scenario + '-results.json')),
        ($taskReport | ConvertTo-Json -Depth 6), (New-Object System.Text.UTF8Encoding($false)))
}
