[CmdletBinding()]
param(
    [string]$RepositoryPath = ""
)

$ErrorActionPreference = "Stop"

function Resolve-RepositoryPath {
    param(
        [string]$RequestedPath
    )

    if (-not [string]::IsNullOrWhiteSpace($RequestedPath)) {
        return (Resolve-Path -LiteralPath $RequestedPath).Path
    }

    return (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot "..\..")).Path
}

function Test-MaintainedMarkdownPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RelativePath
    )

    $normalized = $RelativePath.Replace("\", "/")
    if ($normalized -eq "Templates/README.md" -or
        $normalized -eq "Templates/Processes/README.md") {
        return $true
    }

    if ($normalized.StartsWith(".codex/", [System.StringComparison]::OrdinalIgnoreCase) -or
        $normalized.StartsWith("codex/", [System.StringComparison]::OrdinalIgnoreCase) -or
        $normalized.StartsWith("Templates/", [System.StringComparison]::OrdinalIgnoreCase)) {
        return $false
    }

    return $normalized -eq "AGENTS.md" -or
        $normalized -eq "README.md" -or
        $normalized -eq "CONTRIBUTING.md" -or
        $normalized -eq "SECURITY.md" -or
        $normalized -eq ".github/copilot-instructions.md" -or
        $normalized.StartsWith("docs/", [System.StringComparison]::OrdinalIgnoreCase) -or
        $normalized.StartsWith("src/", [System.StringComparison]::OrdinalIgnoreCase) -or
        $normalized.StartsWith("tests/", [System.StringComparison]::OrdinalIgnoreCase) -or
        $normalized.StartsWith("tools/", [System.StringComparison]::OrdinalIgnoreCase) -or
        $normalized.StartsWith("Tailwind/", [System.StringComparison]::OrdinalIgnoreCase)
}

function Get-LinkTargetPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$RawTarget
    )

    $target = $RawTarget.Trim()
    if ($target.StartsWith("<") -and $target.EndsWith(">")) {
        $target = $target.Substring(1, $target.Length - 2)
    }

    if ($target -match "^(?<path>\S+)\s+['`"]") {
        $target = $Matches.path
    }

    $fragmentIndex = $target.IndexOf("#", [System.StringComparison]::Ordinal)
    if ($fragmentIndex -ge 0) {
        $target = $target.Substring(0, $fragmentIndex)
    }

    return [System.Uri]::UnescapeDataString($target)
}

$repositoryRoot = Resolve-RepositoryPath -RequestedPath $RepositoryPath
$requiredFiles = @(
    ".editorconfig",
    ".gitattributes",
    ".gitignore",
    ".env.example",
    ".github\workflows\ci.yml",
    "AGENTS.md",
    "README.md",
    "LICENSE",
    "CONTRIBUTING.md",
    "SECURITY.md",
    "compose.yaml",
    "docs\README.md",
    "docs\architecture\README.md",
    "docs\architecture\overview.md",
    "docs\architecture\internal-communication.md",
    "docs\architecture\modules.md",
    "src\README.md",
    "src\App\README.md",
    "src\Foundation\README.md",
    "src\Integration\README.md",
    "src\MAF\README.md",
    "src\Memory\README.md",
    "src\Modules\README.md",
    "src\plugins\README.md",
    "src\Processes\README.md",
    "src\UI\README.md",
    "tests\README.md",
    "tools\README.md"
)
$errors = [System.Collections.Generic.List[string]]::new()

foreach ($requiredFile in $requiredFiles) {
    $requiredPath = Join-Path $repositoryRoot $requiredFile
    if (-not (Test-Path -LiteralPath $requiredPath -PathType Leaf)) {
        $errors.Add("Missing required publication file: $requiredFile")
    }
}

$trackedMarkdown = @(
    & git -C $repositoryRoot ls-files --cached --others --exclude-standard -- "*.md"
)
if ($LASTEXITCODE -ne 0) {
    throw "Unable to enumerate tracked Markdown files."
}

