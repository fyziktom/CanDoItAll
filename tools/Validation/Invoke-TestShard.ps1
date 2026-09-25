param(
    [Parameter(Mandatory)][string[]]$Project,
    [Parameter(Mandatory)][string]$Filter,
    [int]$ShardIndex = 1,
    [int]$ShardCount = 1,
    [string]$Configuration = 'Release',
    [string]$ResultsDirectory,
    [string]$LogPrefix = 'shard',
    [string]$WeightsPath,
    [bool]$UseLocalCanDoItAllLibraries = $true,
    [switch]$ListOnly
)

# Runs one deterministic, class-based shard of the given test projects. Every shard lists the same tests with
# the same filter and partitions whole test classes with the same greedy duration packing, so the shards of one
# commit cover each listed test exactly once. Tests inside a shard keep their assembly's own execution policy;
# sharding adds separate processes, never concurrency inside one test process.

$ErrorActionPreference = 'Stop'
$testNamePattern = '^\s{4}([A-Za-z_][\w.+`]*\.[\w`]+)(\(.*\))?\s*$'

if ($ShardCount -lt 1 -or $ShardIndex -lt 1 -or $ShardIndex -gt $ShardCount) {
    throw "ShardIndex must be between 1 and ShardCount; received $ShardIndex of $ShardCount."
}

$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..' '..')).Path
if ([string]::IsNullOrWhiteSpace($ResultsDirectory)) {
    $ResultsDirectory = Join-Path $repositoryRoot 'output' 'test-shards'
}
New-Item -ItemType Directory -Force -Path $ResultsDirectory | Out-Null
$localLibraries = "-p:UseLocalCanDoItAllLibraries=$($UseLocalCanDoItAllLibraries.ToString().ToLowerInvariant())"

$weights = @{}
$defaultCaseSeconds = 1.0
if (-not [string]::IsNullOrWhiteSpace($WeightsPath)) {
    $document = Get-Content -LiteralPath $WeightsPath -Raw | ConvertFrom-Json
    foreach ($property in $document.classes.PSObject.Properties) {
        $weights[$property.Name] = [double]$property.Value
    }
    if ($document.defaultCaseSeconds) {
        $defaultCaseSeconds = [double]$document.defaultCaseSeconds
    }
}

function Get-ListedTests {
    param([Parameter(Mandatory)][string]$ProjectPath)

    $output = & dotnet test $ProjectPath --configuration $Configuration --no-build --no-restore --list-tests --filter $Filter $localLibraries 2>&1
    if ($LASTEXITCODE -ne 0) {
        $output | Write-Host
        throw "Test discovery failed for $ProjectPath with exit code $LASTEXITCODE."
    }

    foreach ($line in $output) {
        $match = [regex]::Match([string]$line, $testNamePattern)
        if ($match.Success) {
            $match.Groups[1].Value
        }
    }
}

$classes = [System.Collections.Generic.List[object]]::new()
$listedByProject = @{}
foreach ($projectPath in $Project) {
    $methods = @(Get-ListedTests -ProjectPath $projectPath)
    $listedByProject[$projectPath] = $methods
    $methods |
        Group-Object -CaseSensitive { $_.Substring(0, $_.LastIndexOf('.')) } |
        ForEach-Object {
            $weight = if ($weights.ContainsKey($_.Name)) { $weights[$_.Name] } else { $_.Count * $defaultCaseSeconds }
            $classes.Add([pscustomobject]@{ Project = $projectPath; Name = $_.Name; Cases = $_.Count; Weight = $weight; Shard = 0 })
        }
}

if ($classes.Count -eq 0) {
    throw "The filter selects no tests in $($Project -join ', ')."
}

# Heaviest classes first, ties in ordinal name order, so every shard on every host computes the same partition.
$classes.Sort([Comparison[object]] {
        param($left, $right)
        $byWeight = $right.Weight.CompareTo($left.Weight)
        if ($byWeight -ne 0) { $byWeight } else { [string]::CompareOrdinal($left.Name, $right.Name) }
    })
