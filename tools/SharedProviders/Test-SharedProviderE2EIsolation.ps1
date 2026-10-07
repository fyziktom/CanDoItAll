#requires -Version 7.4
[CmdletBinding()]
param([string]$Configuration = "Release")

$ErrorActionPreference = "Stop"
$RepositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot "../.."))
$ArtifactParent = Join-Path $RepositoryRoot ".artifacts"
$ToolAssembly = Join-Path $PSScriptRoot "CanDoItAll.SharedProviders.E2E/bin/$Configuration/net10.0/CanDoItAll.SharedProviders.E2E.dll"
$FixtureRoot = Join-Path $ArtifactParent ("shared-providers-e2e-isolation-" + [Guid]::NewGuid().ToString("N"))
$UnownedRoot = "$FixtureRoot-unowned"
$UnexpectedRoot = Join-Path $ArtifactParent ("unrelated-" + [Guid]::NewGuid().ToString("N"))
$ownedRoots = @($FixtureRoot, $UnownedRoot)
$secretNames = @(
    "db-admin-password", "db-central-password", "db-client-a-password", "db-client-b-password",
    "db-central-connection-string", "db-client-a-connection-string", "db-client-b-connection-string",
    "api-central-signing-key", "api-client-a-signing-key", "api-client-b-signing-key",
    "upstream-data-token", "upstream-control-token", "personal-upstream-data-token", "personal-upstream-control-token"
)

function Get-SecretHashes {
    $secretNames | ForEach-Object {
        Get-FileHash -LiteralPath (Join-Path $FixtureRoot "runtime-secrets/$_") | Select-Object -ExpandProperty Hash
    }
}

function Invoke-Preparation {
    param([string]$Root, [bool]$Reset, [int]$ExpectedExitCode)
    $result = & dotnet $ToolAssembly prepare --repository-root $RepositoryRoot --artifact-root $Root --reset $Reset.ToString().ToLowerInvariant()
    if ($LASTEXITCODE -ne $ExpectedExitCode) {
        throw "Preparation returned an unexpected exit code: $LASTEXITCODE (expected $ExpectedExitCode)."
    }
    $receipt = $result | ConvertFrom-Json
    if ($receipt.Status -ne ($ExpectedExitCode -eq 0 ? "succeeded" : "failed")) {
        throw "Preparation returned an inconsistent native outcome."
    }
}

if (-not (Test-Path -LiteralPath $ToolAssembly -PathType Leaf)) {
    throw "Build the E2E tool in the selected configuration before testing isolation."
}
foreach ($root in $ownedRoots) {
    if (Test-Path -LiteralPath $root) {
        throw "The unique test root already exists."
    }
}

try {
    Invoke-Preparation $FixtureRoot $false 0
    $before = @(Get-SecretHashes)
    if ($before.Count -ne 14) {
        throw "Fresh preparation did not create the complete isolated secret set."
    }
    Invoke-Preparation $FixtureRoot $false 0
    $retained = @(Get-SecretHashes)
    if (Compare-Object $before $retained) {
        throw "Non-reset preparation replaced credentials."
    }
    Invoke-Preparation $FixtureRoot $true 0
    $replaced = @(Get-SecretHashes)
    if ($replaced.Count -ne 14 -or @($replaced | Where-Object { $_ -in $before }).Count -ne 0) {
        throw "Owned reset did not rotate the entire fixture secret set."
    }
    Invoke-Preparation (Join-Path $FixtureRoot "nested") $true 1
    if (Test-Path -LiteralPath (Join-Path $FixtureRoot "nested")) {
        throw "An invalid nested root was created."
    }
    Invoke-Preparation $UnexpectedRoot $true 1
    if (Test-Path -LiteralPath $UnexpectedRoot) {
        throw "An unrelated root was created."
    }
    New-Item -ItemType Directory -Path $UnownedRoot | Out-Null
    $canary = Join-Path $UnownedRoot "keep.txt"
    Set-Content -LiteralPath $canary -Value "Retain unowned data"
    Invoke-Preparation $UnownedRoot $true 1
    if ((Get-Content -LiteralPath $canary -Raw).Trim() -cne "Retain unowned data") {
        throw "Reset altered an unmarked directory."
    }
    Write-Output "PASS: 6 native preparation cases; isolated names, resume, owned reset and three refusal controls."
} finally {
    foreach ($root in $ownedRoots) {
        $resolved = [IO.Path]::GetFullPath($root)
        if ([IO.Path]::GetDirectoryName($resolved) -cne $ArtifactParent -or $resolved -notin $ownedRoots) {
            throw "Cleanup escaped the exact test-owned roots."
        }
        if (Test-Path -LiteralPath $resolved) {
            Remove-Item -LiteralPath $resolved -Recurse -Force
        }
    }
}
