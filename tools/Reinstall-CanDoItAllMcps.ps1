[CmdletBinding()]
param(
    [string]$RepoRoot = "",
    [string]$UserConfigPath = "",
    [switch]$SkipUserConfig,
    [switch]$SkipVsCodeConfig,
    [switch]$SkipProcessReset
)

$ErrorActionPreference = "Stop"

function Resolve-AbsolutePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue
    )

    return [System.IO.Path]::GetFullPath($PathValue)
}

function Write-Status {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    Write-Host "[CanDoItAll MCP Resetup] $Message"
}

function Remove-DirectoryRobust {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    for ($attempt = 1; $attempt -le 5; $attempt++) {
        try {
            Remove-Item -LiteralPath $Path -Recurse -Force -ErrorAction Stop
            return
        }
        catch {
            if ($attempt -eq 5) {
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

function Publish-ReleaseArtifact {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ProjectPath,
        [Parameter(Mandatory = $true)]
        [string]$OutputPath
    )

    $parentDirectory = Split-Path -Parent $OutputPath
    New-Item -ItemType Directory -Force -Path $parentDirectory | Out-Null
    Remove-DirectoryRobust -Path $OutputPath

    Write-Status "Publishing $(Split-Path -Leaf $ProjectPath) to $OutputPath"
    Invoke-CheckedCommand -FilePath "dotnet" -Arguments @(
        "publish",
        $ProjectPath,
        "-c",
        "Release",
        "-o",
        $OutputPath,
        "-p:UseAppHost=true"
    ) -WorkingDirectory $RepoRoot
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

    $text = Get-Content -LiteralPath $Path -Raw
    $normalizedSection = $SectionContent.TrimEnd() + "`r`n`r`n"
    $pattern = "(?ms)^\[$([regex]::Escape($SectionName))\]\r?\n.*?(?=^\[|\z)"

    if ([regex]::IsMatch($text, $pattern)) {
        $text = [regex]::Replace($text, $pattern, $normalizedSection, 1)
    }
    else {
        if (-not $text.EndsWith("`r`n")) {
            $text += "`r`n"
        }

        $text += "`r`n" + $normalizedSection
    }

    Set-Content -LiteralPath $Path -Value $text
}

function Update-VsCodeMcpConfig {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$WorkspaceFolderToken
    )

    $json = @"
{
  "servers": {
    "candoitall_dotnetwatch": {
      "type": "stdio",
      "command": "powershell",
      "args": [
        "-NoProfile",
        "-ExecutionPolicy",
        "Bypass",
        "-File",
        "$WorkspaceFolderToken\\tools\\CanDoItAll.Mcp.DotNetWatch\\Start-CanDoItAllDotNetWatchMcp.ps1",
        "-Configuration",
        "Release",
        "-SettingsPath",
        "$WorkspaceFolderToken\\CanDoItAll.Mcp.DotNetWatch.settings.json"
      ],
      "cwd": "$WorkspaceFolderToken"
    },
    "candoitall_sshops": {
      "type": "stdio",
      "command": "$WorkspaceFolderToken\\.artifacts\\mcp-installs\\CanDoItAll.Mcp.SshOps\\current\\CanDoItAll.Mcp.SshOps.exe",
      "args": [
        "--settings",
        "$WorkspaceFolderToken\\CanDoItAll.Mcp.SshOps.settings.json"
      ],
      "cwd": "$WorkspaceFolderToken"
    },
    "playwright": {
      "type": "stdio",
      "command": "npx",
      "args": [
        "@playwright/mcp@latest"
      ]
    },
    "tailwindcss": {
      "type": "stdio",
      "command": "npx",
      "args": [
        "tailwindcss-mcp@latest"
      ]
    }
  }
}
"@

    Set-Content -LiteralPath $Path -Value $json
}

function Update-CodexConfig {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        Write-Status "User config not found at $Path. Skipping Codex config update."
        return
    }

    $escapedRepoRoot = $RepoRoot.Replace("\", "\\")

    $dotNetWatchSection = @"
[mcp_servers.candoitall_dotnetwatch]
command = "powershell"
cwd = "$escapedRepoRoot"
args = [
  "-NoProfile",
  "-ExecutionPolicy",
  "Bypass",
  "-File",
  "$escapedRepoRoot\\tools\\CanDoItAll.Mcp.DotNetWatch\\Start-CanDoItAllDotNetWatchMcp.ps1",
  "-Configuration",
  "Release",
  "-SettingsPath",
  "$escapedRepoRoot\\CanDoItAll.Mcp.DotNetWatch.settings.json"
]
startup_timeout_sec = 120
tool_timeout_sec = 1800
enabled = true
"@

    $sshOpsSection = @"
[mcp_servers.candoitall_sshops]
command = "$escapedRepoRoot\\.artifacts\\mcp-installs\\CanDoItAll.Mcp.SshOps\\current\\CanDoItAll.Mcp.SshOps.exe"
cwd = "$escapedRepoRoot"
args = [
  "--settings",
  "$escapedRepoRoot\\CanDoItAll.Mcp.SshOps.settings.json"
]
startup_timeout_sec = 45
tool_timeout_sec = 1800
enabled = true
"@

    Set-TomlSection -Path $Path -SectionName "mcp_servers.candoitall_dotnetwatch" -SectionContent $dotNetWatchSection
    Set-TomlSection -Path $Path -SectionName "mcp_servers.candoitall_sshops" -SectionContent $sshOpsSection
}

if ([string]::IsNullOrWhiteSpace($RepoRoot)) {
    $RepoRoot = Resolve-AbsolutePath (Join-Path $PSScriptRoot "..")
}

if ([string]::IsNullOrWhiteSpace($UserConfigPath)) {
    $UserConfigPath = Join-Path $env:USERPROFILE ".codex\config.toml"
}

$RepoRoot = Resolve-AbsolutePath $RepoRoot
$UserConfigPath = Resolve-AbsolutePath $UserConfigPath

$dotNetWatchWrapperPath = Resolve-AbsolutePath (Join-Path $RepoRoot "tools\CanDoItAll.Mcp.DotNetWatch\Start-CanDoItAllDotNetWatchMcp.ps1")
$dotNetWatchSettingsPath = Resolve-AbsolutePath (Join-Path $RepoRoot "CanDoItAll.Mcp.DotNetWatch.settings.json")
$sshOpsProjectPath = Resolve-AbsolutePath (Join-Path $RepoRoot "src\CanDoItAll.Mcp.SshOps\CanDoItAll.Mcp.SshOps.csproj")
$sshOpsSettingsPath = Resolve-AbsolutePath (Join-Path $RepoRoot "CanDoItAll.Mcp.SshOps.settings.json")
$managerProjectPath = Resolve-AbsolutePath (Join-Path $RepoRoot "tools\CanDoItAll.Manager\CanDoItAll.Manager.csproj")
$installRoot = Resolve-AbsolutePath (Join-Path $RepoRoot ".artifacts\mcp-installs")
$sshOpsInstallRoot = Resolve-AbsolutePath (Join-Path $installRoot "CanDoItAll.Mcp.SshOps\current")
$managerInstallRoot = Resolve-AbsolutePath (Join-Path $installRoot "CanDoItAll.Manager\current")
$manifestPath = Resolve-AbsolutePath (Join-Path $installRoot "install-manifest.json")
$shadowManifestPath = Resolve-AbsolutePath (Join-Path $RepoRoot ".artifacts\mcp-server-shadow\current.json")
$vscodeMcpPath = Resolve-AbsolutePath (Join-Path $RepoRoot ".vscode\mcp.json")

New-Item -ItemType Directory -Force -Path $installRoot | Out-Null

if (-not $SkipProcessReset.IsPresent) {
    Write-Status "Stopping currently running MCP and manager processes"
    Stop-MatchingProcesses -Needles @(
        "CanDoItAll.Mcp.DotNetWatch.dll",
        "Start-CanDoItAllDotNetWatchMcp.ps1",
        ".artifacts\mcp-server-shadow",
        "CanDoItAll.Mcp.SshOps.exe",
        "CanDoItAll.Mcp.SshOps.dll",
        "CanDoItAll.Manager.exe",
        "CanDoItAll.Manager.dll"
    )
}

Write-Status "Preparing release shadow artifact for CanDoItAll.Mcp.DotNetWatch"
Invoke-CheckedCommand -FilePath "powershell" -Arguments @(
    "-NoProfile",
    "-ExecutionPolicy",
    "Bypass",
    "-File",
    $dotNetWatchWrapperPath,
    "-Configuration",
    "Release",
    "-SettingsPath",
    $dotNetWatchSettingsPath,
    "-ForceRebuild",
    "-PrepareOnly"
) -WorkingDirectory $RepoRoot

if (-not (Test-Path -LiteralPath $shadowManifestPath)) {
    throw "DotNetWatch shadow manifest was not created at '$shadowManifestPath'."
}

Publish-ReleaseArtifact -ProjectPath $sshOpsProjectPath -OutputPath $sshOpsInstallRoot
Publish-ReleaseArtifact -ProjectPath $managerProjectPath -OutputPath $managerInstallRoot

$shadowManifest = Get-Content -LiteralPath $shadowManifestPath -Raw | ConvertFrom-Json
$sshOpsEntrypoint = Get-PreferredEntrypoint -DirectoryPath $sshOpsInstallRoot -AssemblyName "CanDoItAll.Mcp.SshOps"
$managerEntrypoint = Get-PreferredEntrypoint -DirectoryPath $managerInstallRoot -AssemblyName "CanDoItAll.Manager"

$installManifest = @{
    updatedUtc = [DateTimeOffset]::UtcNow.ToString("O")
    dotNetWatch = @{
        mode = "wrapper-shadow"
        configuration = "Release"
        wrapperPath = $dotNetWatchWrapperPath
        settingsPath = $dotNetWatchSettingsPath
        shadowManifestPath = $shadowManifestPath
        shadowDllPath = $shadowManifest.shadowDllPath
    }
    sshOps = @{
        configuration = "Release"
        settingsPath = $sshOpsSettingsPath
        installRoot = $sshOpsInstallRoot
        entrypointPath = $sshOpsEntrypoint
    }
    manager = @{
        configuration = "Release"
        installRoot = $managerInstallRoot
        entrypointPath = $managerEntrypoint
    }
} | ConvertTo-Json -Depth 10

Set-Content -LiteralPath $manifestPath -Value $installManifest
Write-Status "Wrote install manifest to $manifestPath"

if (-not $SkipVsCodeConfig.IsPresent) {
    Write-Status "Updating VS Code MCP config"
    Update-VsCodeMcpConfig -Path $vscodeMcpPath -WorkspaceFolderToken '${workspaceFolder}'
}

if (-not $SkipUserConfig.IsPresent) {
    Write-Status "Updating Codex config"
    Update-CodexConfig -Path $UserConfigPath
}

Write-Status "Resetup completed."
Write-Status "DotNetWatch shadow DLL: $($shadowManifest.shadowDllPath)"
Write-Status "SshOps entrypoint: $sshOpsEntrypoint"
Write-Status "Manager entrypoint: $managerEntrypoint"
