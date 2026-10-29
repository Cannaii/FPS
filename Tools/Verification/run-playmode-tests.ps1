param(
    [Parameter(Mandatory = $true)][string]$UnityEditorPath,
    [Parameter(Mandatory = $true)][string]$ProjectPath,
    [string]$OutputDirectory = 'Library/AFPSVerification/Results/PlayMode',
    [ValidateRange(30, 1800)][int]$TimeoutSeconds = 300,
    [ValidateRange(1, 16)][int]$JobWorkerCount = 2
)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$taskProject = [IO.Path]::GetFullPath($ProjectPath)
$taskEditor = [IO.Path]::GetFullPath($UnityEditorPath)
if (!(Test-Path -LiteralPath $taskEditor -PathType Leaf))
{
    throw ('Unity editor does not exist: ' + $taskEditor)
}
if (!(Test-Path -LiteralPath (Join-Path $taskProject 'ProjectSettings/ProjectVersion.txt')))
{
    throw ('Unity project does not exist: ' + $taskProject)
}
if (![IO.Path]::IsPathRooted($OutputDirectory))
{
    $OutputDirectory = Join-Path $taskRoot $OutputDirectory
}
$taskOutput = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
# 每次独立产物，编译失败/超时不能误读上次通过的XML。
$taskRun = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
$taskXml = Join-Path $taskOutput ($taskRun + '-results.xml')
$taskLog = Join-Path $taskOutput ($taskRun + '-unity.log')
$taskSummary = Join-Path $taskOutput ($taskRun + '-summary.json')
# Start-Process会重新拼接参数；Windows合法路径不能含双引号，显式引用保护含空格的路径。
$taskArguments = @(
    '-batchmode', '-nographics', '-job-worker-count', $JobWorkerCount.ToString(),
    '-projectPath', ('"' + $taskProject + '"'), '-runTests', '-testPlatform', 'PlayMode',
    '-assemblyNames', 'AFPS.Tests.PlayMode',
    '-testResults', ('"' + $taskXml + '"'), '-logFile', ('"' + $taskLog + '"')
)
$taskProcess = $null
$taskPassed = $false
$taskFailure = $null
$taskTotal = 0
$taskSkipped = 0
$taskFailed = 0
try
{
    $taskProcess = Start-Process -FilePath $taskEditor -ArgumentList $taskArguments -WindowStyle Hidden -PassThru
    $taskDeadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    while (!$taskProcess.WaitForExit(1000))
    {
        if ([DateTime]::UtcNow -ge $taskDeadline)
        {
            # 只终止本次启动的Unity，保留日志，不查杀用户编辑器或其他Unity进程。
            $taskProcess.Kill()
            $taskProcess.WaitForExit()
            throw 'PlayMode test run timed out.'
        }
    }
    if (!(Test-Path -LiteralPath $taskXml))
    {
        throw ('Unity produced no test results. Exit code: ' + $taskProcess.ExitCode)
    }
    [xml]$taskResults = [IO.File]::ReadAllText($taskXml)
    $taskTotal = [int]$taskResults.'test-run'.total
    $taskFailed = [int]$taskResults.'test-run'.failed
    $taskSkipped = [int]$taskResults.'test-run'.skipped
    $taskPassed = $taskProcess.ExitCode -eq 0 -and $taskTotal -gt 0 -and $taskFailed -eq 0 -and $taskSkipped -eq 0 `
        -and [int]$taskResults.'test-run'.passed -eq $taskTotal -and $taskResults.'test-run'.result -eq 'Passed'
    if (!$taskPassed)
    {
        throw ('PlayMode failed: total={0}, failed={1}, skipped={2}, exit={3}' -f
            $taskTotal, $taskFailed, $taskSkipped, $taskProcess.ExitCode)
    }
}
catch
{
    $taskFailure = $_.Exception.Message
}
finally
{
    $taskExitCode = if ($taskProcess -and $taskProcess.HasExited) { $taskProcess.ExitCode } else { $null }
    [ordered]@{
        Passed = $taskPassed
        Total = $taskTotal
        Failed = $taskFailed
        Skipped = $taskSkipped
        ExitCode = $taskExitCode
        Error = $taskFailure
        Project = $taskProject
        Results = $taskXml
        Log = $taskLog
    } | ConvertTo-Json | Set-Content -LiteralPath $taskSummary -Encoding UTF8
    if ($taskProcess)
    {
        $taskProcess.Dispose()
    }
}
if (!$taskPassed)
{
    throw ($taskFailure + ' Summary: ' + $taskSummary)
}
Write-Output ('PlayMode PASS: {0} tests; results={1}' -f $taskTotal, $taskXml)
