[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$ServerBaseUrl,
    [Parameter(Mandatory = $true)]
    [string]$AgentToken,
    [string]$RepoRoot = "",
    [string]$AgentName = "CanDoItAll Project Structure Agent",
    [string]$BranchName = "",
    [string]$Configuration = "Release",
    [string]$SettingsPath = "",
    [string]$UserConfigPath = "",
    [switch]$SkipUserConfig,
    [switch]$SkipVsCodeConfig
)

$ErrorActionPreference = "Stop"

function Resolve-AbsolutePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue
    )

    return [System.IO.Path]::GetFullPath($PathValue)
}

function Get-RelativePathPortable {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BasePath,
        [Parameter(Mandatory = $true)]
        [string]$TargetPath
    )

    $normalizedBasePath = Resolve-AbsolutePath $BasePath
    $normalizedTargetPath = Resolve-AbsolutePath $TargetPath
    $baseUri = [System.Uri]($normalizedBasePath.TrimEnd('\') + '\')
    $targetUri = [System.Uri]$normalizedTargetPath
    return [System.Uri]::UnescapeDataString($baseUri.MakeRelativeUri($targetUri).ToString()).Replace('/', '\')
}

function Write-Status {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    Write-Host "[CanDoItAll ProjectStructure MCP] $Message"
}

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [string]$WorkingDirectory = ""
    )

    if ([string]::IsNullOrWhiteSpace($WorkingDirectory)) {
        & $FilePath @Arguments
    }
    else {
        Push-Location $WorkingDirectory
        try {
            & $FilePath @Arguments
        }
        finally {
            Pop-Location
        }
    }

    if ($LASTEXITCODE -ne 0) {
        throw "Command failed with exit code ${LASTEXITCODE}: $FilePath $($Arguments -join ' ')"
    }
}

function Get-PreferredEntrypoint {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DirectoryPath,
        [Parameter(Mandatory = $true)]
        [string]$AssemblyName
    )

    $exePath = Join-Path $DirectoryPath "$AssemblyName.exe"
    if (Test-Path -LiteralPath $exePath) {
        return $exePath
    }

    $dllPath = Join-Path $DirectoryPath "$AssemblyName.dll"
    if (Test-Path -LiteralPath $dllPath) {
        return $dllPath
    }

    throw "Could not locate an entrypoint for '$AssemblyName' under '$DirectoryPath'."
}

function New-VersionedInstallPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$InstallBasePath
    )

    $versionStamp = [DateTimeOffset]::UtcNow.ToString("yyyyMMdd-HHmmss")
    $candidate = Join-Path $InstallBasePath $versionStamp
    $suffix = 0

    while (Test-Path -LiteralPath $candidate) {
        $suffix++
        $candidate = Join-Path $InstallBasePath "$versionStamp-$suffix"
    }

    return $candidate
}

function Set-TomlSection {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$SectionName,
        [Parameter(Mandatory = $true)]
        [string]$SectionContent
    )

    $text = if (Test-Path -LiteralPath $Path) { Get-Content -LiteralPath $Path -Raw } else { "" }
    $normalizedSection = $SectionContent.TrimEnd() + "`r`n`r`n"
    $pattern = "(?ms)^\[$([regex]::Escape($SectionName))\]\r?\n.*?(?=^\[|\z)"

    if ([regex]::IsMatch($text, $pattern)) {
        $text = [regex]::Replace($text, $pattern, $normalizedSection, 1)
    }
    else {
        if (-not [string]::IsNullOrEmpty($text) -and -not $text.EndsWith("`r`n")) {
            $text += "`r`n"
        }

        if (-not [string]::IsNullOrEmpty($text)) {
            $text += "`r`n"
        }

        $text += $normalizedSection
    }

    Set-Content -LiteralPath $Path -Value $text
}

function ConvertTo-Hashtable {
    param(
        [Parameter(Mandatory = $true)]
        [object]$InputObject
    )

    if ($InputObject -is [System.Collections.IDictionary]) {
        $dictionary = @{}
        foreach ($key in $InputObject.Keys) {
            $dictionary[$key] = ConvertTo-Hashtable -InputObject $InputObject[$key]
        }

        return $dictionary
    }

    if ($InputObject -is [System.Collections.IEnumerable] -and -not ($InputObject -is [string])) {
        $items = @()
        foreach ($item in $InputObject) {
            $items += ,(ConvertTo-Hashtable -InputObject $item)
        }

        return $items
    }

    if ($InputObject -is [psobject]) {
        $properties = $InputObject.PSObject.Properties
        if ($properties.Count -gt 0) {
            $dictionary = @{}
            foreach ($property in $properties) {
                $dictionary[$property.Name] = ConvertTo-Hashtable -InputObject $property.Value
            }

            return $dictionary
        }
    }

    return $InputObject
}

