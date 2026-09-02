[CmdletBinding()]
param(
    [Parameter(Mandatory)]
    [string[]]$PackageIds,

    [string]$Candidate = "0.3.0",

    [string]$Source = "https://api.nuget.org/v3/index.json",

    [string]$OutputPath = ".artifacts/ui-refactoring-integration/version-query.json"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

if ($PackageIds.Count -eq 0) {
    throw "At least one PackageIds value is required."
}

$serviceIndex = Invoke-RestMethod -Uri $Source -Method Get
$packageBaseResource = @(
    $serviceIndex.resources |
        Where-Object { $_.'@type' -like 'PackageBaseAddress*' } |
        Select-Object -First 1
)

if ($packageBaseResource.Count -ne 1) {
    throw "PackageBaseAddress was not found in NuGet service index '$Source'."
}

$packageBaseAddress = [string]$packageBaseResource[0].'@id'
if (-not $packageBaseAddress.EndsWith("/", [StringComparison]::Ordinal)) {
    $packageBaseAddress += "/"
}

$records = [System.Collections.Generic.List[object]]::new()
$candidateExists = $false

foreach ($packageId in $PackageIds | Sort-Object -Unique) {
    $normalizedId = $packageId.ToLowerInvariant()
    $versionsUri = "$packageBaseAddress$normalizedId/index.json"
    $versions = @()
    $packageExists = $true

    try {
        $response = Invoke-RestMethod -Uri $versionsUri -Method Get
        $versions = @($response.versions)
    }
    catch {
        $statusCode = $_.Exception.Response.StatusCode.value__
        if ($statusCode -eq 404) {
            $packageExists = $false
        }
        else {
            throw "Failed to query '$versionsUri': $($_.Exception.Message)"
        }
    }

    $matchesCandidate = @(
        $versions | Where-Object {
            [string]::Equals([string]$_, $Candidate, [StringComparison]::OrdinalIgnoreCase)
        }
    ).Count -gt 0

    if ($matchesCandidate) {
        $candidateExists = $true
    }

    $records.Add([pscustomobject]@{
        packageId = $packageId
        source = $Source
        versionsUri = $versionsUri
        packageExists = $packageExists
        candidate = $Candidate
        candidateExists = $matchesCandidate
        versions = $versions
    })
}

$outputFullPath = [System.IO.Path]::GetFullPath($OutputPath)
New-Item -ItemType Directory -Force -Path ([System.IO.Path]::GetDirectoryName($outputFullPath)) | Out-Null

$result = [ordered]@{
    checkedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    candidate = $Candidate
    source = $Source
    candidateExistsForAnyPackage = $candidateExists
    packages = @($records)
}
$result | ConvertTo-Json -Depth 8 |
    Set-Content -LiteralPath $outputFullPath -Encoding utf8

if ($candidateExists) {
    throw "Candidate version '$Candidate' already exists for at least one package. Select another coordinated version and rerun."
}

Write-Host "Candidate '$Candidate' is unused for the supplied package IDs on '$Source'."
Write-Host "Review '$outputFullPath' and repeat with authenticated repository tooling for every private feed."
