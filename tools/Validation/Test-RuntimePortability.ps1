param(
    [string]$RepositoryRoot,
    [string]$Configuration = 'Release',
    [string]$ResultsDirectory,
    [string]$CatalogPath,
    [string]$BuildStampPath,
    [bool]$UseLocalCanDoItAllLibraries = $true,
    [ValidateSet('All', 'Unit', 'Integration', 'Browser')]
    [string]$Scope = 'All',
    [switch]$SkipBuild,
    [switch]$BuildOnly,
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
$stampSchemaVersion = 2
$catalogSchemaVersion = 1

function Get-Sha256Text {
    param([Parameter(Mandatory)][string]$Value)

    $bytes = [System.Text.Encoding]::UTF8.GetBytes($Value)
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes)).ToLowerInvariant()
}

function Get-NormalizedRelativePath {
    param([Parameter(Mandatory)][string]$Path)

    return $Path.Replace('\', '/')
}

function Invoke-GitText {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][string[]]$Arguments
    )

    $output = & git -C $Root @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Git command failed with exit code $LASTEXITCODE."
    }

    return @($output)
}

function Test-IsBuildInputPath {
    param([Parameter(Mandatory)][string]$RelativePath)

    $normalized = Get-NormalizedRelativePath $RelativePath
    if ($normalized -match '^(src|tests|tools|\.github)/') {
        return $true
    }

    return $normalized -in @(
        'CanDoItAll.slnx',
        'Directory.Build.props',
        'Directory.Build.targets',
        'Directory.Packages.props',
        'global.json',
        'NuGet.Config',
        'compose.yaml',
        '.env.example'
    )
}

function Get-SourceFingerprint {
    param([Parameter(Mandatory)][string]$Root)

    $paths = Invoke-GitText -Root $Root -Arguments @('ls-files', '--cached', '--others', '--exclude-standard') |
        ForEach-Object { Get-NormalizedRelativePath $_ } |
        Where-Object { Test-IsBuildInputPath $_ } |
        Sort-Object -Unique
    if ($paths.Count -eq 0) {
        throw 'No build input paths were discovered for the source fingerprint.'
    }

    $records = foreach ($relativePath in $paths) {
        $fullPath = Join-Path $Root $relativePath
        if (Test-Path -LiteralPath $fullPath -PathType Leaf) {
            $hash = (Invoke-GitText -Root $Root -Arguments @('hash-object', '--', $relativePath) | Select-Object -First 1).Trim()
            if ([string]::IsNullOrWhiteSpace($hash)) {
                throw "Git did not hash build input '$relativePath'."
            }
        }
        else {
            $hash = '<deleted>'
        }

        "$relativePath`0$hash"
    }

    return Get-Sha256Text ($records -join "`n")
}

function Get-DependencySourceRecord {
    param([Parameter(Mandatory)][string]$Root)

    if (-not (Test-Path -LiteralPath (Join-Path $Root '.git'))) {
        throw "Source dependency is not a Git checkout: $Root"
    }

    $commit = (Invoke-GitText -Root $Root -Arguments @('rev-parse', 'HEAD') | Select-Object -First 1).Trim()
    return [ordered]@{
        commit = $commit
        sourceFingerprint = Get-SourceFingerprint $Root
    }
}

function Assert-CatalogScopeEntries {
    param([Parameter(Mandatory)][object]$CatalogScope)

    $entries = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($className in @($CatalogScope.expectedClasses)) {
        if ([string]::IsNullOrWhiteSpace($className) -or -not $entries.Add("class:$className")) {
            throw "Runtime portability catalog scope '$($CatalogScope.name)' contains an empty or duplicate class entry."
        }
    }

    foreach ($fullyQualifiedName in @($CatalogScope.expectedFullyQualifiedNames)) {
        if ([string]::IsNullOrWhiteSpace($fullyQualifiedName) -or -not $entries.Add("test:$fullyQualifiedName")) {
            throw "Runtime portability catalog scope '$($CatalogScope.name)' contains an empty or duplicate fully qualified test entry."
        }
    }
}

