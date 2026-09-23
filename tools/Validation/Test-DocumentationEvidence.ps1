[CmdletBinding()]
param()

$ErrorActionPreference = "Stop"
$tokens = $null
$parseErrors = $null
$scriptPath = Join-Path $PSScriptRoot "Test-Documentation.ps1"
$syntax = [System.Management.Automation.Language.Parser]::ParseFile($scriptPath, [ref]$tokens, [ref]$parseErrors)
if ($parseErrors.Count -ne 0) {
    throw "Documentation validator has PowerShell syntax errors."
}
$function = $syntax.Find({
    param($node)
    $node -is [System.Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq "Test-SealedBundleLog"
}, $false)
. ([scriptblock]::Create($function.Extent.Text))

$temporaryRoot = [System.IO.Path]::GetFullPath((Join-Path ([System.IO.Path]::GetTempPath()) ("documentation-evidence-" + [guid]::NewGuid())))
$bundle = Join-Path $temporaryRoot "codex/bundles/Fixture"
[void][System.IO.Directory]::CreateDirectory($bundle)
$relativeLog = "codex/bundles/Fixture/result.log"
$manifest = Join-Path $bundle "MANIFEST.sha256"
$log = Join-Path $bundle "result.log"
[System.IO.File]::WriteAllText($log, "durable result")
$hash = (Get-FileHash -LiteralPath $log -Algorithm SHA256).Hash.ToLowerInvariant()
$tracked = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
[void]$tracked.Add("codex/bundles/Fixture/MANIFEST.sha256")
$passed = 0

function Assert-Evidence {
    param([bool]$Expected, [string]$Label, [string]$Path = $relativeLog)
    $actual = Test-SealedBundleLog -Root $temporaryRoot -RelativePath $Path -TrackedPaths $tracked
    if ($actual -ne $Expected) {
        throw "Evidence assertion failed: $Label"
    }
    $script:passed++
}

try {
    [System.IO.File]::WriteAllText($manifest, "$hash  result.log`n")
    Assert-Evidence $true "sealed durable log"
    Assert-Evidence $false "log outside a bundle" "output/result.log"
    Assert-Evidence $false "PID is never log evidence" "codex/bundles/Fixture/result.pid"
    $tracked.Clear()
    Assert-Evidence $false "untracked manifest"
    [void]$tracked.Add("codex/bundles/Fixture/MANIFEST.sha256")
    [System.IO.File]::WriteAllText($log, "modified result")
    Assert-Evidence $false "modified log"
    [System.IO.File]::WriteAllText($log, "durable result")
    [System.IO.File]::WriteAllText($manifest, "$hash  another.log`n")
    Assert-Evidence $false "unlisted log"
    [System.IO.File]::WriteAllText($manifest, "$hash  result.log`n$hash  result.log`n")
    Assert-Evidence $false "duplicate manifest entry"
    [System.IO.File]::WriteAllText($manifest, "invalid  result.log`n")
    Assert-Evidence $false "malformed digest"
    [System.IO.File]::WriteAllText($manifest, "$hash  Result.log`n")
    Assert-Evidence $false "case-mismatched entry"
    Write-Host "Documentation evidence tests passed: $passed."
} finally {
    $resolvedRoot = [System.IO.Path]::GetFullPath($temporaryRoot)
    $tempParent = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath()).TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar
    if (-not $resolvedRoot.StartsWith($tempParent, [StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing cleanup outside the temporary directory."
    }
    Remove-Item -LiteralPath $resolvedRoot -Recurse -Force
}