$maintainedMarkdown = @(
    $trackedMarkdown |
        Where-Object {
            (Test-MaintainedMarkdownPath -RelativePath $_) -and
            (Test-Path -LiteralPath (Join-Path $repositoryRoot $_) -PathType Leaf)
        }
)
$markdownLinkPattern = [regex]'!?\[[^\]]*\]\((?<target>[^)\r\n]+)\)'
$inlinePathPattern = [regex]'`(?<path>(?:src|tests|tools|docs|Tailwind|codex)[\\/][^`\r\n]+)`'
$absoluteDeveloperPathPattern = [regex]'(?im)(?:[A-Z]:[\\/](?:repositories|repos|source|users)[\\/]|/home/[^/\s]+/)'
$previewWebsitePattern = [regex]'(?i)https://alpha\.aicandoitall\.com'

foreach ($relativePath in $maintainedMarkdown) {
    $fullPath = Join-Path $repositoryRoot $relativePath
    $content = Get-Content -LiteralPath $fullPath -Raw
    if ($absoluteDeveloperPathPattern.IsMatch($content)) {
        $errors.Add("Developer-specific absolute path found in $relativePath")
    }

    if ($previewWebsitePattern.IsMatch($content)) {
        $errors.Add("Preview website address found in ${relativePath}; use https://aicandoitall.com.")
    }

    foreach ($match in $markdownLinkPattern.Matches($content)) {
        $target = Get-LinkTargetPath -RawTarget $match.Groups["target"].Value
        if ([string]::IsNullOrWhiteSpace($target) -or
            $target.StartsWith("#") -or
            $target -match '^(?:https?|mailto|data|app):') {
            continue
        }

        if ([System.IO.Path]::IsPathRooted($target)) {
            $errors.Add("Absolute local Markdown link in ${relativePath}: $target")
            continue
        }

        $resolvedTarget = [System.IO.Path]::GetFullPath(
            (Join-Path (Split-Path -Parent $fullPath) $target))
        if (-not $resolvedTarget.StartsWith($repositoryRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
            $errors.Add("Markdown link escapes the repository in ${relativePath}: $target")
            continue
        }

        if (-not (Test-Path -LiteralPath $resolvedTarget)) {
            $errors.Add("Broken Markdown link in ${relativePath}: $target")
        }
    }

    foreach ($match in $inlinePathPattern.Matches($content)) {
        $sourcePath = $match.Groups["path"].Value.Trim().TrimEnd(".", ",", ":", ";")
        if ($sourcePath -match '[*?{}$]' -or $sourcePath -match '\s') {
            continue
        }

        $sourcePath = $sourcePath -replace ':\d+$', ''
        $resolvedSourcePath = Join-Path $repositoryRoot $sourcePath
        if (-not (Test-Path -LiteralPath $resolvedSourcePath)) {
            $errors.Add("Missing inline repository path in ${relativePath}: $sourcePath")
        }
    }
}

$projects = @(
    & git -C $repositoryRoot ls-files --cached --others --exclude-standard -- "*.csproj"
)
if ($LASTEXITCODE -ne 0) {
    throw "Unable to enumerate C# projects."
}

foreach ($project in $projects) {
    $projectPath = Join-Path $repositoryRoot $project
    if (-not (Test-Path -LiteralPath $projectPath -PathType Leaf)) {
        continue
    }

    $readmePath = Join-Path (Split-Path -Parent $projectPath) "README.md"
    if (-not (Test-Path -LiteralPath $readmePath -PathType Leaf)) {
        $errors.Add("Project README is missing for $project")
    }
}

$forbiddenTrackedPatterns = @(
    '^(?:\.codex|\.codex-runtime|\.local|docs/images|outputs?)/',
    '^CanDoItAll\.Mcp\..*\.settings\.json$',
    '^\.vscode/mcp\.json$',
    '\.csproj\.user$',
    '(?:^|/)(?:__pycache__|TestResults)/',
    '\.(?:log|pyc|pid)$',
    '^(?:gantt-.*|.*infographic.*)\.(?:png|jpg|jpeg|webp)$'
)
$presentTrackedFiles = @(
    & git -C $repositoryRoot ls-files --cached |
        Where-Object {
            Test-Path -LiteralPath (Join-Path $repositoryRoot $_) -PathType Leaf
        }
)
if ($LASTEXITCODE -ne 0) {
    throw "Unable to enumerate tracked files."
}

foreach ($pattern in $forbiddenTrackedPatterns) {
    $matches = @(
        $presentTrackedFiles |
            Where-Object {
                $_.Replace("\", "/") -match $pattern
            }
    )
    if ($matches.Count -gt 0) {
        $errors.Add(
            "Generated or local-only tracked files match '$pattern': $($matches.Count) file(s); first is $($matches[0])")
    }
}

$rootReadmePath = Join-Path $repositoryRoot "README.md"
if (Test-Path -LiteralPath $rootReadmePath -PathType Leaf) {
    $rootReadme = Get-Content -LiteralPath $rootReadmePath -Raw
    if ($rootReadme -notmatch 'actions/workflows/ci\.yml/badge\.svg\?branch=main') {
        $errors.Add("README.md is missing the active main-branch CI badge.")
    }

    if ($rootReadme -notmatch 'img\.shields\.io/badge/license-MIT-blue\.svg') {
        $errors.Add("README.md is missing the canonical MIT license badge.")
    }

    if ($rootReadme -notmatch 'https://aicandoitall\.com') {
        $errors.Add("README.md is missing the final CanDoItAll website address.")
    }
}

$directoryBuildPropsPath = Join-Path $repositoryRoot "Directory.Build.props"
if (Test-Path -LiteralPath $directoryBuildPropsPath -PathType Leaf) {
    $directoryBuildProps = Get-Content -LiteralPath $directoryBuildPropsPath -Raw
    if ($directoryBuildProps -notmatch '<IsPackable>\s*false\s*</IsPackable>') {
        $errors.Add("Directory.Build.props must disable NuGet packaging with IsPackable=false.")
    }
}

$packagingOptIns = @(
    $presentTrackedFiles |
        Where-Object {
            $_ -match '\.(?:csproj|props|targets)$'
        } |
        Where-Object {
            $content = Get-Content -LiteralPath (Join-Path $repositoryRoot $_) -Raw
            $content -match '<IsPackable>\s*true\s*</IsPackable>' -or
                $content -match '<GeneratePackageOnBuild>\s*true\s*</GeneratePackageOnBuild>'
        }
)
if ($packagingOptIns.Count -gt 0) {
    $errors.Add(
        "NuGet packaging is disabled for this repository, but packaging opt-ins exist: $($packagingOptIns -join ', ')")
}

$publishingEntryPoints = @(
    $presentTrackedFiles |
        Where-Object {
            $_ -match '^(?:\.github/workflows|tools)/' -and
                $_ -match '\.(?:ya?ml|ps1|sh|cmd|bat)$'
        } |
        Where-Object {
            (Get-Content -LiteralPath (Join-Path $repositoryRoot $_) -Raw) -match
                '(?im)\bdotnet\s+(?:nuget\s+)?push\b'
        }
)
if ($publishingEntryPoints.Count -gt 0) {
    $errors.Add(
        "NuGet publishing entry points are not allowed yet: $($publishingEntryPoints -join ', ')")
}

$packageJsonPath = Join-Path $repositoryRoot "package.json"
if (Test-Path -LiteralPath $packageJsonPath -PathType Leaf) {
    $packageJson = Get-Content -LiteralPath $packageJsonPath -Raw | ConvertFrom-Json
    if ($packageJson.private -ne $true) {
        $errors.Add("package.json must declare private=true because this repository is not an npm package.")
    }

    if ($packageJson.license -ne "MIT") {
        $errors.Add("package.json must declare the MIT SPDX license expression.")
    }
}

if ($errors.Count -gt 0) {
    $errors |
        Sort-Object -Unique |
        ForEach-Object { Write-Host "ERROR: $_" -ForegroundColor Red }
    throw "Documentation validation failed with $($errors.Count) finding(s)."
}

Write-Host "Documentation validation passed for $($maintainedMarkdown.Count) maintained Markdown files."