function Read-RuntimeCatalog {
    param([Parameter(Mandatory)][string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Runtime portability catalog is missing: $Path"
    }

    $catalog = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    if ($catalog.schemaVersion -ne $catalogSchemaVersion) {
        throw "Runtime portability catalog schema '$($catalog.schemaVersion)' is unsupported."
    }

    if ([string]::IsNullOrWhiteSpace($catalog.catalogVersion) -or
        [string]::IsNullOrWhiteSpace($catalog.traitFilter)) {
        throw 'Runtime portability catalog version and trait filter are required.'
    }

    $scopes = @($catalog.scopes)
    if ($scopes.Count -ne 3) {
        throw "Runtime portability catalog must contain exactly three scopes; found $($scopes.Count)."
    }

    $scopeNames = [Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
    foreach ($catalogScope in $scopes) {
        if (-not $scopeNames.Add([string]$catalogScope.name)) {
            throw "Runtime portability catalog contains duplicate scope '$($catalogScope.name)'."
        }

        if ($catalogScope.name -notin @('Unit', 'Integration', 'Browser') -or
            [string]::IsNullOrWhiteSpace($catalogScope.projectPath) -or
            [string]::IsNullOrWhiteSpace($catalogScope.assemblyPath) -or
            [string]::IsNullOrWhiteSpace($catalogScope.trxFileName) -or
            [int]$catalogScope.expectedCaseCount -lt 1) {
            throw "Runtime portability catalog scope '$($catalogScope.name)' is incomplete."
        }

        Assert-CatalogScopeEntries $catalogScope
    }

    return $catalog
}

function Get-AssemblyRecords {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][object]$Catalog,
        [Parameter(Mandatory)][string]$BuildConfiguration
    )

    return @($Catalog.scopes | ForEach-Object {
        $relativePath = ([string]$_.assemblyPath).Replace('{configuration}', $BuildConfiguration, [StringComparison]::Ordinal)
        $fullPath = Join-Path $Root $relativePath
        if (-not (Test-Path -LiteralPath $fullPath -PathType Leaf)) {
            throw "Runtime portability assembly is missing: $relativePath"
        }

        [ordered]@{
            path = Get-NormalizedRelativePath $relativePath
            sha256 = (Get-FileHash -LiteralPath $fullPath -Algorithm SHA256).Hash.ToLowerInvariant()
        }
    })
}

function Get-CurrentBuildStampModel {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][object]$Catalog,
        [Parameter(Mandatory)][string]$BuildConfiguration
    )

    $commit = (Invoke-GitText -Root $Root -Arguments @('rev-parse', 'HEAD') | Select-Object -First 1).Trim()
    $sdkVersion = (& dotnet --version).Trim()
    if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($sdkVersion)) {
        throw 'Could not resolve the active .NET SDK version.'
    }

    return [ordered]@{
        schemaVersion = $stampSchemaVersion
        repositoryCommit = $commit
        sourceFingerprint = Get-SourceFingerprint $Root
        configuration = $BuildConfiguration
        dependencyMode = if ($UseLocalCanDoItAllLibraries) { 'source' } else { 'package' }
        sdkVersion = $sdkVersion
        catalogVersion = [string]$Catalog.catalogVersion
        componentsSource = if ($UseLocalCanDoItAllLibraries) { Get-DependencySourceRecord $CanDoItAllComponentsRepositoryRoot } else { $null }
        fileToolsSource = if ($UseLocalCanDoItAllLibraries) { Get-DependencySourceRecord $CanDoItAllFileToolsRepositoryRoot } else { $null }
        assemblies = Get-AssemblyRecords -Root $Root -Catalog $Catalog -BuildConfiguration $BuildConfiguration
    }
}

