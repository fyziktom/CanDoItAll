[CmdletBinding()]
param(
    [string]$RepoRoot = "",
    [string]$UserConfigPath = "",
    [string]$ShadowConfiguration = "Release",
    [switch]$SkipUserConfig,
    [switch]$SkipVsCodeConfig,
    [switch]$SkipProcessReset,
    [switch]$SkipSkillSync,
    [switch]$SkipTrayStartupShortcut,
    [switch]$SkipTrayDesktopShortcut
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

function Test-BackendCatalogRecordLive {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$Record
    )

    if ($null -eq $Record.processId -or $null -eq $Record.processStartedUtc) {
        return $false
    }

    try {
        $process = Get-Process -Id ([int]$Record.processId) -ErrorAction Stop
        if ($process.HasExited) {
            return $false
        }

        $startedUtc = $process.StartTime.ToUniversalTime()
        $registeredStart = ([DateTimeOffset]$Record.processStartedUtc).UtcDateTime
        return [Math]::Abs(($startedUtc - $registeredStart).TotalSeconds) -le 60
    }
    catch {
        return $false
    }
}

function Cleanup-WorkspaceBackendCatalog {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CatalogDirectory,
        [Parameter(Mandatory = $true)]
        [string]$WorkspaceRoot,
        [Parameter(Mandatory = $true)]
        [string]$SettingsPath
    )

    if (-not (Test-Path -LiteralPath $CatalogDirectory)) {
        return
    }

    $deletedCount = 0
    $failedCount = 0

    foreach ($file in Get-ChildItem -LiteralPath $CatalogDirectory -Filter *.json -File) {
        try {
            $record = Get-Content -LiteralPath $file.FullName -Raw | ConvertFrom-Json
            if ($null -eq $record -or $null -eq $record.identity) {
                continue
            }

            $recordWorkspace = [System.IO.Path]::GetFullPath([string]$record.identity.workspaceRoot)
            $recordSettingsPath = [System.IO.Path]::GetFullPath([string]$record.identity.settingsPath)
            if (-not [string]::Equals($recordWorkspace, $WorkspaceRoot, [System.StringComparison]::OrdinalIgnoreCase)) {
                continue
            }

            if (-not [string]::Equals($recordSettingsPath, $SettingsPath, [System.StringComparison]::OrdinalIgnoreCase)) {
                continue
            }

            if (Test-BackendCatalogRecordLive -Record $record) {
                continue
            }

            Remove-Item -LiteralPath $file.FullName -Force -ErrorAction Stop
            $deletedCount++
        }
        catch {
            $failedCount++
        }
    }

    if ($deletedCount -gt 0 -or $failedCount -gt 0) {
        Write-Status "Backend catalog cleanup for this workspace removed $deletedCount stale record(s); failed to remove $failedCount."
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
        "-p:UseAppHost=true",
        "-p:CopyRepositoryTemplatesToOutput=false"
    ) -WorkingDirectory $RepoRoot
}

function New-VersionedInstallPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$InstallBasePath
    )

    New-Item -ItemType Directory -Force -Path $InstallBasePath | Out-Null

    $versionStamp = [DateTimeOffset]::UtcNow.ToString("yyyyMMdd-HHmmss")
    $candidate = Join-Path $InstallBasePath $versionStamp
    $suffix = 0

    while (Test-Path -LiteralPath $candidate) {
        $suffix++
        $candidate = Join-Path $InstallBasePath "$versionStamp-$suffix"
    }

    return $candidate
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

function Remove-TomlSection {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$SectionName
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $text = Get-Content -LiteralPath $Path -Raw
    $pattern = "(?ms)^\[$([regex]::Escape($SectionName))\]\r?\n.*?(?=^\[|\z)"
    $updated = [regex]::Replace($text, $pattern, "", 1)
    if ($updated -ne $text) {
        Set-Content -LiteralPath $Path -Value ($updated.TrimEnd() + "`r`n")
    }
}

