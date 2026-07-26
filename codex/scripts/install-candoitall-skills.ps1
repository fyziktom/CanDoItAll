[CmdletBinding()]
param(
    [string]$CodexHome = $(if ($env:CODEX_HOME) { $env:CODEX_HOME } else { Join-Path ([Environment]::GetFolderPath("UserProfile")) ".codex" }),
    [string]$SharedInfoRepoRoot = "",
    [string]$OpenAiSkillsRepoUrl = "https://github.com/openai/skills.git",
    [string]$OpenAiSkillsCache = $(Join-Path $env:TEMP "codex-openai-skills-cache"),
    [string]$DotNetSkillsRepoUrl = "https://github.com/dotnet/skills.git",
    [string]$DotNetSkillsCache = $(Join-Path $env:TEMP "codex-dotnet-skills-cache"),
    [switch]$SkipCustomSkills,
    [switch]$SkipPublicSkills
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
if ([string]::IsNullOrWhiteSpace($SharedInfoRepoRoot)) {
    if (-not [string]::IsNullOrWhiteSpace($env:CANDOITALL_SHAREDINFO_ROOT)) {
        $SharedInfoRepoRoot = $env:CANDOITALL_SHAREDINFO_ROOT
    }
    else {
        $SharedInfoRepoRoot = Join-Path (Split-Path -Parent $repoRoot) "CanDoItAll.SharedInfo"
    }
}

$SharedInfoRepoRoot = [System.IO.Path]::GetFullPath($SharedInfoRepoRoot)
$sharedInfoInstallerPath = Join-Path $SharedInfoRepoRoot "tools\install\codex\Install-CodexSkills.ps1"
if (-not (Test-Path -LiteralPath $sharedInfoInstallerPath -PathType Leaf)) {
    throw "The canonical SharedInfo Codex skill installer was not found at '$sharedInfoInstallerPath'."
}

$targetSkillRoot = Join-Path $CodexHome "skills"
$publicSkillSources = @(
    [pscustomobject]@{
        Name           = "openai/skills"
        RepoUrl        = $OpenAiSkillsRepoUrl
        CacheDirectory = $OpenAiSkillsCache
        PreferredPaths = @{
            "openai-docs" = "skills\.system\openai-docs"
        }
        SkillNames     = @(
            "openai-docs",
            "playwright",
            "screenshot",
            "imagegen"
        )
    },
    [pscustomobject]@{
        Name           = "dotnet/skills"
        RepoUrl        = $DotNetSkillsRepoUrl
        CacheDirectory = $DotNetSkillsCache
        SkillNames     = @(
            "mtp-hot-reload"
        )
    }
)

$installedSkills = New-Object System.Collections.Generic.List[object]

function Write-Step {
    param([string]$Message)
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Ensure-Directory {
    param([string]$Path)
    if (-not (Test-Path $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

function Find-SkillDirectory {
    param(
        [string]$Root,
        [string]$SkillName,
        [string]$PreferredRelativePath
    )

    if ($PreferredRelativePath) {
        $preferredPath = Join-Path $Root $PreferredRelativePath
        if (-not (Test-Path (Join-Path $preferredPath "SKILL.md"))) {
            throw "Preferred path for skill '$SkillName' does not contain SKILL.md: $preferredPath"
        }

        return $preferredPath
    }

    $matches = Get-ChildItem -Path $Root -Directory -Recurse |
        Where-Object {
            $_.Name -eq $SkillName -and
            (Test-Path (Join-Path $_.FullName "SKILL.md"))
        }

    if (-not $matches) {
        throw "Could not find skill '$SkillName' under '$Root'."
    }

    if ($matches.Count -gt 1) {
        $paths = $matches | ForEach-Object { $_.FullName }
        throw "Found multiple matches for skill '$SkillName': $($paths -join '; ')"
    }

    return $matches[0].FullName
}

function Remove-TargetDirectoryIfPresent {
    param(
        [string]$TargetDirectory
    )

    $resolvedTargetRoot = [System.IO.Path]::GetFullPath($targetSkillRoot)
    $resolvedTargetDirectory = [System.IO.Path]::GetFullPath($TargetDirectory)
    $expectedPrefix = $resolvedTargetRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar) + [System.IO.Path]::DirectorySeparatorChar

    if (-not $resolvedTargetDirectory.StartsWith($expectedPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove directory outside target skill root: $resolvedTargetDirectory"
    }

    if (Test-Path $resolvedTargetDirectory) {
        Remove-Item -LiteralPath $resolvedTargetDirectory -Recurse -Force
    }
}

function Install-SkillDirectory {
    param(
        [string]$SourceDirectory,
        [string]$SkillName,
        [string]$Origin
    )

    $targetDirectory = Join-Path $targetSkillRoot $SkillName
    Remove-TargetDirectoryIfPresent -TargetDirectory $targetDirectory

    Copy-Item -LiteralPath $SourceDirectory -Destination $targetSkillRoot -Recurse -Force
    $installedSkills.Add([pscustomobject]@{
        Skill  = $SkillName
        Origin = $Origin
        Target = $targetDirectory
    }) | Out-Null
}

function Ensure-Git {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        throw "git is required to install public sibling skills."
    }
}

function Sync-GitRepository {
    param(
        [string]$RepositoryName,
        [string]$RepositoryUrl,
        [string]$CacheDirectory
    )

    Ensure-Git

    if (Test-Path (Join-Path $CacheDirectory ".git")) {
        Write-Step "Updating cached $RepositoryName repository"
        git -C $CacheDirectory fetch --depth 1 origin main | Out-Null
        git -C $CacheDirectory reset --hard origin/main | Out-Null
        git -C $CacheDirectory clean -fd | Out-Null
        return
    }

    if (Test-Path $CacheDirectory) {
        Remove-Item -Path $CacheDirectory -Recurse -Force
    }

    $cacheParent = Split-Path -Parent $CacheDirectory
    Ensure-Directory -Path $cacheParent

    Write-Step "Cloning $RepositoryName into cache"
    git clone --depth 1 $RepositoryUrl $CacheDirectory | Out-Null
}

Ensure-Directory -Path $targetSkillRoot

if (-not $SkipCustomSkills) {
    Write-Step "Installing canonical CanDoItAll skills and support resources from SharedInfo"
    $sharedInfoResults = @(& $sharedInfoInstallerPath -CodexHome $CodexHome -Force)
    foreach ($result in $sharedInfoResults) {
        $installedSkills.Add([pscustomobject]@{
            Skill  = $result.Name
            Origin = "CanDoItAll.SharedInfo"
            Target = $result.Target
        }) | Out-Null
    }
}

if (-not $SkipPublicSkills) {
    foreach ($publicSkillSource in $publicSkillSources) {
        Sync-GitRepository `
            -RepositoryName $publicSkillSource.Name `
            -RepositoryUrl $publicSkillSource.RepoUrl `
            -CacheDirectory $publicSkillSource.CacheDirectory

        Write-Step "Installing public sibling skills from $($publicSkillSource.Name)"
        foreach ($skillName in $publicSkillSource.SkillNames) {
            $preferredRelativePath = $null
            if ($publicSkillSource.PSObject.Properties.Name -contains "PreferredPaths" -and
                $publicSkillSource.PreferredPaths.ContainsKey($skillName)) {
                $preferredRelativePath = $publicSkillSource.PreferredPaths[$skillName]
            }

            $sourceDirectory = Find-SkillDirectory `
                -Root $publicSkillSource.CacheDirectory `
                -SkillName $skillName `
                -PreferredRelativePath $preferredRelativePath
            Install-SkillDirectory -SourceDirectory $sourceDirectory -SkillName $skillName -Origin $publicSkillSource.Name
        }
    }
}

Write-Host ""
Write-Host "Installed skills into $targetSkillRoot" -ForegroundColor Green
$installedSkills |
    Sort-Object Skill |
    Format-Table -AutoSize
