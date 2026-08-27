$ErrorActionPreference = 'Stop'
$repoRoot = (git rev-parse --show-toplevel).Trim()
$bundleRoot = Join-Path $repoRoot 'codex/bundles/shared-providers'
$unitRoot = Split-Path $PSScriptRoot -Parent
$transcriptPath = Join-Path $PSScriptRoot 'transcripts/closure-validation.txt'
Start-Transcript -Path $transcriptPath -Force | Out-Null
try {
    Write-Output "SPMETA META-NAMES META-PRICES META-PRIVATE META-SETTINGS META-E2E completed-stage artifact gate; cwd=$((Get-Location).Path)"
    Write-Output 'Command: proof/Validate-Closure.ps1; compatible feedback shape, not original SB07 closure'
    function Assert-Proof([bool] $Condition, [string] $Message) {
        if (-not $Condition) {
            throw $Message
        }
        Write-Output "PASS $Message"
    }
    function Resolve-ProofPath([string] $Reference) {
        if ($Reference.StartsWith('bundle://')) {
            return Join-Path $bundleRoot $Reference.Substring(9)
        }
        if ($Reference.StartsWith('repo://')) {
            return Join-Path $repoRoot $Reference.Substring(7)
        }
        throw "Unsupported proof reference: $Reference"
    }

    $expected = [ordered]@{
        'metadata-private-edit-final' = 161
        'metadata-save-consumers' = 217
        'metadata-integration-final' = 46
        'metadata-components-closure' = 38
        'metadata-ui-closure-2' = 1
        'metadata-ui-closure-repeat' = 1
    }
    $passedNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($entry in $expected.GetEnumerator()) {
        [xml]$run = Get-Content (Join-Path $PSScriptRoot "transcripts/$($entry.Key).trx") -Raw
        $counts = $run.TestRun.ResultSummary.Counters
        Assert-Proof ([int]$counts.total -eq $entry.Value -and [int]$counts.passed -eq $entry.Value -and [int]$counts.failed -eq 0 -and [int]$counts.notExecuted -eq 0) "$($entry.Key): $($entry.Value) executed/passed, no failures/skips"
        $transcript = Get-Content (Join-Path $PSScriptRoot "transcripts/$($entry.Key).txt") -Raw
        Assert-Proof ($transcript.Contains("Discovered tests: $($entry.Value);") -and $transcript.Contains('Exit code: 0') -and $transcript.Contains('Working directory:') -and $transcript.Contains('--list-tests --filter')) "$($entry.Key): command/discovery/cwd/exit evidence"
        foreach ($result in $run.TestRun.Results.UnitTestResult) {
            [void]$passedNames.Add($result.testName)
        }
    }

    $failingFirst = [ordered]@{
        'metadata-failing-first-authorized' = 3
        'metadata-removed-model-rerender' = 1
        'metadata-private-edit-failing-first' = 2
    }
    foreach ($entry in $failingFirst.GetEnumerator()) {
        [xml]$run = Get-Content (Join-Path $PSScriptRoot "transcripts/$($entry.Key).trx") -Raw
        Assert-Proof ([int]$run.TestRun.ResultSummary.Counters.failed -eq $entry.Value) "$($entry.Key): genuine $($entry.Value) failed tests before fix"
        foreach ($result in $run.TestRun.Results.UnitTestResult) {
            Assert-Proof ($result.outcome -eq 'Failed' -and $passedNames.Contains($result.testName)) "same adversarial test now passes: $($result.testName)"
        }
    }

    $manifest = Get-Content (Join-Path $PSScriptRoot 'manifest.md') -Raw
    $references = [regex]::Matches($manifest, '(?:bundle|repo)://[A-Za-z0-9_./-]+') |
        ForEach-Object { $_.Value.TrimEnd('.') } | Sort-Object -Unique
    foreach ($reference in $references) {
        Assert-Proof (Test-Path -LiteralPath (Resolve-ProofPath $reference)) "artifact reference resolves: $reference"
    }

    $index = Get-Content (Join-Path $PSScriptRoot 'changed-files.json') -Raw | ConvertFrom-Json
    Assert-Proof ($index.baseline -eq 'f092472ab83d36caf0e0fb52119d57d7aad35a65' -and $index.baseline -eq (git rev-parse HEAD).Trim()) 'baseline identity unchanged'
    Assert-Proof (-not $index.skillFilesChanged -and $index.files.Count -gt 30) 'all source/test/bundle changes indexed; no skill edits'
    foreach ($item in $index.files) {
        $actual = (Get-FileHash -LiteralPath (Resolve-ProofPath $item.path) -Algorithm SHA256).Hash.ToLowerInvariant()
        if ($actual -ne $item.after) {
            throw "Changed-file hash mismatch: $($item.path)"
        }
        if ($null -ne $item.before -and $item.before -notmatch '^[a-f0-9]{64}$') {
            throw "Invalid baseline hash: $($item.path)"
        }
    }
    $regeneratedText = node (Join-Path $PSScriptRoot 'Build-ProofIndex.mjs')
    Assert-Proof ($LASTEXITCODE -eq 0) 'read-only Git/worktree hash regeneration succeeds'
    $regenerated = $regeneratedText | ConvertFrom-Json
    Assert-Proof (($regenerated.files | ConvertTo-Json -Depth 5 -Compress) -eq ($index.files | ConvertTo-Json -Depth 5 -Compress)) "complete baseline and worktree hash set matches ($($index.files.Count) files)"

    $boundary = Get-Content (Join-Path $PSScriptRoot 'provider-boundary-closure.json') -Raw | ConvertFrom-Json
    Assert-Proof ($boundary.passed -and $boundary.violations.Count -eq 0) 'provider boundary validator passes'
    $source = Get-Content (Join-Path $PSScriptRoot 'transcripts/source-assertions.txt') -Raw
    Assert-Proof ($source.Contains('no stubs/fixture branches, project changes, or new partial boundaries. Exit code: 0')) 'production assertions and anti-stub gate pass'

    $runtime = Get-Content (Join-Path $PSScriptRoot 'transcripts/metadata-ui-closure-repeat-runtime.txt') -Raw
    foreach ($marker in @(
        'PASS production ledger counts: 8|8|1',
        'PASS newly generated PNG signature: 89 50 4e 47 0d 0a 1a 0a',
        'http://127.0.0.1:5210/health HTTP 200: Healthy',
        'http://127.0.0.1:5212/health HTTP 200: Healthy',
        'candoitall-spui-shared : 0 error/critical/unhandled-exception headings',
        'candoitall-spui-client : 0 error/critical/unhandled-exception headings',
        'candoitall-shared-providers-ui:spmeta-20260827-3',
        'Runtime evidence read completed. Exit code: 0'
    )) {
        Assert-Proof ($runtime.Contains($marker)) "runtime: $marker"
    }
    $build = Get-Content (Join-Path $PSScriptRoot 'transcripts/docker-closure-build.txt') -Raw
    Assert-Proof ($build.Contains('sha256:184a105104f916334d143cf42bc627221ad1e997f1141503c9beff567ebe79d6') -and $build.Contains('Docker build-log export exit code: 0')) 'final image build digest matches runtime tag'
    $testBuild = Get-Content (Join-Path $PSScriptRoot 'transcripts/playwright-closure-build.txt') -Raw
    Assert-Proof ($testBuild.Contains('Exit code: 0')) 'final browser-test build passes'

    $status = Get-Content (Join-Path $bundleRoot 'STATUS.md') -Raw
    Assert-Proof ($status.Contains('SPMETA` — `DONE') -and $status.Contains('| SB07 | `BLOCKED`')) 'SPMETA closed without falsely closing original SB07'
    $readme = Get-Content (Join-Path $unitRoot 'README.md') -Raw
    Assert-Proof (-not $readme.Contains('- [ ]') -and $readme.Contains('State: `DONE`')) 'work-unit acceptance complete'
    $inputs = Get-Content (Join-Path $unitRoot 'inputs.md') -Raw
    Assert-Proof (([regex]::Matches($inputs, '\| Solved')).Count -eq 5) 'all five raw input rows closed with evidence'
    foreach ($name in @('architecture-review.md','ui-review.md','semantic-review.md','01-execution-report.md')) {
        Assert-Proof (Test-Path -LiteralPath (Join-Path $unitRoot "reviews/$name")) "review artifact exists: $name"
    }

    $credentialFiles = @(Get-ChildItem -LiteralPath $unitRoot -Recurse -File |
        Where-Object Extension -in '.txt','.json','.md','.trx' |
        Where-Object { (Get-Content -LiteralPath $_.FullName -Raw) -match 'eyJ[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\.|sk-[A-Za-z0-9]{20,}' })
    Assert-Proof ($credentialFiles.Count -eq 0) 'no credential-shaped values in text proof (values never printed)'
    git diff --check
    Assert-Proof ($LASTEXITCODE -eq 0) 'git diff --check passes'
    Write-Output 'SPMETA completed-stage semantic/artifact gate: PASS. Original SB07 remains BLOCKED. Exit code: 0'
} catch {
    Write-Output "SPMETA closure gate FAILED: $($_.Exception.Message). Exit code: 1"
    throw
} finally {
    Stop-Transcript | Out-Null
}
