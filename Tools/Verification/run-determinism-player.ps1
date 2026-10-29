param(
    [Parameter(Mandatory = $true)][ValidateSet('BuildWindows', 'BuildAndroid', 'BuildLoadWindows', 'RunWindows', 'RunAndroid')][string]$Action,
    [string]$UnityEditorPath,
    [string]$ProjectPath,
    [string]$PlayerPath,
    [string]$AdbPath,
    [string]$DeviceSerial,
    [string]$OutputDirectory = 'Library/AFPSVerification/DeterminismPlayer',
    [ValidateRange(30, 3600)][int]$TimeoutSeconds = 900
)
$ErrorActionPreference = 'Stop'
$taskRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
if (![IO.Path]::IsPathRooted($OutputDirectory))
{
    $OutputDirectory = Join-Path $taskRoot $OutputDirectory
}
$taskOutput = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Force -Path $taskOutput | Out-Null
$taskRun = (Get-Date -Format 'yyyyMMdd-HHmmss') + '-' + [guid]::NewGuid().ToString('N').Substring(0, 8)
$taskLog = Join-Path $taskOutput ($taskRun + '-' + $Action + '.log')
$taskResult = Join-Path $taskOutput ($taskRun + '-' + $Action + '.json')

function Wait-OwnedProcess($taskProcess)
{
    try
    {
        $taskDeadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
        while (!$taskProcess.WaitForExit(1000))
        {
            if ([DateTime]::UtcNow -ge $taskDeadline)
            {
                $taskProcess.Kill()
                $taskProcess.WaitForExit()
                throw 'Verification process timed out.'
            }
        }
        if ($taskProcess.ExitCode -ne 0)
        {
            throw ('Verification process failed: exit=' + $taskProcess.ExitCode + '; log=' + $taskLog)
        }
    }
    finally
    {
        $taskProcess.Dispose()
    }
}

