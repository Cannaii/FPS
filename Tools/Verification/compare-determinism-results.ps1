param(
    [Parameter(Mandatory = $true)][string]$Baseline,
    [Parameter(Mandatory = $true)][string]$Candidate,
    [ValidateSet('Android', 'WindowsPlayer')][string]$ExpectedCandidatePlatform = 'Android',
    [ValidateSet('IL2CPP', 'Mono')][string]$ExpectedCandidateBackend = 'IL2CPP'
)
$ErrorActionPreference = 'Stop'
$taskBaseline = Get-Content -LiteralPath $Baseline -Raw -Encoding UTF8 | ConvertFrom-Json
$taskCandidate = Get-Content -LiteralPath $Candidate -Raw -Encoding UTF8 | ConvertFrom-Json
foreach ($taskResult in @($taskBaseline, $taskCandidate))
{
    if ($taskResult.passed -isnot [bool] -or !$taskResult.passed -or $taskResult.error)
    {
        throw 'Probe did not finish successfully.'
    }
    if ($taskResult.suite -cne 'AFPS-DET-1' -or $taskResult.frames -ne 2048 -or $taskResult.resets -ne 1)
    {
        throw 'Unexpected suite or incomplete frame sequence.'
    }
    foreach ($taskField in @('fixtureSha256', 'sourceSha256'))
    {
        if ($taskResult.$taskField -cnotmatch '^[0-9A-F]{64}$')
        {
            throw ('Missing or malformed SHA256: ' + $taskField)
        }
    }
    foreach ($taskField in @('worldHash', 'configurationHash', 'rollingHash', 'finalHash'))
    {
        if ($taskResult.$taskField -cnotmatch '^[0-9A-F]{16}$')
        {
            throw ('Missing or malformed state hash: ' + $taskField)
        }
    }
    if ($taskResult.pointerBits -ne 64 -or [string]::IsNullOrEmpty($taskResult.processor) -or
        [string]::IsNullOrEmpty($taskResult.unityVersion))
    {
        throw 'Missing 64-bit runtime metadata.'
    }
}
if ($taskBaseline.platform -cne 'WindowsPlayer' -or $taskBaseline.backend -cne 'Mono' -or
    $taskCandidate.platform -cne $ExpectedCandidatePlatform -or
    $taskCandidate.backend -cne $ExpectedCandidateBackend)
{
    throw 'Platform/backend does not match the requested acceptance target.'
}
foreach ($taskField in @('suite', 'fixtureSha256', 'sourceSha256', 'unityVersion', 'frames', 'resets',
    'algorithm', 'protocol', 'worldHash', 'configurationHash', 'rollingHash', 'finalHash'))
{
    if ($taskBaseline.$taskField -cne $taskCandidate.$taskField)
    {
        throw ('Determinism mismatch: ' + $taskField)
    }
}
if ($taskBaseline.algorithm -le 0 -or $taskBaseline.protocol -le 0)
{
    throw 'Missing simulation/protocol identity.'
}
Write-Output ('Determinism PASS: {0}/{1} vs {2}/{3}; frames={4}; rolling={5}' -f
    $taskBaseline.platform, $taskBaseline.backend, $taskCandidate.platform, $taskCandidate.backend,
    $taskBaseline.frames, $taskBaseline.rollingHash)
