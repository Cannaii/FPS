param(
    [string]$PlayerPath = 'Library/AFPSVerification/Player/AFPSGame.exe',
    [ValidateSet('Baseline', 'Combat', 'Sprint', 'Dash', 'Jump', 'Wall', 'Stance', 'WallRun', 'Climb', 'Grapple', 'Reconnect', 'Practice', 'RoomMenu', 'RoomDiscovery', 'RoomDiscoveryDedicated', 'LobbyReady')]
    [string]$Scenario = 'Baseline',
    [string]$OutputDirectory,
    [ValidateRange(1, 65534)][int]$BasePort = 18100,
    [ValidateRange(5000, 60000)][int]$TimeoutMilliseconds = 45000,
    [string]$AuthorityTraceDirectory,
    [switch]$NetworkDebug,
    [switch]$ConcurrentRooms,
    [switch]$GracefulServerExit,
    [ValidateRange(0, 64)][int]$JobWorkerCount = 2,
    [ValidateRange(640, 3840)][int]$ScreenWidth = 1280,
    [ValidateRange(360, 2160)][int]$ScreenHeight = 720,
    [switch]$Capture
)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskPlayer = if ([IO.Path]::IsPathRooted($PlayerPath)) { $PlayerPath } else { Join-Path $taskRoot $PlayerPath }
if (!(Test-Path -LiteralPath $taskPlayer))
{
    throw ('Player does not exist: ' + $taskPlayer)
}
if ($Capture -and $Scenario -notin @('Practice', 'RoomMenu', 'RoomDiscovery', 'LobbyReady'))
{
    throw 'Capture is supported only by the Practice and RoomMenu scenarios.'
}
if ($Scenario -eq 'Practice' -and ($ConcurrentRooms -or $GracefulServerExit))
{
    throw 'Practice has no UDP rooms or dedicated server.'
}
if ($GracefulServerExit -and $Scenario -eq 'Reconnect')
{
    # 重连回归会主动制造暂时空服，不能把这个中间阶段当作最终离开而关闭独服。
    throw 'GracefulServerExit is not supported by the Reconnect scenario.'
}
if ($Scenario -in @('RoomMenu', 'RoomDiscovery', 'LobbyReady') -and ($ConcurrentRooms -or $GracefulServerExit))
{
    throw 'RoomMenu validates one graphical Host/Client room.'
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
    Stance = @('-afpsStanceSmoke', 'AFPS_STANCE_RESULT')
    WallRun = @('-afpsWallRunSmoke', 'AFPS_WALLRUN_RESULT')
    Climb = @('-afpsClimbSmoke', 'AFPS_CLIMB_RESULT')
    Grapple = @('-afpsGrappleSmoke', 'AFPS_GRAPPLE_RESULT')
    Reconnect = @('-afpsReconnectSmoke', 'AFPS_RECONNECT_RESULT')
    Practice = @('-afpsPracticeSmoke', 'AFPS_PRACTICE_RESULT')
    RoomMenu = @('-afpsRoomMenuSmoke', 'AFPS_ROOMMENU_RESULT')
    LobbyReady = @('-afpsRoomMenuSmoke', 'AFPS_ROOMMENU_RESULT')
    RoomDiscovery = @('-afpsRoomMenuSmoke', 'AFPS_ROOMMENU_RESULT')
    RoomDiscoveryDedicated = @('-afpsRoomMenuSmoke', 'AFPS_ROOMMENU_RESULT')
}
$taskResults = @()
$taskSuccess = $false
$taskFailure = $null
try
{
    $taskRooms = if ($Scenario -eq 'Practice') { @('Practice') } else { @('Host', 'Dedicated') }
    if ($Scenario -in @('RoomMenu', 'RoomDiscovery', 'LobbyReady'))
    {
        $taskRooms = @('Host')
    }
    elseif ($Scenario -eq 'RoomDiscoveryDedicated')
    {
        $taskRooms = @('Dedicated')
    }
    if ($ConcurrentRooms)
    {
        $taskRooms = @('Concurrent')
    }
    foreach ($taskRoom in $taskRooms)
    {
        $taskEntries = @()
        try
        {
            # 真实断开通知可能比另一端最终断言早到达；先等全组报告结果再统一允许退出。
            $taskBarrier = Join-Path $OutputDirectory ('exit-' + [Guid]::NewGuid().ToString('N') + '.ready')
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
            elseif ($taskRoom -eq 'Dedicated')
            {
                $taskSpecs += @{ Mode = 'Server'; Name = 'server'; Port = $BasePort + 1 }
                $taskSpecs += @{ Mode = 'Client'; Name = 'client1'; Port = $BasePort + 1 }
                $taskSpecs += @{ Mode = 'Client'; Name = 'client2'; Port = $BasePort + 1 }
            }
            else
            {
                $taskSpecs += @{ Mode = 'Host'; Name = 'host'; Port = $BasePort }
                $taskSpecs += @{ Mode = 'Client'; Name = 'host-client'; Port = $BasePort }
                $taskSpecs += @{ Mode = 'Server'; Name = 'server'; Port = $BasePort + 1 }
                $taskSpecs += @{ Mode = 'Client'; Name = 'client1'; Port = $BasePort + 1 }
                $taskSpecs += @{ Mode = 'Client'; Name = 'client2'; Port = $BasePort + 1 }
            }
            foreach ($taskSpec in $taskSpecs)
            {
                $taskLog = Join-Path $OutputDirectory ($Scenario.ToLowerInvariant() + '-' + $taskSpec.Name + '.log')
                # 0表示保留Unity默认线程数；可显式恢复历史双房间并发退出工况。
                $taskArguments = @('-batchmode', '-logFile', $taskLog)
                if ($JobWorkerCount -gt 0)
                {
                    $taskArguments += @('-job-worker-count', $JobWorkerCount)
                }
                if ($GracefulServerExit -and $taskSpec.Mode -eq 'Server')
                {
                    $taskArguments += '-afpsShutdownSmoke'
                }
                if ($taskSpec.Mode -ne 'Server' -and $taskRoom -ne 'Practice')
                {
                    $taskArguments += @('-afpsSmokeExitBarrier', $taskBarrier)
                }
                if ($NetworkDebug)
                {
                    $taskArguments += '-afpsNetworkDebug'
                }
                if ($taskRoom -eq 'Practice')
                {
                    $taskArguments += @('-force-d3d11', '-screen-width', $ScreenWidth, '-screen-height', $ScreenHeight)
                    if ($Capture)
                    {
                        $taskArguments += '-afpsPracticeCapture'
                    }
                }
                elseif ($Scenario -in @('RoomMenu', 'RoomDiscovery', 'RoomDiscoveryDedicated', 'LobbyReady') -and $taskSpec.Mode -ne 'Server')
                {
                    # 不传-afpsMode，必须经过真实启动菜单；共享文件只用于传递Host实际显示的房间码。
                    $taskArguments += @('-force-d3d11', '-screen-width', $ScreenWidth, '-screen-height', $ScreenHeight,
                        '-afpsRoomMenuRole', $taskSpec.Mode, '-afpsRoomMenuPort', $taskSpec.Port,
                        '-afpsRoomMenuCodeFile', ($taskBarrier + '.code'))
                    if ($Scenario -eq 'LobbyReady')
                    {
                        $taskArguments += '-afpsLobbyCancelTest'
                    }
                    if ($Scenario -in @('RoomDiscovery', 'RoomDiscoveryDedicated'))
                    {
                        $taskArguments += '-afpsRoomDiscoveryJoin'
                    }
                    if ($Capture)
                    {
                        $taskArguments += '-afpsRoomMenuCapture'
                    }
                }
                else
                {
                    $taskArguments += @('-nographics', '-afpsMode', $taskSpec.Mode,
                        '-afpsAddress', '127.0.0.1', '-afpsPort', $taskSpec.Port)
                }
                # 墙专项的独服也必须加载相同夹具，否则入局世界指纹必然不同。
                if ($taskSpec.Mode -ne 'Server' -or $Scenario -in @('Wall', 'Stance', 'WallRun', 'Climb', 'Grapple'))
                {
                    $taskArguments += $taskFlags[$Scenario][0]
                }
                # 仅记录会自行正常退出的Host；独服由此脚本强制清理时不能生成完整页脚。
                if ($AuthorityTraceDirectory -and $taskSpec.Mode -eq 'Host')
                {
                    $taskTraceRoot = if ([IO.Path]::IsPathRooted($AuthorityTraceDirectory)) { $AuthorityTraceDirectory } else { Join-Path $taskRoot $AuthorityTraceDirectory }
                    $taskArguments += @('-afpsAuthorityTrace', (Join-Path $taskTraceRoot $Scenario))
                }
                $taskProcess = Start-Process $taskPlayer -WindowStyle Hidden -WorkingDirectory $taskRoot -ArgumentList $taskArguments -PassThru
                $taskEntries += @{ Process = $taskProcess; Log = $taskLog; Mode = $taskSpec.Mode; Name = $taskSpec.Name }
            }
            if ($taskRoom -ne 'Practice')
            {
                $taskDeadline = [DateTime]::UtcNow.AddMilliseconds($TimeoutMilliseconds)
                do
                {
                    $taskReady = $true
                    foreach ($taskEntry in $taskEntries)
                    {
                        if ($taskEntry.Mode -eq 'Server')
                        {
                            continue
                        }
                        $taskLines = @(Select-String -LiteralPath $taskEntry.Log -Pattern $taskFlags[$Scenario][1] -ErrorAction SilentlyContinue | ForEach-Object { $_.Line })
                        if ($taskLines -match 'RESULT\] FAIL')
                        {
                            $taskResults += @{ Player = $taskEntry.Name; Results = $taskLines; Log = $taskEntry.Log }
                            throw ('Player assertion failed before exit: ' + $taskEntry.Name)
                        }
                        if (!($taskLines -match 'RESULT\] PASS'))
                        {
                            if ($taskEntry.Process.HasExited)
                            {
                                $taskResults += @{ Player = $taskEntry.Name; ExitCode = $taskEntry.Process.ExitCode;
                                    Log = $taskEntry.Log; Tail = @(Get-Content -LiteralPath $taskEntry.Log -Tail 20) }
                                throw ('Player exited before reporting success: ' + $taskEntry.Name)
                            }
                            $taskReady = $false
                        }
                    }
                    if (!$taskReady)
                    {
                        Start-Sleep -Milliseconds 100
                    }
                } while (!$taskReady -and [DateTime]::UtcNow -lt $taskDeadline)
                if (!$taskReady)
                {
                    foreach ($taskEntry in $taskEntries)
                    {
                        $taskResults += @{ Player = $taskEntry.Name; ResultTimedOut = $true; Log = $taskEntry.Log;
                            Tail = @(Get-Content -LiteralPath $taskEntry.Log -Tail 20) }
                    }
                    throw 'Player result barrier timeout.'
                }
                [IO.File]::WriteAllText($taskBarrier, 'release')
            }
            foreach ($taskEntry in $taskEntries)
            {
                if ($taskEntry.Mode -eq 'Server' -and !$GracefulServerExit)
                {
                    continue
                }
                $taskFinished = $taskEntry.Process.WaitForExit($TimeoutMilliseconds)
                $taskEntry.Process.Refresh()
                if (!$taskFinished -and !$taskEntry.Process.HasExited)
                {
                    $taskResults += @{ Player = $taskEntry.Name; TimedOut = $true;
                        Threads = $taskEntry.Process.Threads.Count; Log = $taskEntry.Log;
                        Tail = @(Get-Content -LiteralPath $taskEntry.Log -Tail 20) }
                    throw ('Player exit timeout: ' + $taskEntry.Name)
                }
                $taskMarker = if ($taskEntry.Mode -eq 'Server') { 'AFPS_SHUTDOWN_RESULT' } else { $taskFlags[$Scenario][1] }
                $taskLines = @(Select-String -LiteralPath $taskEntry.Log -Pattern $taskMarker | ForEach-Object { $_.Line })
                $taskStopped = @(Select-String -LiteralPath $taskEntry.Log -Pattern '\[AFPS_NETWORK_STOPPED\] PASS' | ForEach-Object { $_.Line })
                $taskResults += @{ Player = $taskEntry.Name; ExitCode = $taskEntry.Process.ExitCode;
                    Results = $taskLines; NetworkStopped = $taskStopped; Log = $taskEntry.Log }
                Write-Output ($taskEntry.Name + ': exit=' + $taskEntry.Process.ExitCode + '; ' + ($taskLines -join ' '))
                if ($taskEntry.Process.ExitCode -ne 0 -or !($taskLines -match 'RESULT\] PASS'))
                {
                    throw ('Player smoke failed: ' + $taskEntry.Name)
                }
                if ($GracefulServerExit -and $taskStopped.Count -ne 1)
                {
                    throw ('Expected exactly one successful network cleanup: ' + $taskEntry.Name)
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
    $taskReport = @{ Scenario = $Scenario; Passed = $taskSuccess; Error = $taskFailure;
        ConcurrentRooms = [bool]$ConcurrentRooms; JobWorkerCount = $JobWorkerCount;
        GracefulServerExit = [bool]$GracefulServerExit; Players = $taskResults }
    [IO.File]::WriteAllText((Join-Path $OutputDirectory ($Scenario + '-results.json')),
        ($taskReport | ConvertTo-Json -Depth 6), (New-Object System.Text.UTF8Encoding($false)))
}
