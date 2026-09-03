[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string]$ComponentsRepoRoot,

    [Parameter(Mandatory)]
    [string]$FileToolsRepoRoot,

    [Parameter(Mandatory)]
    [string]$MainRepoRoot,

    [Parameter(Mandatory)]
    [string]$ExpectedVersion
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Read-Property {
    param(
        [Parameter(Mandatory)][string]$File,
        [Parameter(Mandatory)][string]$Name
    )

    [xml]$document = Get-Content -LiteralPath $File -Raw
    $node = $document.SelectSingleNode(
        "/*[local-name()='Project']/*[local-name()='PropertyGroup']/*[local-name()='$Name']"
    )
    if ($null -eq $node) {
        throw "Property '$Name' was not found in '$File'."
    }

    return $node.InnerText.Trim()
}

$componentsProps = Join-Path $ComponentsRepoRoot "Directory.Build.props"
$fileToolsProps = Join-Path $FileToolsRepoRoot "Directory.Build.props"
$mainProps = Join-Path $MainRepoRoot "Directory.Build.props"

$actual = [ordered]@{
    Components = Read-Property $componentsProps "CanDoItAllPackageBaseVersion"
    FileTools = Read-Property $fileToolsProps "Version"
    MainComponentsFallback = Read-Property $mainProps "CanDoItAllComponentsPackageVersion"
    MainFileToolsFallback = Read-Property $mainProps "CanDoItAllFileToolsPackageVersion"
}

foreach ($entry in $actual.GetEnumerator()) {
    if ($entry.Value -ne $ExpectedVersion) {
        throw "$($entry.Key) version is '$($entry.Value)'; expected '$ExpectedVersion'."
    }
}

$overrideHits = @(
    Get-ChildItem -LiteralPath (Join-Path $FileToolsRepoRoot "src") -Filter "*.csproj" -Recurse |
        ForEach-Object {
            $text = Get-Content -LiteralPath $_.FullName -Raw
            if ($text -match "<Version>" -or $text -match "<PackageVersion>") {
                $_.FullName
            }
        }
)

if ($overrideHits.Count -gt 0) {
    throw "FileTools project-local version overrides remain:`n$($overrideHits -join [Environment]::NewLine)"
}

$actual | ConvertTo-Json
Write-Host "Version consistency passed for '$ExpectedVersion'."
