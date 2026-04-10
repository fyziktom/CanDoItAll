[CmdletBinding()]
param(
    [string]$RepoRoot = "",
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

    Write-Host "[CanDoItAll Processes MCP] $Message"
}

function Remove-DirectoryRobust {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    for ($attempt = 1; $attempt -le 6; $attempt++) {
        try {
            Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
            return
        }
        catch {
            if ($attempt -eq 6) {
                throw
            }

            Start-Sleep -Milliseconds (250 * $attempt)
        }
    }
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

function Stop-MatchingProcesses {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Needles
    )

    $processes = Get-CimInstance Win32_Process
    foreach ($process in $processes) {
        $commandLine = [string]$process.CommandLine
        if ([string]::IsNullOrWhiteSpace($commandLine)) {
            continue
        }

        $matched = $false
        foreach ($needle in $Needles) {
            if ($commandLine.IndexOf($needle, [System.StringComparison]::OrdinalIgnoreCase) -ge 0) {
                $matched = $true
                break
            }
        }

        if (-not $matched) {
            continue
        }

        try {
            Write-Status "Stopping process $($process.ProcessId) | $($process.Name)"
            Stop-Process -Id $process.ProcessId -Force -ErrorAction Stop
        }
        catch {
            Write-Status "Failed to stop process $($process.ProcessId): $($_.Exception.Message)"
        }
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

function Ensure-ProcessesSettings {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (Test-Path -LiteralPath $Path) {
        return
    }

    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $Path) | Out-Null
    $settings = @"
{
  "Server": {
    "Name": "CanDoItAll.Mcp.Processes",
    "RepositoryRoot": ".",
    "EnsureCurrentProfileReadyOnStartup": true
  }
}
"@

    Set-Content -LiteralPath $Path -Value $settings
    Write-Status "Seeded default settings at $Path"
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

    $config = @{}
    if (Test-Path -LiteralPath $Path) {
        try {
            $config = ConvertTo-Hashtable -InputObject (Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json)
        }
        catch {
            Write-Warning "VS Code MCP config at '$Path' is not valid JSON. Rebuilding it."
            $config = @{}
        }
    }

    if (-not $config.ContainsKey("servers")) {
        $config["servers"] = @{}
    }

    $config["servers"]["candoitall_processes"] = @{
        type = "stdio"
        command = "$WorkspaceFolderToken\$CommandRelativePath"
        args = @(
            "--settings",
            "$WorkspaceFolderToken\CanDoItAll.Mcp.Processes.settings.json"
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
[mcp_servers.candoitall_processes]
command = "$escapedEntrypoint"
cwd = "$escapedRepoRoot"
args = [
  "--settings",
  "$escapedRepoRoot\\CanDoItAll.Mcp.Processes.settings.json"
]
startup_timeout_sec = 45
tool_timeout_sec = 1800
enabled = true
"@

    Set-TomlSection -Path $Path -SectionName "mcp_servers.candoitall_processes" -SectionContent $section
}

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = Resolve-AbsolutePath (Join-Path $PSScriptRoot "..")
}

if ([string]::IsNullOrWhiteSpace($UserConfigPath)) {
    $UserConfigPath = Join-Path $env:USERPROFILE ".codex\config.toml"
}

$RepoRoot = Resolve-AbsolutePath $RepoRoot
$UserConfigPath = Resolve-AbsolutePath $UserConfigPath
$projectPath = Resolve-AbsolutePath (Join-Path $RepoRoot "src\CanDoItAll.Mcp.Processes\CanDoItAll.Mcp.Processes.csproj")
$installRoot = Resolve-AbsolutePath (Join-Path $RepoRoot ".artifacts\mcp-installs\CanDoItAll.Mcp.Processes\current")
$vscodeConfigPath = Resolve-AbsolutePath (Join-Path $RepoRoot ".vscode\mcp.json")

if ([string]::IsNullOrWhiteSpace($SettingsPath)) {
    $SettingsPath = Join-Path $RepoRoot "CanDoItAll.Mcp.Processes.settings.json"
}

$SettingsPath = Resolve-AbsolutePath $SettingsPath
Ensure-ProcessesSettings -Path $SettingsPath

Write-Status "Stopping currently running process-MCP instances before publish"
Stop-MatchingProcesses -Needles @(
    "CanDoItAll.Mcp.Processes.exe",
    "CanDoItAll.Mcp.Processes.dll"
)

Write-Status "Publishing CanDoItAll.Mcp.Processes to $installRoot"
Remove-DirectoryRobust -Path $installRoot
New-Item -ItemType Directory -Force -Path (Split-Path -Parent $installRoot) | Out-Null
Invoke-CheckedCommand -FilePath "dotnet" -Arguments @(
    "publish",
    $projectPath,
    "-c",
    $Configuration,
    "-o",
    $installRoot,
    "-p:UseAppHost=true"
) -WorkingDirectory $RepoRoot

$entrypoint = Get-PreferredEntrypoint -DirectoryPath $installRoot -AssemblyName "CanDoItAll.Mcp.Processes"
$workspaceRelativeEntrypoint = Get-RelativePathPortable -BasePath $RepoRoot -TargetPath $entrypoint

if (-not $SkipVsCodeConfig.IsPresent) {
    Update-VsCodeConfig -Path $vscodeConfigPath -WorkspaceFolderToken '${workspaceFolder}' -CommandRelativePath $workspaceRelativeEntrypoint
    Write-Status "Updated VS Code MCP config at $vscodeConfigPath"
}

if (-not $SkipUserConfig.IsPresent) {
    Update-CodexConfig -Path $UserConfigPath -EntrypointPath $entrypoint
    Write-Status "Updated Codex config at $UserConfigPath"
}

Write-Status "Processes MCP install completed."
Write-Status "Entrypoint: $entrypoint"
Write-Status "Settings: $SettingsPath"