function Update-VsCodeConfig {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$WorkspaceFolderToken,
        [Parameter(Mandatory = $true)]
        [string]$CommandRelativePath
    )

    $config = if (Test-Path -LiteralPath $Path) {
        ConvertTo-Hashtable -InputObject (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json)
    }
    else {
        @{}
    }

    if (-not $config.ContainsKey("servers")) {
        $config["servers"] = @{}
    }

    $config["servers"]["candoitall_projectstructure"] = @{
        type = "stdio"
        command = "$WorkspaceFolderToken\$CommandRelativePath"
        args = @(
            "--settings",
            "$WorkspaceFolderToken\CanDoItAll.Mcp.ProjectStructure.settings.local.json"
        )
        cwd = $WorkspaceFolderToken
    }

    Set-Content -LiteralPath $Path -Value ($config | ConvertTo-Json -Depth 10)
}

function Update-CodexConfig {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$EntrypointPath
    )

    $escapedRepoRoot = $RepoRoot.Replace("\", "\\")
    $escapedEntrypoint = $EntrypointPath.Replace("\", "\\")
    $section = @"
[mcp_servers.candoitall_projectstructure]
command = "$escapedEntrypoint"
cwd = "$escapedRepoRoot"
args = [
  "--settings",
  "$escapedRepoRoot\\CanDoItAll.Mcp.ProjectStructure.settings.local.json"
]
startup_timeout_sec = 45
tool_timeout_sec = 1800
enabled = true
"@

    Set-TomlSection -Path $Path -SectionName "mcp_servers.candoitall_projectstructure" -SectionContent $section
}

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = Resolve-AbsolutePath (Join-Path $PSScriptRoot "..")
}

if ([string]::IsNullOrWhiteSpace($UserConfigPath)) {
    $UserConfigPath = Join-Path $env:USERPROFILE ".codex\config.toml"
}

$RepoRoot = Resolve-AbsolutePath $RepoRoot
$UserConfigPath = Resolve-AbsolutePath $UserConfigPath
$projectPath = Resolve-AbsolutePath (Join-Path $RepoRoot "src\CanDoItAll.Mcp.ProjectStructure\CanDoItAll.Mcp.ProjectStructure.csproj")
$installBaseRoot = Resolve-AbsolutePath (Join-Path $RepoRoot ".artifacts\mcp-installs\CanDoItAll.Mcp.ProjectStructure")
$installRoot = New-VersionedInstallPath -InstallBasePath $installBaseRoot
$vscodeConfigPath = Resolve-AbsolutePath (Join-Path $RepoRoot ".vscode\mcp.json")

if ([string]::IsNullOrWhiteSpace($SettingsPath)) {
    $SettingsPath = Join-Path $RepoRoot "CanDoItAll.Mcp.ProjectStructure.settings.local.json"
}

$SettingsPath = Resolve-AbsolutePath $SettingsPath
$normalizedBaseUrl = $ServerBaseUrl.Trim().TrimEnd('/')

New-Item -ItemType Directory -Force -Path $installBaseRoot | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $SettingsPath) | Out-Null
Write-Status "Publishing CanDoItAll.Mcp.ProjectStructure to $installRoot"
Invoke-CheckedCommand -FilePath "dotnet" -Arguments @(
    "publish",
    $projectPath,
    "-c",
    $Configuration,
    "-o",
    $installRoot,
    "-p:UseAppHost=true"
) -WorkingDirectory $RepoRoot

$settings = @{
    Server = @{
        Name = "CanDoItAll.Mcp.ProjectStructure"
        BaseUrl = $normalizedBaseUrl
        AgentToken = $AgentToken.Trim()
        AgentName = $AgentName.Trim()
        RepositoryRoot = "."
        BranchName = $BranchName.Trim()
        TimeoutSeconds = 30
    }
} | ConvertTo-Json -Depth 5

Set-Content -LiteralPath $SettingsPath -Value $settings
Write-Status "Wrote settings to $SettingsPath"

$entrypoint = Get-PreferredEntrypoint -DirectoryPath $installRoot -AssemblyName "CanDoItAll.Mcp.ProjectStructure"
$workspaceRelativeEntrypoint = Get-RelativePathPortable -BasePath $RepoRoot -TargetPath $entrypoint

if (-not $SkipVsCodeConfig.IsPresent) {
    Update-VsCodeConfig -Path $vscodeConfigPath -WorkspaceFolderToken '${workspaceFolder}' -CommandRelativePath $workspaceRelativeEntrypoint
    Write-Status "Updated VS Code MCP config at $vscodeConfigPath"
}

if (-not $SkipUserConfig.IsPresent) {
    Update-CodexConfig -Path $UserConfigPath -EntrypointPath $entrypoint
    Write-Status "Updated Codex config at $UserConfigPath"
}

Write-Status "ProjectStructure MCP install completed."
Write-Status "Entrypoint: $entrypoint"
