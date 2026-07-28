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
        $normalized.StartsWith("codex/bundles/", [System.StringComparison]::OrdinalIgnoreCase) -or
        $normalized.StartsWith("Templates/", [System.StringComparison]::OrdinalIgnoreCase)) {
        return $false
    }

    return $normalized -eq "README.md" -or
        $normalized -eq "CONTRIBUTING.md" -or
        $normalized -eq "SECURITY.md" -or
        $normalized -eq "codex/README.md" -or
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
    "README.md",
    "LICENSE",
    "CONTRIBUTING.md",
    "SECURITY.md",
    "docs\README.md"
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

foreach ($relativePath in $maintainedMarkdown) {
    $fullPath = Join-Path $repositoryRoot $relativePath
    $content = Get-Content -LiteralPath $fullPath -Raw
    if ($absoluteDeveloperPathPattern.IsMatch($content)) {
        $errors.Add("Developer-specific absolute path found in $relativePath")
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

$packageJsonPath = Join-Path $repositoryRoot "package.json"
if (Test-Path -LiteralPath $packageJsonPath -PathType Leaf) {
    $packageJson = Get-Content -LiteralPath $packageJsonPath -Raw | ConvertFrom-Json
    if ($packageJson.private -ne $true) {
        $errors.Add("package.json must declare private=true because this repository is not an npm package.")
    }

    if ($packageJson.license -ne "SEE LICENSE IN LICENSE") {
        $errors.Add("package.json license metadata does not point to the repository LICENSE.")
    }
}

if ($errors.Count -gt 0) {
    $errors |
        Sort-Object -Unique |
        ForEach-Object { Write-Host "ERROR: $_" -ForegroundColor Red }
    throw "Documentation validation failed with $($errors.Count) finding(s)."
}

Write-Host "Documentation validation passed for $($maintainedMarkdown.Count) maintained Markdown files."
