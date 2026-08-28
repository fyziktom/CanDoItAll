param()
$ErrorActionPreference = 'Stop'
$taskRoot = (git rev-parse --show-toplevel).Trim()
$taskProof = $PSScriptRoot
Write-Output ('Closure audit UTC: ' + [DateTime]::UtcNow.ToString('o'))
Write-Output ('Working directory: ' + $taskRoot)
Write-Output 'Command: pwsh -File codex/bundles/shared-provider-real-catalogs/proof/SB07/Collect-Closure.ps1'
$taskSpecs = @(
    @{ Name = 'Unit'; Discovery = 'unit-verification-discovery.txt'; Result = 'unit-verification.trx'; Count = 206 },
    @{ Name = 'Components'; Discovery = 'components-verification-discovery.txt'; Result = 'components-verification.trx'; Count = 46 },
    @{ Name = 'Integration'; Discovery = 'relay-integration-verification-discovery.txt'; Result = 'relay-integration-after-discovery.trx'; Count = 56 }
)
foreach ($taskSpec in $taskSpecs) {
    [xml] $taskXml = Get-Content -Raw (Join-Path $taskProof $taskSpec.Result)
    $taskExpected = @(Get-Content (Join-Path $taskProof $taskSpec.Discovery) | ForEach-Object { $_.Trim() } | Where-Object { $_ -like 'CanDoItAll.Tests.*' })
    $taskActual = @($taskXml.TestRun.Results.UnitTestResult | ForEach-Object { $_.testName })
    $taskDifference = @(Compare-Object $taskExpected $taskActual -CaseSensitive)
    $taskFailures = @($taskXml.TestRun.Results.UnitTestResult | Where-Object outcome -ne Passed)
    if ($taskDifference.Count -ne 0 -or $taskActual.Count -ne $taskSpec.Count -or $taskFailures.Count -ne 0) {
        throw ('Discovery/result mismatch: ' + $taskSpec.Name)
    }
    Write-Output ($taskSpec.Name + ': exact discovery matched; all ' + $taskActual.Count + ' original records Passed.')
}
$taskPaths = @((git diff --name-only HEAD), (git ls-files --others --exclude-standard)) | ForEach-Object { $_ } | Sort-Object -Unique
$taskOwnedPaths = @($taskPaths | Where-Object { $_ -notmatch '/proof/' })
$taskCaptured = @{}
foreach ($taskRow in (Import-Csv (Join-Path $taskProof 'before-hashes.csv'))) {
    $taskCaptured[$taskRow.Path] = $taskRow.SHA256
}
$taskHashes = foreach ($taskPath in $taskOwnedPaths) {
    if (-not (Test-Path -LiteralPath (Join-Path $taskRoot $taskPath) -PathType Leaf)) {
        throw ('Missing changed file: ' + $taskPath)
    }
    $taskStart = [Diagnostics.ProcessStartInfo]::new('git')
    $taskStart.WorkingDirectory = $taskRoot
    $taskStart.UseShellExecute = $false
    $taskStart.CreateNoWindow = $true
    $taskStart.RedirectStandardOutput = $true
    $taskStart.RedirectStandardError = $true
    $taskStart.ArgumentList.Add('show')
    $taskStart.ArgumentList.Add('HEAD:' + $taskPath)
    $taskProcess = [Diagnostics.Process]::Start($taskStart)
    $taskBytes = [IO.MemoryStream]::new()
    $taskProcess.StandardOutput.BaseStream.CopyTo($taskBytes)
    $null = $taskProcess.StandardError.ReadToEnd()
    $taskProcess.WaitForExit()
    $taskBefore = if ($taskProcess.ExitCode -eq 0) { [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($taskBytes.ToArray())) } else { 'Absent in HEAD (new file)' }
    $taskBytes.Dispose()
    $taskProcess.Dispose()
    [pscustomobject]@{ Path = $taskPath; BeforeHeadBlobSha256 = $taskBefore; CapturedPreEditSha256 = $taskCaptured[$taskPath]; AfterSha256 = (Get-FileHash (Join-Path $taskRoot $taskPath) -Algorithm SHA256).Hash }
}
$taskHashes | Export-Csv -NoTypeInformation (Join-Path $taskProof 'changed-files.csv')
Write-Output ('Changed-file hashes: ' + $taskHashes.Count + '. HEAD blob provenance is distinct from exact captured pre-edit bytes.')
$taskSourcePaths = @($taskOwnedPaths | Where-Object { $_ -like 'src/*' })
Write-Output 'Anti-stub command: rg -n TODO|NotImplementedException|e2e-secondary-model|THINK-|Thinking Proof [changed production files]'
rg -n 'TODO|NotImplementedException|e2e-secondary-model|THINK-|Thinking Proof' -- $taskSourcePaths
if ($LASTEXITCODE -eq 0) {
    throw 'Inspect production placeholder/fixture-specific match before closure.'
}
if ($LASTEXITCODE -ne 1) {
    throw 'Production anti-stub search failed.'
}
Write-Output 'No production placeholder or validation-agent special case found.'
Write-Output 'Source assertions (supplementary to behavioral tests):'
rg -n 'OmitTemperature|ResolveTerminalCompletion|IsSuggested|sourceCapability|ThinkingCapabilities|SharedProviderThinkingCapabilityMapper|NumericOrdering' -- $taskSourcePaths
if ($LASTEXITCODE -ne 0) {
    throw 'Production ownership assertions missing.'
}
Write-Output 'SB07-I1: typed capability/independent override producer and consumer verified by original focused tests.'
Write-Output 'SB07-I2: actual-catalog suggestions, sorted names and preserved legacy choices verified by original focused tests.'
Write-Output 'SB07-I3: temperature/SDK envelope/Responses terminal regressions verified by original focused tests.'
Write-Output 'SB08-I1: see original MCP results plus correlated source dispatch/usage; no fixture invocation counts as live proof.'
Write-Output 'SB08-I2: see source-client-parity.json, inspected screenshots and final-health.json.'
git diff --check
if ($LASTEXITCODE -ne 0) {
    throw 'Whitespace validation failed.'
}
Write-Output 'Closure collector exit: 0. Broader suite failures remain explicitly reported separately.'
