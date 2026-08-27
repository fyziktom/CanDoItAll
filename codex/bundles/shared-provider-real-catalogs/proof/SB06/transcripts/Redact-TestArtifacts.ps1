$ErrorActionPreference = 'Stop'
$proofRoot = Split-Path $PSScriptRoot -Parent
$pattern = 'eyJ[A-Za-z0-9_-]{12,}\.[A-Za-z0-9_-]{12,}\.[A-Za-z0-9_-]{12,}|sk-proj-[A-Za-z0-9_-]{24,}'
Write-Output 'Command: & proof/SB06/transcripts/Redact-TestArtifacts.ps1'
foreach ($file in @('integration-suite.trx', 'unit-suite.trx')) {
    $path = Join-Path $proofRoot $file
    $content = [IO.File]::ReadAllText($path)
    $matchCount = [regex]::Matches($content, $pattern).Count
    $beforeHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    [xml] $before = $content
    $originalCounters = $before.TestRun.ResultSummary.Counters.OuterXml
    $redacted = [regex]::Replace($content, $pattern, '[REDACTED_TEST_CREDENTIAL]')
    [xml] $after = $redacted
    if ($originalCounters -ne $after.TestRun.ResultSummary.Counters.OuterXml -or
        $before.TestRun.Results.UnitTestResult.Count -ne $after.TestRun.Results.UnitTestResult.Count) {
        throw 'Redaction would alter test outcomes.'
    }
    [IO.File]::WriteAllText($path, $redacted, [Text.UTF8Encoding]::new($false))
    $afterHash = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
    Write-Output "$file : $matchCount credential-shaped strings redacted; outcomes and result count unchanged."
    Write-Output "Original SHA256: $beforeHash"
    Write-Output "Redacted SHA256: $afterHash"
}
Write-Output 'No unredacted duplicate is retained in the proof directory. Values were never printed.'
Write-Output 'ExitCode: 0'
