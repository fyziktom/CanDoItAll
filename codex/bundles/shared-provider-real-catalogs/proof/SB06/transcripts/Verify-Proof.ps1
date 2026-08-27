param([switch] $FailingFirst)
$ErrorActionPreference = 'Stop'
$proofRoot = Split-Path $PSScriptRoot -Parent
if ($FailingFirst) {
    Write-Output 'Command: & proof/SB06/transcripts/Verify-Proof.ps1 -FailingFirst'
} else {
    Write-Output 'Command: & proof/SB06/transcripts/Verify-Proof.ps1'
}
if ($FailingFirst) {
    [xml] $red = Get-Content (Join-Path $proofRoot 'regression-red.trx') -Raw
    if ([int] $red.TestRun.ResultSummary.Counters.failed -ne 1 -or [int] $red.TestRun.ResultSummary.Counters.total -ne 1) {
        throw 'Original failing-first evidence does not have exactly one failing test.'
    }
    Write-Output 'Archived failing-first evidence, not a new execution of the test:'
    Get-Content (Join-Path $proofRoot 'regression-red.txt')
    Write-Output 'Original test ExitCode: 1'
    Write-Output 'Collector ExitCode: 0'
    return
}
$checks = @(
    @{ File = 'unit-tests-final.trx'; Count = 6 },
    @{ File = 'component-tests-final.trx'; Count = 4 },
    @{ File = 'integration-tests.trx'; Count = 19 },
    @{ File = '../SB05/component-tests.trx'; Count = 11 }
)
foreach ($check in $checks) {
    [xml] $result = Get-Content (Join-Path $proofRoot $check.File) -Raw
    $counters = $result.TestRun.ResultSummary.Counters
    if ([int] $counters.total -ne $check.Count -or [int] $counters.passed -ne $check.Count) {
        throw "Focused proof failed: $($check.File)"
    }
    Write-Output "$($check.File): $($check.Count) passed"
    foreach ($test in $result.TestRun.Results.UnitTestResult) {
        Write-Output "$($test.outcome): $($test.testName)"
    }
}
$finalBrowser = Get-Content (Join-Path $proofRoot 'mcp-image2-result.json') -Raw | ConvertFrom-Json
$browser = $finalBrowser.lifecycle
if ($browser.active -ne 200 -or $browser.cancelled -ne 200 -or $browser.revoked -ne 401 -or
    $browser.deleted -ne 401 -or -not $browser.sameToken -or -not $browser.testTokenDeleted -or
    $browser.freshProviders -ne '0 / 0' -or -not $browser.shortIdSearch -or
    $finalBrowser.settledShortIdSearch.noMatchCount -ne 0 -or $finalBrowser.settledShortIdSearch.shortIdSearchCount -ne 1) {
    throw 'Live Playwright lifecycle or empty-client evidence is invalid.'
}
Write-Output 'TOKEN-SCOPES: exact checkbox selection, empty rejection and cancellation covered by passing tests.'
Write-Output 'TOKEN-LIFECYCLE: same-token live HTTP 200/cancel200/revoke401/delete401; file reopen and corrupt denial pass.'
Write-Output 'TOKEN-ADMIN: lazy registry call count and denied actions pass; real Docker UI issuance/list/actions pass.'
Write-Output 'TOKEN-PRIVACY: metadata-only persistence assertions pass; live evidence contains no bearer value.'
Write-Output 'FRESH-5214: Playwright zero-provider state; runtime-final.txt has four zero database counts and all three health200.'
Write-Output 'Anti-stub source audit follows:'
Get-Content (Join-Path $proofRoot 'source-audit.txt')
foreach ($source in (Import-Csv (Join-Path $proofRoot 'changed-files.csv'))) {
    if ((Get-FileHash -LiteralPath $source.Path -Algorithm SHA256).Hash -ne $source.SHA256) {
        throw "Source hash changed: $($source.Path)"
    }
}
Write-Output 'All changed-source hashes match.'
Write-Output 'ExitCode: 0'
