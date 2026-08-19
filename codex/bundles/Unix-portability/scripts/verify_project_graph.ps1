param(
    [string]$RepositoryRoot,
    [string]$OutputPath
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../../..'))
}

$RepositoryRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
$projectFiles = @(& rg --files -g '*.csproj' $RepositoryRoot |
    ForEach-Object { Get-Item -LiteralPath $_ } |
    Sort-Object FullName)

$projectSet = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)
foreach ($project in $projectFiles) {
    [void]$projectSet.Add($project.FullName)
}

$adjacency = @{}
$unresolved = [System.Collections.Generic.List[object]]::new()
$referenceCount = 0

foreach ($project in $projectFiles) {
    $targets = [System.Collections.Generic.List[string]]::new()
    [xml]$document = Get-Content -LiteralPath $project.FullName -Raw
    foreach ($reference in @($document.SelectNodes("//*[local-name()='ProjectReference']"))) {
        $include = [string]$reference.Include
        if ([string]::IsNullOrWhiteSpace($include) -or $include.Contains('$(')) {
            $unresolved.Add([pscustomobject]@{
                project = [System.IO.Path]::GetRelativePath($RepositoryRoot, $project.FullName).Replace('\', '/')
                include = $include
            })
            continue
        }

        $targetPath = [System.IO.Path]::GetFullPath((Join-Path $project.DirectoryName $include))
        if ($projectSet.Contains($targetPath)) {
            $targets.Add($targetPath)
            $referenceCount++
        }
    }

    $adjacency[$project.FullName] = @($targets)
}

$state = @{}
$stack = [System.Collections.Generic.List[string]]::new()
$cyclicProjects = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

function Visit-Project {
    param([string]$ProjectPath)

    $state[$ProjectPath] = 1
    $stack.Add($ProjectPath)

    foreach ($targetPath in @($adjacency[$ProjectPath])) {
        $targetState = if ($state.ContainsKey($targetPath)) { [int]$state[$targetPath] } else { 0 }
        if ($targetState -eq 0) {
            Visit-Project -ProjectPath $targetPath
            continue
        }

        if ($targetState -ne 1) {
            continue
        }

        $cycleStart = $stack.IndexOf($targetPath)
        for ($index = $cycleStart; $index -lt $stack.Count; $index++) {
            [void]$cyclicProjects.Add($stack[$index])
        }
    }

    $stack.RemoveAt($stack.Count - 1)
    $state[$ProjectPath] = 2
}

foreach ($project in $projectFiles) {
    if (-not $state.ContainsKey($project.FullName)) {
        Visit-Project -ProjectPath $project.FullName
    }
}

$result = [ordered]@{
    schemaVersion = 1
    generatedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    repositoryRoot = $RepositoryRoot.Replace('\', '/')
    projectCount = $projectFiles.Count
    inRepositoryProjectReferences = $referenceCount
    cyclicProjectCount = $cyclicProjects.Count
    cyclicProjects = @($cyclicProjects | ForEach-Object {
        [System.IO.Path]::GetRelativePath($RepositoryRoot, $_).Replace('\', '/')
    } | Sort-Object)
    unresolvedProjectReferenceCount = $unresolved.Count
    unresolvedProjectReferences = @($unresolved)
}

$json = $result | ConvertTo-Json -Depth 6
if (-not [string]::IsNullOrWhiteSpace($OutputPath)) {
    $resolvedOutput = if ([System.IO.Path]::IsPathRooted($OutputPath)) {
        $OutputPath
    } else {
        Join-Path $RepositoryRoot $OutputPath
    }
    $parent = Split-Path -Parent $resolvedOutput
    if (-not [string]::IsNullOrWhiteSpace($parent)) {
        [System.IO.Directory]::CreateDirectory($parent) | Out-Null
    }
    Set-Content -LiteralPath $resolvedOutput -Value $json -Encoding utf8
}

$json