$loads = [double[]]::new($ShardCount)
foreach ($class in $classes) {
    $target = 0
    for ($index = 1; $index -lt $ShardCount; $index++) {
        if ($loads[$index] -lt $loads[$target]) {
            $target = $index
        }
    }
    $class.Shard = $target + 1
    $loads[$target] += $class.Weight
}

# A class selector ("FullyQualifiedName~<class>.") matches every test whose fully qualified name contains it.
# Every possible match is a dot-bounded substring of the name, so checking those substrings proves that each
# listed test is claimed by exactly one selector: no test runs twice or disappears between shards.
$selectorTokens = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
foreach ($class in $classes) {
    [void]$selectorTokens.Add("$($class.Name).")
}
foreach ($projectPath in $Project) {
    foreach ($method in $listedByProject[$projectPath]) {
        $owners = 0
        for ($start = 0; $start -ge 0 -and $start -lt $method.Length; $start = $method.IndexOf('.', $start) + 1) {
            for ($end = $method.IndexOf('.', $start); $end -ge 0; $end = $method.IndexOf('.', $end + 1)) {
                if ($selectorTokens.Contains($method.Substring($start, $end - $start + 1))) {
                    $owners++
                }
            }
            if ($method.IndexOf('.', $start) -lt 0) {
                break
            }
        }
        if ($owners -ne 1) {
            throw "Test $method matches $owners class selectors; the shard partition would not run it exactly once."
        }
    }
}

for ($index = 0; $index -lt $ShardCount; $index++) {
    $shardClasses = @($classes | Where-Object Shard -eq ($index + 1))
    $cases = ($shardClasses | Measure-Object -Property Cases -Sum).Sum
    Write-Host ("Shard {0}/{1}: {2} classes, {3} listed cases, estimated {4:n0} s" -f ($index + 1), $ShardCount, $shardClasses.Count, $cases, $loads[$index])
}

$failures = 0
$executedProjects = 0
foreach ($projectPath in $Project) {
    $ownClasses = @($classes | Where-Object { $_.Project -eq $projectPath -and $_.Shard -eq $ShardIndex } | Sort-Object Name)
    if ($ownClasses.Count -eq 0) {
        continue
    }

    $assemblyName = [IO.Path]::GetFileNameWithoutExtension($projectPath)
    $expectedCases = ($ownClasses | Measure-Object -Property Cases -Sum).Sum
    $classFilter = ($ownClasses | ForEach-Object { "FullyQualifiedName~$($_.Name)." }) -join '|'
    $shardFilter = "($Filter)&($classFilter)"
    Write-Host "Shard $ShardIndex/$ShardCount runs $($ownClasses.Count) classes ($expectedCases listed cases) from $assemblyName."
    if ($ListOnly) {
        $ownClasses | ForEach-Object { Write-Host "  $($_.Name) [$($_.Cases)]" }
        continue
    }

    $executedProjects++
    $trxPath = Join-Path $ResultsDirectory "$LogPrefix-$assemblyName.trx"
    Remove-Item -LiteralPath $trxPath -Force -ErrorAction SilentlyContinue
    & dotnet test $projectPath --configuration $Configuration --no-build --no-restore `
        --filter $shardFilter `
        --logger "trx;LogFileName=$LogPrefix-$assemblyName.trx" `
        --results-directory $ResultsDirectory `
        $localLibraries
    if ($LASTEXITCODE -ne 0) {
        $failures++
    }

    # A theory whose data cannot be serialized is listed once and reports every row, so a shard may report
    # more results than it listed, never fewer.
    if (-not (Test-Path -LiteralPath $trxPath -PathType Leaf)) {
        Write-Host "Shard $ShardIndex/$ShardCount produced no results file for $assemblyName."
        $failures++
        continue
    }

    [xml]$trx = Get-Content -LiteralPath $trxPath -Raw
    $reported = [int]$trx.TestRun.ResultSummary.Counters.total
    Write-Host "Shard $ShardIndex/$ShardCount reported $reported results for $expectedCases listed cases from $assemblyName."
    if ($reported -lt $expectedCases) {
        $failures++
    }
}

if (-not $ListOnly -and $executedProjects -eq 0) {
    throw "Shard $ShardIndex/$ShardCount received no test classes; lower the shard count."
}

if ($failures -gt 0) {
    exit 1
}