function Assert-BuildStampModel {
    param(
        [Parameter(Mandatory)][object]$Stamp,
        [Parameter(Mandatory)][object]$Current
    )

    if ([int]$Stamp.schemaVersion -ne $stampSchemaVersion) {
        throw "Build stamp schema '$($Stamp.schemaVersion)' is unsupported."
    }

    if ($Stamp.repositoryCommit -ne $Current.repositoryCommit) {
        throw 'Build stamp has the wrong repository commit.'
    }

    if ($Stamp.sourceFingerprint -ne $Current.sourceFingerprint) {
        throw 'Build stamp is stale for the current source fingerprint.'
    }

    if ($Stamp.configuration -ne $Current.configuration) {
        throw 'Build stamp has the wrong build configuration.'
    }

    if ($Stamp.dependencyMode -ne $Current.dependencyMode) {
        throw 'Build stamp has the wrong dependency mode.'
    }

    if ($Stamp.sdkVersion -ne $Current.sdkVersion) {
        throw 'Build stamp has the wrong .NET SDK version.'
    }

    if ($Stamp.catalogVersion -ne $Current.catalogVersion) {
        throw 'Build stamp has the wrong runtime catalog version.'
    }

    foreach ($dependencyName in @('componentsSource', 'fileToolsSource')) {
        $stampedDependency = $Stamp.$dependencyName
        $currentDependency = $Current.$dependencyName
        if (($null -eq $stampedDependency) -ne ($null -eq $currentDependency) -or
            ($null -ne $currentDependency -and
             ($stampedDependency.commit -ne $currentDependency.commit -or
              $stampedDependency.sourceFingerprint -ne $currentDependency.sourceFingerprint))) {
            throw "Build stamp has stale $dependencyName dependency source."
        }
    }

    $stampedAssemblies = @($Stamp.assemblies)
    $currentAssemblies = @($Current.assemblies)
    if ($stampedAssemblies.Count -ne $currentAssemblies.Count) {
        throw 'Build stamp has the wrong assembly set.'
    }

    for ($index = 0; $index -lt $currentAssemblies.Count; $index++) {
        if ($stampedAssemblies[$index].path -ne $currentAssemblies[$index].path -or
            $stampedAssemblies[$index].sha256 -ne $currentAssemblies[$index].sha256) {
            throw "Build stamp assembly identity mismatch at index $index."
        }
    }
}

function Write-BuildStamp {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][object]$Model
    )

    $parent = Split-Path -Parent $Path
    [IO.Directory]::CreateDirectory($parent) | Out-Null
    $document = [ordered]@{}
    foreach ($property in $Model.GetEnumerator()) {
        $document[$property.Key] = $property.Value
    }

    $document.createdUtc = [DateTimeOffset]::UtcNow.ToString('O')
    [IO.File]::WriteAllText($Path, ($document | ConvertTo-Json -Depth 8) + [Environment]::NewLine)
}

function Assert-RuntimeResultModel {
    param(
        [Parameter(Mandatory)][object]$CatalogScope,
        [Parameter(Mandatory)][int]$ActualCount,
        [Parameter(Mandatory)][int]$FailedCount,
        [Parameter(Mandatory)][string[]]$ActualClasses,
        [Parameter(Mandatory)][string[]]$ActualFullyQualifiedNames
    )

    if ($ActualCount -eq 0) {
        throw "Runtime portability scope '$($CatalogScope.name)' discovered zero tests."
    }

    if ($ActualCount -ne [int]$CatalogScope.expectedCaseCount -or $FailedCount -ne 0) {
        throw "Runtime portability scope '$($CatalogScope.name)' expected $($CatalogScope.expectedCaseCount) passing cases; total=$ActualCount failed=$FailedCount."
    }

    $expectedClasses = @($CatalogScope.expectedClasses | Sort-Object -Unique)
    $actualClassSet = @($ActualClasses | Sort-Object -Unique)
    $missingClasses = @($expectedClasses | Where-Object { $_ -notin $actualClassSet })
    $unexpectedClasses = @($actualClassSet | Where-Object { $_ -notin $expectedClasses })
    if ($missingClasses.Count -ne 0 -or $unexpectedClasses.Count -ne 0) {
        throw "Runtime portability scope '$($CatalogScope.name)' class selection drifted. Missing=[$($missingClasses -join ', ')] Unexpected=[$($unexpectedClasses -join ', ')]"
    }

    $expectedTests = @($CatalogScope.expectedFullyQualifiedNames | Sort-Object -Unique)
    if ($expectedTests.Count -ne 0) {
        $actualTests = @($ActualFullyQualifiedNames | Sort-Object -Unique)
        $missingTests = @($expectedTests | Where-Object { $_ -notin $actualTests })
        $unexpectedTests = @($actualTests | Where-Object { $_ -notin $expectedTests })
        if ($missingTests.Count -ne 0 -or $unexpectedTests.Count -ne 0) {
            throw "Runtime portability scope '$($CatalogScope.name)' fully qualified test selection drifted. Missing=[$($missingTests -join ', ')] Unexpected=[$($unexpectedTests -join ', ')]"
        }
    }
}