function Update-VsCodeMcpConfig {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$WorkspaceFolderToken,
        [Parameter(Mandatory = $true)]
        [string]$ComponentsCommandPath,
        [Parameter(Mandatory = $true)]
        [string]$CodeAnalyticsCommandPath
    )

    $escapedComponentsCommandPath = $ComponentsCommandPath.Replace("\", "\\")
    $escapedCodeAnalyticsCommandPath = $CodeAnalyticsCommandPath.Replace("\", "\\")
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
        "-RepoRoot",
        "$WorkspaceFolderToken",
        "-Configuration",
        "$ShadowConfiguration",
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
    "candoitall_components": {
      "type": "stdio",
      "command": "$WorkspaceFolderToken\\$escapedComponentsCommandPath",
      "args": [
        "--settings",
        "$WorkspaceFolderToken\\CanDoItAll.Mcp.Components.settings.json"
      ],
      "cwd": "$WorkspaceFolderToken"
    },
    "candoitall_codeanalytics": {
      "type": "stdio",
      "command": "$WorkspaceFolderToken\\$escapedCodeAnalyticsCommandPath",
      "args": [
        "--settings",
        "$WorkspaceFolderToken\\CanDoItAll.Mcp.CodeAnalytics.settings.json"
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
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$SshOpsEntrypoint,
        [Parameter(Mandatory = $true)]
        [string]$ComponentsEntrypoint,
        [Parameter(Mandatory = $true)]
        [string]$CodeAnalyticsEntrypoint,
        [Parameter(Mandatory = $true)]
        [string]$SharpToolsEntrypoint,
        [Parameter(Mandatory = $true)]
        [string]$SharpToolsWorkingDirectory,
        [Parameter(Mandatory = $true)]
        [string]$SharpToolsLogDirectory
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        Write-Status "User config not found at $Path. Skipping Codex config update."
        return
    }

    $escapedRepoRoot = $RepoRoot.Replace("\", "\\")
    $escapedSshOpsEntrypoint = $SshOpsEntrypoint.Replace("\", "\\")
    $escapedComponentsEntrypoint = $ComponentsEntrypoint.Replace("\", "\\")
    $escapedCodeAnalyticsEntrypoint = $CodeAnalyticsEntrypoint.Replace("\", "\\")
    $escapedSharpToolsEntrypoint = $SharpToolsEntrypoint.Replace("\", "\\")
    $escapedSharpToolsWorkingDirectory = $SharpToolsWorkingDirectory.Replace("\", "\\")
    $escapedSharpToolsLogDirectory = $SharpToolsLogDirectory.Replace("\", "\\")

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
  "-RepoRoot",
  "$escapedRepoRoot",
  "-Configuration",
  "$ShadowConfiguration",
  "-SettingsPath",
  "$escapedRepoRoot\\CanDoItAll.Mcp.DotNetWatch.settings.json"
]
startup_timeout_sec = 120
tool_timeout_sec = 1800
enabled = true
"@

    $sshOpsSection = @"
[mcp_servers.candoitall_sshops]
command = "$escapedSshOpsEntrypoint"
cwd = "$escapedRepoRoot"
args = [
  "--settings",
  "$escapedRepoRoot\\CanDoItAll.Mcp.SshOps.settings.json"
]
startup_timeout_sec = 45
tool_timeout_sec = 1800
enabled = true
"@

    $componentsSection = @"
[mcp_servers.candoitall_components]
command = "$escapedComponentsEntrypoint"
cwd = "$escapedRepoRoot"
args = [
  "--settings",
  "$escapedRepoRoot\\CanDoItAll.Mcp.Components.settings.json"
]
startup_timeout_sec = 45
tool_timeout_sec = 1800
enabled = true
"@

    $codeAnalyticsSection = @"
[mcp_servers.candoitall_codeanalytics]
command = "$escapedCodeAnalyticsEntrypoint"
cwd = "$escapedRepoRoot"
args = [
  "--settings",
  "$escapedRepoRoot\\CanDoItAll.Mcp.CodeAnalytics.settings.json"
]
startup_timeout_sec = 60
tool_timeout_sec = 1800
enabled = true
"@

    $sharpToolsSection = @"
[mcp_servers.sharptools]
# Backup only. Keep disabled unless CanDoItAll CodeAnalytics has a real capability gap
# that cannot be repaired or solved in-session.
command = "$escapedSharpToolsEntrypoint"
cwd = "$escapedSharpToolsWorkingDirectory"
args = [
  "--log-directory",
  "$escapedSharpToolsLogDirectory",
  "--log-level",
  "Information",
  "--disable-git"
]
startup_timeout_sec = 45
tool_timeout_sec = 120
enabled = false
"@

    Set-TomlSection -Path $Path -SectionName "mcp_servers.candoitall_dotnetwatch" -SectionContent $dotNetWatchSection
    Set-TomlSection -Path $Path -SectionName "mcp_servers.candoitall_sshops" -SectionContent $sshOpsSection
    Set-TomlSection -Path $Path -SectionName "mcp_servers.candoitall_components" -SectionContent $componentsSection
    Set-TomlSection -Path $Path -SectionName "mcp_servers.candoitall_codeanalytics" -SectionContent $codeAnalyticsSection
    Set-TomlSection -Path $Path -SectionName "mcp_servers.sharptools" -SectionContent $sharpToolsSection
    Remove-TomlSection -Path $Path -SectionName "mcp_servers.candoitall_projectstructure"
    Remove-TomlSection -Path $Path -SectionName "mcp_servers.candoitall_processes"
}

function Sync-RepoSkills {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SkillSourceRoot,
        [Parameter(Mandatory = $true)]
        [string]$SkillTargetRoot
    )

    if (-not (Test-Path -LiteralPath $SkillSourceRoot)) {
        Write-Status "Repo skill root '$SkillSourceRoot' does not exist. Skipping skill sync."
        return @()
    }

    New-Item -ItemType Directory -Force -Path $SkillTargetRoot | Out-Null
    $syncedSkillNames = New-Object System.Collections.Generic.List[string]
    $skillDirectories = @(Get-ChildItem -LiteralPath $SkillSourceRoot -Directory -Recurse |
        Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName "SKILL.md") } |
        Sort-Object FullName)
    $seenSkillNames = @{}

    foreach ($directory in $skillDirectories) {
        if ($seenSkillNames.ContainsKey($directory.Name)) {
            throw "Duplicate repo-managed skill folder name '$($directory.Name)' found in '$($directory.FullName)' and '$($seenSkillNames[$directory.Name])'."
        }

        $seenSkillNames[$directory.Name] = $directory.FullName
        $targetPath = Join-Path $SkillTargetRoot $directory.Name
        Remove-DirectoryRobust -Path $targetPath
        Copy-Item -LiteralPath $directory.FullName -Destination $targetPath -Recurse -Force
        [void]$syncedSkillNames.Add($directory.Name)
        Write-Status "Synced Codex skill '$($directory.Name)' to $targetPath"
    }

    return $syncedSkillNames
}

function Set-Shortcut {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ShortcutPath,
        [Parameter(Mandatory = $true)]
        [string]$TargetPath,
        [Parameter(Mandatory = $true)]
        [string]$Arguments,
        [Parameter(Mandatory = $true)]
        [string]$WorkingDirectory
    )

    $shell = New-Object -ComObject WScript.Shell
    try {
        $shortcut = $shell.CreateShortcut($ShortcutPath)
        $shortcut.TargetPath = $TargetPath
        $shortcut.Arguments = $Arguments
        $shortcut.WorkingDirectory = $WorkingDirectory
        $shortcut.IconLocation = $TargetPath
        $shortcut.Save()
    }
    finally {
        if ($null -ne $shortcut) {
            [System.Runtime.InteropServices.Marshal]::ReleaseComObject($shortcut) | Out-Null
        }

        if ($null -ne $shell) {
            [System.Runtime.InteropServices.Marshal]::ReleaseComObject($shell) | Out-Null
        }
    }
}

function Format-ShortcutArguments {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    return ($Arguments | ForEach-Object {
        if ($_ -match '\s') {
            '"' + $_ + '"'
        }
        else {
            $_
        }
    }) -join ' '
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
$componentsProjectPath = Resolve-AbsolutePath (Join-Path $RepoRoot "src\CanDoItAll.Mcp.Components\CanDoItAll.Mcp.Components.csproj")
$componentsSettingsPath = Resolve-AbsolutePath (Join-Path $RepoRoot "CanDoItAll.Mcp.Components.settings.json")
$codeAnalyticsProjectPath = Resolve-AbsolutePath (Join-Path $RepoRoot "src\CanDoItAll.Mcp.CodeAnalytics\CanDoItAll.Mcp.CodeAnalytics.csproj")
$codeAnalyticsSettingsPath = Resolve-AbsolutePath (Join-Path $RepoRoot "CanDoItAll.Mcp.CodeAnalytics.settings.json")
$sshOpsProjectPath = Resolve-AbsolutePath (Join-Path $RepoRoot "src\CanDoItAll.Mcp.SshOps\CanDoItAll.Mcp.SshOps.csproj")
$sshOpsSettingsPath = Resolve-AbsolutePath (Join-Path $RepoRoot "CanDoItAll.Mcp.SshOps.settings.json")
$managerProjectPath = Resolve-AbsolutePath (Join-Path $RepoRoot "tools\CanDoItAll.Manager\CanDoItAll.Manager.csproj")
$trayProjectPath = Resolve-AbsolutePath (Join-Path $RepoRoot "tools\CanDoItAll.Mcp.DotNetWatch.Tray\CanDoItAll.Mcp.DotNetWatch.Tray.csproj")
$repoSkillRoot = Resolve-AbsolutePath (Join-Path $RepoRoot "codex\skills")
$userSkillRoot = Resolve-AbsolutePath (Join-Path $env:USERPROFILE ".codex\skills")
$repoParentRoot = Split-Path -Parent $RepoRoot
$sharpToolsEntrypoint = Resolve-AbsolutePath (Join-Path $repoParentRoot "SharpToolsMCP\SharpTools.StdioServer\bin\Release\net8.0\SharpTools.StdioServer.exe")
$sharpToolsWorkingDirectory = Resolve-AbsolutePath (Join-Path $repoParentRoot "SharpToolsMCP\SharpTools.StdioServer\bin\Release\net8.0")
$sharpToolsLogDirectory = Resolve-AbsolutePath (Join-Path $repoParentRoot "codex-logs\sharptools")
$installRoot = Resolve-AbsolutePath (Join-Path $RepoRoot ".artifacts\mcp-installs")
$componentsInstallRoot = Resolve-AbsolutePath (Join-Path $installRoot "CanDoItAll.Mcp.Components\current")
$codeAnalyticsInstallRoot = Resolve-AbsolutePath (Join-Path $installRoot "CanDoItAll.Mcp.CodeAnalytics\current")
$sshOpsInstallRoot = Resolve-AbsolutePath (Join-Path $installRoot "CanDoItAll.Mcp.SshOps\current")
$managerInstallRoot = Resolve-AbsolutePath (Join-Path $installRoot "CanDoItAll.Manager\current")
$trayInstallRoot = Resolve-AbsolutePath (Join-Path $installRoot "CanDoItAll.Mcp.DotNetWatch.Tray\current")
$manifestPath = Resolve-AbsolutePath (Join-Path $installRoot "install-manifest.json")
$shadowManifestPath = Resolve-AbsolutePath (Join-Path $RepoRoot ".artifacts\mcp-server-shadow\current.json")
$vscodeMcpPath = Resolve-AbsolutePath (Join-Path $RepoRoot ".vscode\mcp.json")
$startupShortcutPath = Resolve-AbsolutePath (Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs\Startup\CanDoItAll DotNetWatch Tray.lnk")
$desktopShortcutPath = Resolve-AbsolutePath (Join-Path ([Environment]::GetFolderPath("DesktopDirectory")) "CanDoItAll DotNetWatch Tray.lnk")
$globalBackendCatalogDirectory = Resolve-AbsolutePath (Join-Path $env:LOCALAPPDATA "CanDoItAll.Mcp.DotNetWatch\backend-catalog")
$installOwnedCompanionProcessNeedles = @(
    "CanDoItAll.Mcp.Components.exe",
    "CanDoItAll.Mcp.Components.dll",
    "CanDoItAll.Mcp.CodeAnalytics.exe",
    "CanDoItAll.Mcp.CodeAnalytics.dll",
    "CanDoItAll.Mcp.SshOps.exe",
    "CanDoItAll.Mcp.SshOps.dll",
    "CanDoItAll.Manager.exe",
    "CanDoItAll.Manager.dll",
    "CanDoItAll.Mcp.DotNetWatch.Tray.exe",
    "CanDoItAll.Mcp.DotNetWatch.Tray.dll"
)

New-Item -ItemType Directory -Force -Path $installRoot | Out-Null

Write-Status "Stopping install-owned companion processes before publish"
Stop-MatchingProcesses -Needles $installOwnedCompanionProcessNeedles

if (-not $SkipProcessReset.IsPresent) {
    Write-Status "Stopping currently running DotNetWatch, manager, and tray processes"
    Stop-MatchingProcesses -Needles @(
        "CanDoItAll.Mcp.DotNetWatch.dll",
        "Start-CanDoItAllDotNetWatchMcp.ps1",
        ".artifacts\mcp-server-shadow",
        $installOwnedCompanionProcessNeedles
    )

    Cleanup-WorkspaceBackendCatalog -CatalogDirectory $globalBackendCatalogDirectory -WorkspaceRoot $RepoRoot -SettingsPath $dotNetWatchSettingsPath
}

Write-Status "Preparing shadow artifact for CanDoItAll.Mcp.DotNetWatch ($ShadowConfiguration)"
Invoke-CheckedCommand -FilePath "powershell" -Arguments @(
    "-NoProfile",
    "-ExecutionPolicy",
    "Bypass",
    "-File",
    $dotNetWatchWrapperPath,
    "-RepoRoot",
    $RepoRoot,
    "-Configuration",
    $ShadowConfiguration,
    "-SettingsPath",
    $dotNetWatchSettingsPath,
    "-ForceRebuild",
    "-PrepareOnly"
) -WorkingDirectory $RepoRoot

if (-not (Test-Path -LiteralPath $shadowManifestPath)) {
    throw "DotNetWatch shadow manifest was not created at '$shadowManifestPath'."
}

Publish-ReleaseArtifact -ProjectPath $componentsProjectPath -OutputPath $componentsInstallRoot
Publish-ReleaseArtifact -ProjectPath $codeAnalyticsProjectPath -OutputPath $codeAnalyticsInstallRoot
Publish-ReleaseArtifact -ProjectPath $sshOpsProjectPath -OutputPath $sshOpsInstallRoot
Publish-ReleaseArtifact -ProjectPath $managerProjectPath -OutputPath $managerInstallRoot
Publish-ReleaseArtifact -ProjectPath $trayProjectPath -OutputPath $trayInstallRoot

$shadowManifest = Get-Content -LiteralPath $shadowManifestPath -Raw | ConvertFrom-Json
$componentsEntrypoint = Get-PreferredEntrypoint -DirectoryPath $componentsInstallRoot -AssemblyName "CanDoItAll.Mcp.Components"
$componentsWorkspaceEntrypoint = Get-RelativePathPortable -BasePath $RepoRoot -TargetPath $componentsEntrypoint
$codeAnalyticsEntrypoint = Get-PreferredEntrypoint -DirectoryPath $codeAnalyticsInstallRoot -AssemblyName "CanDoItAll.Mcp.CodeAnalytics"
$codeAnalyticsWorkspaceEntrypoint = Get-RelativePathPortable -BasePath $RepoRoot -TargetPath $codeAnalyticsEntrypoint
$sshOpsEntrypoint = Get-PreferredEntrypoint -DirectoryPath $sshOpsInstallRoot -AssemblyName "CanDoItAll.Mcp.SshOps"
$managerEntrypoint = Get-PreferredEntrypoint -DirectoryPath $managerInstallRoot -AssemblyName "CanDoItAll.Manager"
$trayEntrypoint = Get-PreferredEntrypoint -DirectoryPath $trayInstallRoot -AssemblyName "CanDoItAll.Mcp.DotNetWatch.Tray"

$trayArguments = @(
    "--repo-root", $RepoRoot,
    "--settings-path", $dotNetWatchSettingsPath,
    "--wrapper-path", $dotNetWatchWrapperPath,
    "--shadow-manifest-path", $shadowManifestPath
)

if (-not $SkipTrayStartupShortcut.IsPresent) {
    Write-Status "Updating startup shortcut for tray app"
    Set-Shortcut -ShortcutPath $startupShortcutPath -TargetPath $trayEntrypoint -Arguments (Format-ShortcutArguments -Arguments $trayArguments) -WorkingDirectory $RepoRoot
}
elseif (Test-Path -LiteralPath $startupShortcutPath) {
    Remove-Item -LiteralPath $startupShortcutPath -Force
}

if (-not $SkipTrayDesktopShortcut.IsPresent) {
    Write-Status "Updating desktop shortcut for tray app"
    Set-Shortcut -ShortcutPath $desktopShortcutPath -TargetPath $trayEntrypoint -Arguments (Format-ShortcutArguments -Arguments $trayArguments) -WorkingDirectory $RepoRoot
}
elseif (Test-Path -LiteralPath $desktopShortcutPath) {
    Remove-Item -LiteralPath $desktopShortcutPath -Force
}

$syncedSkills = @()
if (-not $SkipSkillSync.IsPresent) {
    Write-Status "Syncing repo-managed Codex skills"
    $syncedSkills = @(Sync-RepoSkills -SkillSourceRoot $repoSkillRoot -SkillTargetRoot $userSkillRoot)
}

$installManifest = @{
    updatedUtc = [DateTimeOffset]::UtcNow.ToString("O")
    repoRoot = $RepoRoot
    dotNetWatch = @{
        mode = "wrapper-shadow"
        configuration = $ShadowConfiguration
        wrapperPath = $dotNetWatchWrapperPath
        settingsPath = $dotNetWatchSettingsPath
        shadowManifestPath = $shadowManifestPath
        shadowDllPath = $shadowManifest.shadowDllPath
    }
    components = @{
        configuration = "Release"
        settingsPath = $componentsSettingsPath
        installRoot = $componentsInstallRoot
        entrypointPath = $componentsEntrypoint
    }
    codeAnalytics = @{
        configuration = "Release"
        settingsPath = $codeAnalyticsSettingsPath
        installRoot = $codeAnalyticsInstallRoot
        entrypointPath = $codeAnalyticsEntrypoint
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
    tray = @{
        configuration = "Release"
        installRoot = $trayInstallRoot
        entrypointPath = $trayEntrypoint
        startupShortcutPath = if ($SkipTrayStartupShortcut.IsPresent) { $null } else { $startupShortcutPath }
        desktopShortcutPath = if ($SkipTrayDesktopShortcut.IsPresent) { $null } else { $desktopShortcutPath }
        arguments = $trayArguments
    }
    skills = @{
        sourceRoot = $repoSkillRoot
        targetRoot = $userSkillRoot
        synced = $syncedSkills
    }
    instructions = @{
        repoInstructionsPath = (Join-Path $RepoRoot ".github\copilot-instructions.md")
    }
} | ConvertTo-Json -Depth 10

Set-Content -LiteralPath $manifestPath -Value $installManifest
Write-Status "Wrote install manifest to $manifestPath"

if (-not $SkipVsCodeConfig.IsPresent) {
    Write-Status "Updating VS Code MCP config"
    Update-VsCodeMcpConfig -Path $vscodeMcpPath -WorkspaceFolderToken '${workspaceFolder}' -ComponentsCommandPath $componentsWorkspaceEntrypoint -CodeAnalyticsCommandPath $codeAnalyticsWorkspaceEntrypoint
}

if (-not $SkipUserConfig.IsPresent) {
    Write-Status "Updating Codex config"
    Update-CodexConfig -Path $UserConfigPath -SshOpsEntrypoint $sshOpsEntrypoint -ComponentsEntrypoint $componentsEntrypoint -CodeAnalyticsEntrypoint $codeAnalyticsEntrypoint -SharpToolsEntrypoint $sharpToolsEntrypoint -SharpToolsWorkingDirectory $sharpToolsWorkingDirectory -SharpToolsLogDirectory $sharpToolsLogDirectory
}

Write-Status "Resetup completed."
Write-Status "DotNetWatch shadow DLL: $($shadowManifest.shadowDllPath)"
Write-Status "Components entrypoint: $componentsEntrypoint"
Write-Status "CodeAnalytics entrypoint: $codeAnalyticsEntrypoint"
Write-Status "SshOps entrypoint: $sshOpsEntrypoint"
Write-Status "Manager entrypoint: $managerEntrypoint"
Write-Status "Tray entrypoint: $trayEntrypoint"
Write-Status "Tray desktop shortcut: $desktopShortcutPath"