if ($Action -in @('BuildWindows', 'BuildAndroid', 'BuildLoadWindows'))
{
    if (!$ProjectPath -or !$UnityEditorPath)
    {
        throw 'Build requires explicit ProjectPath and UnityEditorPath.'
    }
    $taskProject = [IO.Path]::GetFullPath($ProjectPath).TrimEnd('\', '/')
    if ($taskProject -eq $taskRoot.TrimEnd('\', '/'))
    {
        throw 'Use an isolated verification project, not the user workspace.'
    }
    if (!(Test-Path -LiteralPath (Join-Path $taskProject 'ProjectSettings/ProjectVersion.txt')))
    {
        throw 'Not a Unity project.'
    }
    $taskLock = Join-Path $taskProject 'Temp/UnityLockfile'
    if (Test-Path -LiteralPath $taskLock)
    {
        try
        {
            $taskLockStream = [IO.File]::Open($taskLock, [IO.FileMode]::Open,
                [IO.FileAccess]::ReadWrite, [IO.FileShare]::None)
            $taskLockStream.Dispose()
        }
        catch
        {
            throw 'Verification project is already open; run Unity builds sequentially.'
        }
    }
    # 编译器生成资源、验证包标识及目标切换仅允许发生在显式隔离工程。
    Set-Content -LiteralPath (Join-Path $taskProject 'AFPSVerificationWorkspace.marker') -Value 'AFPS verification workspace' -Encoding UTF8
    $taskAndroid = $Action -eq 'BuildAndroid'
    $taskTarget = if ($taskAndroid) { 'Android' } else { 'Win64' }
    $taskBuildDirectory = Join-Path $taskOutput ($taskRun + '-' + $taskTarget)
    New-Item -ItemType Directory -Force -Path $taskBuildDirectory | Out-Null
    $taskPlayer = Join-Path $taskBuildDirectory $(if ($taskAndroid) { 'AFPSDeterminism.apk' } else { 'AFPSDeterminism.exe' })
    $taskMethod = if ($Action -eq 'BuildLoadWindows') { 'AFPS.Verification.Editor.ServerLoadVerificationBuild.Build' } else { 'AFPS.Verification.Editor.DeterminismVerificationBuild.Build' }
    $taskArguments = @('-batchmode', '-nographics', '-job-worker-count', '2', '-buildTarget', $taskTarget,
        '-projectPath', ('"' + $taskProject + '"'), '-executeMethod', $taskMethod,
        '-afpsVerificationBuildOutput', ('"' + $taskPlayer + '"'), '-logFile', ('"' + $taskLog + '"'))
    Wait-OwnedProcess (Start-Process -FilePath $UnityEditorPath -ArgumentList $taskArguments -WindowStyle Hidden -PassThru)
    if (!(Test-Path -LiteralPath ($taskPlayer + '.build.json')) -or !(Test-Path -LiteralPath $taskPlayer))
    {
        throw 'Build did not produce a complete artifact/status pair.'
    }
    $taskStatus = Get-Content -LiteralPath ($taskPlayer + '.build.json') -Raw | ConvertFrom-Json
    if ($taskStatus.result -ne 'Succeeded' -or $taskStatus.errors -ne 0)
    {
        throw 'Build status reports failure.'
    }
    Write-Output ('BUILD PASS: ' + $taskPlayer)
    exit 0
}
if (!(Test-Path -LiteralPath $PlayerPath -PathType Leaf))
{
    throw 'Run requires an existing PlayerPath.'
}
if ($Action -eq 'RunWindows')
{
    $taskArguments = @('-batchmode', '-nographics', '-job-worker-count', '2',
        '-afpsDeterminismOutput', ('"' + $taskResult + '"'), '-logFile', ('"' + $taskLog + '"'))
    Wait-OwnedProcess (Start-Process -FilePath $PlayerPath -ArgumentList $taskArguments -WindowStyle Hidden -PassThru)
}
else
{
    if (!$AdbPath -or !$DeviceSerial)
    {
        throw 'Android run requires explicit AdbPath and DeviceSerial; never auto-select a device.'
    }
    $taskPackage = 'com.afps.determinismprobe'
    function Invoke-ProbeAdb([string[]]$taskArgs)
    {
        $taskText = & $AdbPath -s $DeviceSerial @taskArgs 2>&1
        if ($LASTEXITCODE -ne 0)
        {
            throw ('adb failed: ' + ($taskText -join "`n"))
        }
        return $taskText
    }
    # 仅安装/运行独立调试探针包；不清空设备日志、不卸载游戏、不修改系统设置。
    Invoke-ProbeAdb @('install', '-r', [IO.Path]::GetFullPath($PlayerPath)) | Out-File -LiteralPath $taskLog -Encoding UTF8
    Invoke-ProbeAdb @('shell', 'run-as', $taskPackage, 'rm', '-f', 'files/AFPSDeterminism-result.json') | Out-Null
    Invoke-ProbeAdb @('shell', 'am', 'start', '-W', '-n', ($taskPackage + '/com.unity3d.player.UnityPlayerActivity')) |
        Out-File -LiteralPath $taskLog -Encoding UTF8 -Append
    $taskDeadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do
    {
        $taskText = & $AdbPath -s $DeviceSerial exec-out run-as $taskPackage cat files/AFPSDeterminism-result.json 2>$null
        if ($LASTEXITCODE -eq 0)
        {
            [IO.File]::WriteAllText($taskResult, ($taskText -join "`n"), (New-Object Text.UTF8Encoding($false)))
            break
        }
        if ([DateTime]::UtcNow -ge $taskDeadline)
        {
            throw ('Android probe produced no result; log=' + $taskLog)
        }
        Start-Sleep -Milliseconds 500
    } while ($true)
}
if (!(Test-Path -LiteralPath $taskResult))
{
    throw 'Probe exited without a result file.'
}
$taskProbe = Get-Content -LiteralPath $taskResult -Raw -Encoding UTF8 | ConvertFrom-Json
if ($taskProbe.passed -isnot [bool] -or !$taskProbe.passed)
{
    throw ('Probe failed: ' + $taskProbe.error)
}
Write-Output ('PROBE PASS: ' + $taskResult)
