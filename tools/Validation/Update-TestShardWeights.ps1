param(
    [Parameter(Mandatory)][string[]]$TrxPath,
    [Parameter(Mandatory)][string]$Source,
    [string]$OutputPath
)

# Records measured per-class durations for Invoke-TestShard.ps1. Shards only need relative weights: a class that
# is missing here is weighted by its case count, so the file needs refreshing only when shards drift out of balance.

$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $PSScriptRoot 'TestShardWeights.json'
}

$classSeconds = @{}
$caseSeconds = [System.Collections.Generic.List[double]]::new()
foreach ($path in $TrxPath) {
    [xml]$trx = Get-Content -LiteralPath $path -Raw
    $namespace = @{ t = 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010' }
    $classById = @{}
    foreach ($definition in (Select-Xml -Xml $trx -XPath '//t:TestDefinitions/t:UnitTest' -Namespace $namespace).Node) {
        $classById[$definition.id] = $definition.TestMethod.className
    }

    foreach ($result in (Select-Xml -Xml $trx -XPath '//t:Results/t:UnitTestResult' -Namespace $namespace).Node) {
        $className = $classById[$result.testId]
        if (-not $className -or -not $result.duration) {
            continue
        }

        $seconds = [TimeSpan]::Parse($result.duration, [Globalization.CultureInfo]::InvariantCulture).TotalSeconds
        $classSeconds[$className] = [double]($classSeconds[$className]) + $seconds
        $caseSeconds.Add($seconds)
    }
}

if ($classSeconds.Count -eq 0) {
    throw 'The TRX files contain no timed test results.'
}

# Classes without history are weighted by their case count times the mean case duration.
$meanCaseSeconds = ($caseSeconds | Measure-Object -Sum).Sum / $caseSeconds.Count
$classes = [ordered]@{}
$names = [string[]]$classSeconds.Keys
[Array]::Sort($names, [StringComparer]::Ordinal)
foreach ($name in $names) {
    $classes[$name] = [Math]::Round($classSeconds[$name], 1)
}

[ordered]@{
    schemaVersion = 1
    source = $Source
    defaultCaseSeconds = [Math]::Round([Math]::Max($meanCaseSeconds, 0.01), 3)
    classes = $classes
} | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $OutputPath -Encoding utf8NoBOM

Write-Host "Wrote $($classes.Count) class weights to $OutputPath."
