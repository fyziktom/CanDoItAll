param()
$ErrorActionPreference = 'Stop'
$taskRoot = (git rev-parse --show-toplevel).Trim()
$taskTranscriptDirectory = Join-Path $PSScriptRoot 'transcripts'
Write-Output ('SB09 closure audit UTC: ' + [DateTime]::UtcNow.ToString('o'))
Write-Output ('Working directory: ' + $taskRoot)
$taskSpecs = @(
    @{ Name = 'Unit'; Label = 'unit-final'; Count = 138 },
    @{ Name = 'Components'; Label = 'components-layout-verified'; Count = 35 },
    @{ Name = 'Integration'; Label = 'integration-final'; Count = 56 }
)
$taskVerification = foreach ($taskSpec in $taskSpecs) {
    [xml] $taskXml = Get-Content -Raw (Join-Path $taskTranscriptDirectory ($taskSpec.Label + '.trx'))
    $taskExpected = @(Get-Content (Join-Path $taskTranscriptDirectory ($taskSpec.Label + '-discovery.txt')) | ForEach-Object { $_.Trim() } | Where-Object { $_ -like 'CanDoItAll.Tests.*' })
    $taskActual = @($taskXml.TestRun.Results.UnitTestResult | ForEach-Object { $_.testName })
    $taskDifference = @(Compare-Object $taskExpected $taskActual -CaseSensitive)
    $taskNonPass = @($taskXml.TestRun.Results.UnitTestResult | Where-Object outcome -ne Passed)
    if ($taskDifference.Count -ne 0 -or $taskActual.Count -ne $taskSpec.Count -or $taskNonPass.Count -ne 0) {
        throw ('Discovery/result mismatch: ' + $taskSpec.Name)
    }
    Write-Host ($taskSpec.Name + ': exact discovery matched; all ' + $taskActual.Count + ' original records Passed.')
    [pscustomobject]@{ Suite = $taskSpec.Name; Label = $taskSpec.Label; Discovered = $taskExpected.Count; Executed = $taskActual.Count; ExactMatch = $true }
}
$taskVerification | ConvertTo-Json | Out-File (Join-Path $PSScriptRoot 'discovery-verification.json')
[xml] $taskRed = Get-Content -Raw (Join-Path $taskTranscriptDirectory 'failing-first.trx')
if (@($taskRed.TestRun.Results.UnitTestResult | Where-Object outcome -eq Failed).Count -ne 9) {
    throw 'Original nine failing regressions not present.'
}
Write-Output 'Original failing-first: nine failures; manual precedence, validation and sharing were absent.'
$taskBroad = foreach ($taskSuite in @('unit', 'components', 'integration')) {
    [xml] $taskXml = Get-Content -Raw (Join-Path $taskTranscriptDirectory ($taskSuite + '-broad.trx'))
    [xml] $taskBaseline = Get-Content -Raw (Join-Path $PSScriptRoot ('../SB07/' + $taskSuite + '-broad.trx'))
    $taskResults = @($taskXml.TestRun.Results.UnitTestResult)
    $taskFailed = @($taskResults | Where-Object outcome -eq Failed)
    $taskOldFailures = @($taskBaseline.TestRun.Results.UnitTestResult | Where-Object outcome -eq Failed | ForEach-Object testName)
    $taskNewFailures = @($taskFailed | Where-Object { $_.testName -cnotin $taskOldFailures })
    if ($taskResults.Count -eq 0) {
        throw 'Broad suite executed zero tests.'
    }
    $taskDiscovered = @(Get-Content (Join-Path $taskTranscriptDirectory ($taskSuite + '-broad-discovery.txt')) | ForEach-Object { $_.Trim() } | Where-Object { $_ -like 'CanDoItAll.Tests.*' })
    $taskActualNames = @($taskResults | ForEach-Object testName)
    $taskExpansions = @(foreach ($taskName in $taskDiscovered) {
        if ($taskName -cin $taskActualNames) {
            continue
        }
        $taskExpanded = @($taskActualNames | Where-Object { $_.StartsWith($taskName + '(', [StringComparison]::Ordinal) })
        if ($taskExpanded.Count -eq 0) {
            throw ('Broad discovered test has no original result: ' + $taskName)
        }
        [pscustomobject]@{ Theory = $taskName; OriginalResultRows = $taskExpanded.Count }
    })
    foreach ($taskName in $taskActualNames) {
        if ($taskName -cnotin $taskDiscovered -and ($taskName -split '\(', 2)[0] -cnotin $taskDiscovered) {
            throw ('Broad result not covered by frozen discovery: ' + ($taskName -split '\(', 2)[0])
        }
    }
    [pscustomobject]@{ Suite = $taskSuite; Discovered = $taskDiscovered.Count; Total = $taskResults.Count; Passed = @($taskResults | Where-Object outcome -eq Passed).Count; Failed = $taskFailed.Count; Skipped = @($taskResults | Where-Object outcome -eq NotExecuted).Count; NewFailures = @($taskNewFailures | ForEach-Object testName); ExpandedTheories = $taskExpansions }
    $taskFailed | ForEach-Object { [pscustomobject]@{ Name = $_.testName; PreviouslyFailed = $_.testName -cin $taskOldFailures; Message = $_.Output.ErrorInfo.Message } } | Export-Csv -NoTypeInformation (Join-Path $PSScriptRoot ($taskSuite + '-broad-failures.csv'))
}
$taskBroad | ConvertTo-Json -Depth 4 | Out-File (Join-Path $PSScriptRoot 'broad-comparison.json')
$taskBroad | Format-Table Suite,Total,Passed,Failed,Skipped
if (@($taskBroad | Where-Object { $_.NewFailures.Count -gt 0 }).Count -gt 0) {
    throw 'New broad failures require review; do not claim unrelated baseline.'
}
Write-Output 'Every broad failure identity is present in SB07; failure causes still require source review.'
$taskCaptured = @{}
Import-Csv (Join-Path $PSScriptRoot 'before-hashes.csv') | ForEach-Object { $taskCaptured[$_.Path] = $_.SHA256 }
$taskPaths = @((git diff --name-only HEAD), (git ls-files --others --exclude-standard)) | ForEach-Object { $_ } | Sort-Object -Unique
$taskOwned = @($taskPaths | Where-Object { $_ -notmatch '/proof/' })
$taskHashes = foreach ($taskPath in $taskOwned) {
    $taskBefore = if ($taskCaptured.ContainsKey($taskPath)) { $taskCaptured[$taskPath] } else { 'ABSENT (new file)' }
    [pscustomobject]@{ Path = $taskPath; BeforeSha256 = $taskBefore; AfterSha256 = (Get-FileHash -LiteralPath (Join-Path $taskRoot $taskPath)).Hash }
}
$taskHashes | Export-Csv -NoTypeInformation (Join-Path $PSScriptRoot 'changed-files.csv')
Write-Output ('Before/after source and bundle hash rows: ' + $taskHashes.Count)
$taskProduction = @($taskOwned | Where-Object { $_ -like 'src/*' })
rg -n 'TODO|NotImplementedException|e2e-secondary-model|Thinking Proof|MODEL-SOL' -- $taskProduction
if ($LASTEXITCODE -ne 1) {
    throw 'Production anti-stub audit requires review.'
}
Write-Output 'No production test-provider branch, placeholder, fixture ID or fabricated model found.'
rg -n 'ProviderModelThinkingConfiguration|SourceManaged|IsSourceManaged|RefreshSharedProvider|SharedProviderRefreshButton|NumericOrdering' -- $taskProduction
if ($LASTEXITCODE -ne 0) {
    throw 'Expected production ownership assertions absent.'
}
git diff --check
if ($LASTEXITCODE -ne 0) {
    throw 'Whitespace validation failed.'
}
Write-Output 'SB09-I1/I2/I3: typed precedence, strict validation, default inheritance and shared-only ownership are behaviorally tested.'
Write-Output 'SB10-I1/I2/I3: original MCP results, inspected screenshots, source dispatch/usage and final image health are in SB10.'
Write-Output 'Closure collector exit: 0. Broad suites are explicitly not green.'
