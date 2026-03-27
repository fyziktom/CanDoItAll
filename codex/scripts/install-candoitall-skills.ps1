[CmdletBinding()]
param(
    [string]$CodexHome = $(if ($env:CODEX_HOME) { $env:CODEX_HOME } else { Join-Path $HOME ".codex" }),
    [string]$DotNetSkillsRepoUrl = "https://github.com/dotnet/skills.git",
    [string]$DotNetSkillsCache = $(Join-Path $env:TEMP "codex-dotnet-skills-cache"),
    [switch]$SkipCustomSkills,
    [switch]$SkipPublicSkills
)

$ErrorActionPreference = "Stop"

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$repoSkillRoot = Join-Path $repoRoot "codex\skills"
$targetSkillRoot = Join-Path $CodexHome "skills"

$customSkillNames = @(
    "candoitall-bundle-workflow",
    "candoitall-bundle-preparation",
    "candoitall-bundle-execution",
    "candoitall-watch-playwright-loop",
    "candoitall-dotnetwatch-setup"
)

$publicSkillNames = @(
    "frontend-skill",
    "playwright",
    "screenshot",
    "imagegen",
    "mtp-hot-reload"
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
        [string]$SkillName
    )

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

function Install-SkillDirectory {
    param(
        [string]$SourceDirectory,
        [string]$SkillName,
        [string]$Origin
    )

    $targetDirectory = Join-Path $targetSkillRoot $SkillName
    if (Test-Path $targetDirectory) {
        Remove-Item -Path $targetDirectory -Recurse -Force
    }

    Copy-Item -Path $SourceDirectory -Destination $targetSkillRoot -Recurse -Force
    $installedSkills.Add([pscustomobject]@{
        Skill  = $SkillName
        Origin = $Origin
        Target = $targetDirectory
    }) | Out-Null
}

function Ensure-Git {
    if (-not (Get-Command git -ErrorAction SilentlyContinue)) {
        throw "git is required to install public sibling skills from dotnet/skills."
    }
}

function Sync-DotNetSkillsRepository {
    param([string]$CacheDirectory)

    Ensure-Git

    if (Test-Path (Join-Path $CacheDirectory ".git")) {
        Write-Step "Updating cached dotnet/skills repository"
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

    Write-Step "Cloning dotnet/skills into cache"
    git clone --depth 1 $DotNetSkillsRepoUrl $CacheDirectory | Out-Null
}

Ensure-Directory -Path $targetSkillRoot

if (-not $SkipCustomSkills) {
    Write-Step "Installing custom CanDoItAll skills from repo"
    foreach ($skillName in $customSkillNames) {
        $sourceDirectory = Find-SkillDirectory -Root $repoSkillRoot -SkillName $skillName
        Install-SkillDirectory -SourceDirectory $sourceDirectory -SkillName $skillName -Origin "repo"
    }
}

if (-not $SkipPublicSkills) {
    Sync-DotNetSkillsRepository -CacheDirectory $DotNetSkillsCache

    Write-Step "Installing public sibling skills from dotnet/skills"
    foreach ($skillName in $publicSkillNames) {
        $sourceDirectory = Find-SkillDirectory -Root $DotNetSkillsCache -SkillName $skillName
        Install-SkillDirectory -SourceDirectory $sourceDirectory -SkillName $skillName -Origin "dotnet/skills"
    }
}

Write-Host ""
Write-Host "Installed skills into $targetSkillRoot" -ForegroundColor Green
$installedSkills |
    Sort-Object Skill |
    Format-Table -AutoSize