function Assert-RuntimePortabilityTrx {
    param(
        [Parameter(Mandatory)][string]$Path,
        [Parameter(Mandatory)][object]$CatalogScope
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Runtime portability TRX was not produced: $Path"
    }

    [xml]$trx = Get-Content -LiteralPath $Path
    $namespace = [Xml.XmlNamespaceManager]::new($trx.NameTable)
    $namespace.AddNamespace('t', 'http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
    $summary = $trx.SelectSingleNode('//t:ResultSummary/t:Counters', $namespace)
    if ($null -eq $summary) {
        throw "Runtime portability TRX has no result counters: $Path"
    }

    $testMethods = @($trx.SelectNodes('//t:UnitTest/t:TestMethod', $namespace))
    $actualClasses = @($testMethods | ForEach-Object { [string]$_.className })
    $actualFullyQualifiedNames = @($testMethods | ForEach-Object { "$($_.className).$($_.name)" })
    $failedCount = [int]$summary.failed + [int]$summary.error + [int]$summary.timeout + [int]$summary.aborted
    Assert-RuntimeResultModel `
        -CatalogScope $CatalogScope `
        -ActualCount ([int]$summary.total) `
        -FailedCount $failedCount `
        -ActualClasses $actualClasses `
        -ActualFullyQualifiedNames $actualFullyQualifiedNames
}

function Invoke-RuntimePortabilityProject {
    param(
        [Parameter(Mandatory)][string]$Root,
        [Parameter(Mandatory)][object]$CatalogScope,
        [Parameter(Mandatory)][string]$TraitFilter,
        [Parameter(Mandatory)][string]$OutputDirectory
    )

    $arguments = @(
        'test',
        (Join-Path $Root $CatalogScope.projectPath),
        '--configuration', $Configuration,
        '--no-build',
        '--no-restore',
        '--nologo',
        '--filter', $TraitFilter,
        '--logger', "trx;LogFileName=$($CatalogScope.trxFileName)",
        '--results-directory', $OutputDirectory,
        "-p:UseLocalCanDoItAllLibraries=$($UseLocalCanDoItAllLibraries.ToString().ToLowerInvariant())"
    )
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Runtime portability tests failed for '$($CatalogScope.projectPath)' with exit code $LASTEXITCODE."
    }
}

function Assert-Throws {
    param(
        [Parameter(Mandatory)][string]$Name,
        [Parameter(Mandatory)][scriptblock]$Action
    )

    try {
        & $Action
    }
    catch {
        Write-Host "PASS negative fixture: $Name"
        return
    }

    throw "Negative fixture '$Name' did not fail."
}

function New-SelfTestStamp {
    param(
        [string]$Commit = 'commit-a',
        [string]$Fingerprint = 'source-a',
        [string]$DependencyMode = 'package',
        [string]$AssemblyHash = 'assembly-a'
    )

    return [pscustomobject]@{
        schemaVersion = $stampSchemaVersion
        repositoryCommit = $Commit
        sourceFingerprint = $Fingerprint
        configuration = 'Release'
        dependencyMode = $DependencyMode
        sdkVersion = '10.0.303'
        catalogVersion = 'catalog-a'
        componentsSource = $null
        fileToolsSource = $null
        assemblies = @([pscustomobject]@{ path = 'tests/test.dll'; sha256 = $AssemblyHash })
    }
}

function Invoke-RunnerSelfTests {
    $current = New-SelfTestStamp
    Assert-Throws -Name 'stale source stamp' -Action {
        Assert-BuildStampModel -Stamp (New-SelfTestStamp -Fingerprint 'source-old') -Current $current
    }
    Assert-Throws -Name 'wrong dependency mode' -Action {
        Assert-BuildStampModel -Stamp (New-SelfTestStamp -DependencyMode 'source') -Current $current
    }
    Assert-Throws -Name 'wrong repository commit' -Action {
        Assert-BuildStampModel -Stamp (New-SelfTestStamp -Commit 'commit-old') -Current $current
    }
    Assert-Throws -Name 'stale assembly hash' -Action {
        Assert-BuildStampModel -Stamp (New-SelfTestStamp -AssemblyHash 'assembly-old') -Current $current
    }

    $duplicateScope = [pscustomobject]@{
        name = 'Unit'
        projectPath = 'tests/test.csproj'
        assemblyPath = 'tests/test.dll'
        trxFileName = 'test.trx'
        expectedCaseCount = 1
        expectedClasses = @('Tests.Example', 'Tests.Example')
        expectedFullyQualifiedNames = @()
    }
    Assert-Throws -Name 'duplicate catalog entry' -Action {
        Assert-CatalogScopeEntries $duplicateScope
    }

    $resultScope = [pscustomobject]@{
        name = 'Browser'
        expectedCaseCount = 1
        expectedClasses = @('Tests.Example')
        expectedFullyQualifiedNames = @('Tests.Example.Expected_test')
    }
    Assert-Throws -Name 'missing fully qualified test' -Action {
        Assert-RuntimeResultModel -CatalogScope $resultScope -ActualCount 1 -FailedCount 0 -ActualClasses @('Tests.Example') -ActualFullyQualifiedNames @('Tests.Example.Other_test')
    }
    Assert-Throws -Name 'zero-test result' -Action {
        Assert-RuntimeResultModel -CatalogScope $resultScope -ActualCount 0 -FailedCount 0 -ActualClasses @() -ActualFullyQualifiedNames @()
    }

    Write-Host 'Runtime portability runner self-tests passed: 7/7.'
}

if ($SelfTest) {
    Invoke-RunnerSelfTests
    exit 0
}

if ($SkipBuild -and $BuildOnly) {
    throw 'SkipBuild and BuildOnly are mutually exclusive.'
}

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
}

$RepositoryRoot = [IO.Path]::GetFullPath($RepositoryRoot)
$CanDoItAllComponentsRepositoryRoot = [IO.Path]::GetFullPath((Join-Path $RepositoryRoot '..\CanDoItAll.Components'))
$CanDoItAllFileToolsRepositoryRoot = [IO.Path]::GetFullPath((Join-Path $RepositoryRoot '..\CanDoItAll.FileTools'))
if ([string]::IsNullOrWhiteSpace($ResultsDirectory)) {
    $ResultsDirectory = Join-Path $RepositoryRoot 'artifacts/runtime-portability'
}

$ResultsDirectory = [IO.Path]::GetFullPath($ResultsDirectory)
[IO.Directory]::CreateDirectory($ResultsDirectory) | Out-Null
if ([string]::IsNullOrWhiteSpace($CatalogPath)) {
    $CatalogPath = Join-Path $PSScriptRoot 'RuntimePortabilityCatalog.json'
}

$CatalogPath = [IO.Path]::GetFullPath($CatalogPath)
if ([string]::IsNullOrWhiteSpace($BuildStampPath)) {
    $BuildStampPath = Join-Path $ResultsDirectory 'runtime-portability-build-stamp.json'
}

$BuildStampPath = [IO.Path]::GetFullPath($BuildStampPath)

$catalog = Read-RuntimeCatalog $CatalogPath
if ($SkipBuild) {
    if (-not (Test-Path -LiteralPath $BuildStampPath -PathType Leaf)) {
        throw "SkipBuild requires a durable build stamp: $BuildStampPath"
    }

    $stamp = Get-Content -LiteralPath $BuildStampPath -Raw | ConvertFrom-Json
    $current = Get-CurrentBuildStampModel -Root $RepositoryRoot -Catalog $catalog -BuildConfiguration $Configuration
    Assert-BuildStampModel -Stamp $stamp -Current $current
}
else {
    $buildArguments = @(
        'build',
        (Join-Path $RepositoryRoot 'CanDoItAll.slnx'),
        '--configuration', $Configuration,
        '--no-restore',
        '--nologo',
        "-p:UseLocalCanDoItAllLibraries=$($UseLocalCanDoItAllLibraries.ToString().ToLowerInvariant())"
    )
    & dotnet @buildArguments
    if ($LASTEXITCODE -ne 0) {
        throw "Runtime portability Release build failed with exit code $LASTEXITCODE."
    }

    $current = Get-CurrentBuildStampModel -Root $RepositoryRoot -Catalog $catalog -BuildConfiguration $Configuration
    Write-BuildStamp -Path $BuildStampPath -Model $current
}

if ($BuildOnly) {
    Write-Host "Runtime portability build stamp created. Catalog=$($catalog.catalogVersion) stamp=$BuildStampPath"
    exit 0
}

$selectedScopes = @($catalog.scopes | Where-Object { $Scope -eq 'All' -or $_.name -eq $Scope })
foreach ($catalogScope in $selectedScopes) {
    Invoke-RuntimePortabilityProject -Root $RepositoryRoot -CatalogScope $catalogScope -TraitFilter $catalog.traitFilter -OutputDirectory $ResultsDirectory
    Assert-RuntimePortabilityTrx -Path (Join-Path $ResultsDirectory $catalogScope.trxFileName) -CatalogScope $catalogScope
}

$totalCases = ($selectedScopes | Measure-Object -Property expectedCaseCount -Sum).Sum
Write-Host "Runtime portability gate passed. Catalog=$($catalog.catalogVersion) scopes=$($selectedScopes.Count) cases=$totalCases evidence=$ResultsDirectory"
