[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ComponentsRepoRoot
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$required = @(
    "src/CanDoItAll.Components.BaseLib/wwwroot/css/material-symbols.css",
    "src/CanDoItAll.Components.BaseLib/wwwroot/css/output.css"
)

foreach ($relativePath in $required) {
    $path = Join-Path $ComponentsRepoRoot $relativePath
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required Components source asset is missing: $relativePath"
    }

    if ((Get-Item -LiteralPath $path).Length -eq 0) {
        throw "Required Components source asset is empty: $relativePath"
    }
}

Write-Host "Required Components source assets are present."
