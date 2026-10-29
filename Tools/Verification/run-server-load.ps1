param(
    [Parameter(Mandatory = $true)][string]$PlayerPath,
    [ValidateSet('All', 'Idle', 'Movement', 'Combat')][string]$Scenario = 'All',
    [ValidateRange(1, 10)][int]$Rounds = 3,
    [ValidateRange(100, 3000)][int]$SampleTicks = 500,
    [string]$OutputDirectory = 'Library/AFPSVerification/ServerLoad/Results'
)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskPlayer = [IO.Path]::GetFullPath($PlayerPath)
if (!(Test-Path -LiteralPath $taskPlayer -PathType Leaf))
{
    throw 'Load player does not exist.'
}
if (![IO.Path]::IsPathRooted($OutputDirectory))
{
    $OutputDirectory = Join-Path $taskRoot $OutputDirectory
}
New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$taskOutput = [IO.Path]::GetFullPath($OutputDirectory)
$taskScenarios = if ($Scenario -eq 'All') { @('Idle', 'Movement', 'Combat') } else { @($Scenario) }
$taskReports = @()
$taskRunPrefix = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
for ($taskRound = 1; $taskRound -le $Rounds; $taskRound++)
{
    foreach ($taskScenario in $taskScenarios)
    {
        $taskDirectory = Join-Path $taskOutput ($taskRunPrefix + '-' + $taskScenario + '-' + $taskRound)
        New-Item -ItemType Directory -Force -Path $taskDirectory | Out-Null
        $taskReady = Join-Path $taskDirectory 'ready.txt'
        $taskStop = Join-Path $taskDirectory 'stop.txt'
        $taskSocket = New-Object Net.Sockets.UdpClient(0)
        $taskPort = $taskSocket.Client.LocalEndPoint.Port
        $taskSocket.Dispose()
        $taskProcesses = @()
        $taskError = $null
        try
        {
            for ($taskGroup = 0; $taskGroup -lt 5; $taskGroup++)
            {
                $taskRole = if ($taskGroup -eq 0) { 'Server' } else { 'Clients' }
                $taskName = if ($taskGroup -eq 0) { 'server' } else { 'clients-' + $taskGroup }
                $taskLog = Join-Path $taskDirectory ($taskName + '.log')
                $taskJson = Join-Path $taskDirectory ($taskName + '.json')
                $taskArgs = @('-batchmode', '-nographics', '-job-worker-count', '2',
                    '-afpsLoadRole', $taskRole, '-afpsLoadScenario', $taskScenario, '-afpsLoadSamples', $SampleTicks,
                    '-afpsLoadGroupCount', '8', '-afpsLoadPort', $taskPort,
                    '-afpsLoadReady', ('"' + $taskReady + '"'), '-afpsLoadStop', ('"' + $taskStop + '"'),
                    '-afpsLoadOutput', ('"' + $taskJson + '"'), '-logFile', ('"' + $taskLog + '"'))
                $taskProcess = Start-Process -FilePath $taskPlayer -ArgumentList $taskArgs -WindowStyle Hidden -PassThru
                $taskProcesses += $taskProcess
                if ($taskGroup -eq 0)
                {
                    $taskDeadline = [DateTime]::UtcNow.AddSeconds(20)
                    while (!(Test-Path -LiteralPath $taskReady))
                    {
                        if ($taskProcess.HasExited -or [DateTime]::UtcNow -ge $taskDeadline)
                        {
                            throw 'Load server failed to listen.'
                        }
                        Start-Sleep -Milliseconds 100
                    }
                }
            }
            $taskDeadline = [DateTime]::UtcNow.AddSeconds(140)
            while (@($taskProcesses | Where-Object { !$_.HasExited }).Count -gt 0)
            {
                if ([DateTime]::UtcNow -ge $taskDeadline)
                {
                    throw 'Load run timed out.'
                }
                Start-Sleep -Milliseconds 250
            }
            foreach ($taskProcess in $taskProcesses)
            {
                $taskProcess.WaitForExit()
                if ($taskProcess.ExitCode -ne 0)
                {
                    throw ('Load process failed: exit=' + $taskProcess.ExitCode)
                }
            }
        }
        catch
        {
            $taskError = $_.Exception.Message
        }
        finally
        {
            # 仅释放本次持有的进程对象，不按名称清理用户或其他测试进程。
            foreach ($taskProcess in $taskProcesses)
            {
                if (!$taskProcess.HasExited)
                {
                    $taskProcess.Kill()
                    $taskProcess.WaitForExit()
                }
                $taskProcess.Dispose()
            }
        }
        $taskResults = @()
        foreach ($taskName in @('server', 'clients-1', 'clients-2', 'clients-3', 'clients-4'))
        {
            $taskJson = Join-Path $taskDirectory ($taskName + '.json')
            if (!(Test-Path -LiteralPath $taskJson))
            {
                $taskError = 'Missing result: ' + $taskName
                continue
            }
            $taskResult = Get-Content -LiteralPath $taskJson -Raw -Encoding UTF8 | ConvertFrom-Json
            $taskResults += $taskResult
            if ($taskResult.passed -isnot [bool] -or !$taskResult.passed)
            {
                $taskError = 'Acceptance failed: ' + $taskName
            }
        }
        $taskReport = [ordered]@{ Scenario = $taskScenario; Round = $taskRound; Passed = !$taskError; Error = $taskError; Results = $taskResults }
        $taskReport | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $taskDirectory 'summary.json') -Encoding UTF8
        $taskReports += $taskReport
        $taskReports | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath (Join-Path $taskOutput ($taskRunPrefix + '-all.json')) -Encoding UTF8
        Write-Output ('{0} round {1}: pass={2}; results={3}' -f $taskScenario, $taskRound, !$taskError, $taskDirectory)
    }
}
if (@($taskReports | Where-Object { !$_.Passed }).Count -gt 0)
{
    throw 'Some load runs failed acceptance; inspect all.json and per-process results.'
}
