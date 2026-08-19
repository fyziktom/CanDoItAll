function Resolve-AbsolutePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue,

        [Parameter(Mandatory = $true)]
        [string]$BasePath
    )

    if ([System.IO.Path]::IsPathRooted($PathValue)) {
        return [System.IO.Path]::GetFullPath($PathValue)
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $PathValue))
}

function Assert-PathBelow {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ParentPath,

        [Parameter(Mandatory = $true)]
        [string]$ChildPath,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $directorySeparator = [System.IO.Path]::DirectorySeparatorChar
    $normalizedParent = [System.IO.Path]::GetFullPath($ParentPath).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar
    )
    $parentPrefix = $normalizedParent + $directorySeparator
    $normalizedChild = [System.IO.Path]::GetFullPath($ChildPath)
    $isParent = $normalizedChild.Equals($normalizedParent, [System.StringComparison]::OrdinalIgnoreCase)
    $isBelowParent = $normalizedChild.StartsWith($parentPrefix, [System.StringComparison]::OrdinalIgnoreCase)
    if (-not $isParent -and -not $isBelowParent) {
        throw "$Description '$ChildPath' is outside the owned root '$ParentPath'."
    }
}

function Invoke-CodeAnalyticsProtocolSmoke {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ServerExecutable,

        [Parameter(Mandatory = $true)]
        [string]$SettingsPath,

        [Parameter(Mandatory = $true)]
        [string]$RequestPath,

        [Parameter(Mandatory = $true)]
        [string]$HarnessProjectPath,

        [Parameter(Mandatory = $true)]
        [string]$McpRoot,

        [Parameter(Mandatory = $true)]
        [string]$WorkspaceRoot,

        [Parameter(Mandatory = $true)]
        [string]$BuildConfiguration,

        [Parameter(Mandatory = $true)]
        [int]$ListTimeoutSeconds,

        [Parameter(Mandatory = $true)]
        [int]$InvocationTimeoutSeconds,

        [Parameter(Mandatory = $true)]
        [int]$TerminationTimeoutSeconds
    )

    if (-not (Test-Path -LiteralPath $ServerExecutable -PathType Leaf)) {
        throw "Published CodeAnalytics MCP entry point was not found at '$ServerExecutable'."
    }

    $commonArguments = @(
        "run",
        "--project",
        $HarnessProjectPath,
        "--configuration",
        $BuildConfiguration,
        "--no-build",
        "--",
        "--server-command",
        $ServerExecutable,
        "--server-arg",
        "--settings",
        "--server-arg",
        $SettingsPath,
        "--working-directory",
        $WorkspaceRoot
    )

    $listOutput = Invoke-CheckedDotNet `
        -Arguments ($commonArguments + @("--tool", "tools/list")) `
        -WorkingDirectory $McpRoot `
        -FailureMessage "CodeAnalytics MCP tools/list smoke failed." `
        -TimeoutSeconds $ListTimeoutSeconds `
        -TerminationTimeoutSeconds $TerminationTimeoutSeconds `
        -Quiet
    $listText = $listOutput -join [Environment]::NewLine
    if ($listText.IndexOf("code_analytics_impacted_tests_get", [System.StringComparison]::Ordinal) -lt 0) {
        throw "The published CodeAnalytics MCP does not expose code_analytics_impacted_tests_get."
    }

    Invoke-CheckedDotNet `
        -Arguments (
            $commonArguments + @(
                "--tool",
                "code_analytics_impacted_tests_get",
                "--arguments-file",
                $RequestPath
            )
        ) `
        -WorkingDirectory $McpRoot `
        -FailureMessage "CodeAnalytics impacted-test invocation smoke failed." `
        -TimeoutSeconds $InvocationTimeoutSeconds `
        -TerminationTimeoutSeconds $TerminationTimeoutSeconds `
        -Quiet |
        Out-Null
}

function Get-OwnedCodeAnalyticsProcesses {
    param(
        [Parameter(Mandatory = $true)]
        [string]$CurrentExecutable
    )

    return @(
        Get-Process -Name "CanDoItAll.Mcp.CodeAnalytics" -ErrorAction SilentlyContinue |
            Where-Object {
                try {
                    [string]::Equals(
                        [System.IO.Path]::GetFullPath($_.Path),
                        $CurrentExecutable,
                        [System.StringComparison]::OrdinalIgnoreCase)
                }
                catch {
                    $false
                }
            }
    )
}

function Stop-OwnedCodeAnalyticsProcesses {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ExecutablePath,

        [Parameter(Mandatory = $true)]
        [int]$TimeoutSeconds
    )

    $ownedProcesses = Get-OwnedCodeAnalyticsProcesses -CurrentExecutable $ExecutablePath
    $stoppedProcessIds = @()
    foreach ($process in $ownedProcesses) {
        Write-Host "Stopping owned CodeAnalytics MCP process $($process.Id) at '$ExecutablePath'."
        Stop-Process -Id $process.Id -Force -ErrorAction Stop
        try {
            Wait-Process -Id $process.Id -Timeout $TimeoutSeconds -ErrorAction Stop
        }
        catch {
            if (Get-Process -Id $process.Id -ErrorAction SilentlyContinue) {
                throw "Owned CodeAnalytics MCP process $($process.Id) did not exit within $TimeoutSeconds seconds."
            }
        }

        $stoppedProcessIds += $process.Id
    }

    return $stoppedProcessIds
}

function Get-InstallMutexName {
    param(
        [Parameter(Mandatory = $true)]
        [string]$InstallPath
    )

    $normalizedPath = [System.IO.Path]::GetFullPath($InstallPath).ToUpperInvariant()
    $hasher = [System.Security.Cryptography.SHA256]::Create()
    try {
        $pathBytes = [System.Text.Encoding]::UTF8.GetBytes($normalizedPath)
        $hash = [System.BitConverter]::ToString($hasher.ComputeHash($pathBytes)).Replace("-", "")
        return "Local\CanDoItAll.CodeAnalyticsMcp.Install.$($hash.Substring(0, 24))"
    }
    finally {
        $hasher.Dispose()
    }
}

function Get-CombinedFailureMessage {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Context,

        [Parameter(Mandatory = $true)]
        [System.Management.Automation.ErrorRecord]$OriginalError,

        [string[]]$SecondaryFailures = @(),

        [string[]]$Details = @()
    )

    $parts = @("${Context}: $($OriginalError.Exception.Message)")
    $parts += $Details | Where-Object { -not [string]::IsNullOrWhiteSpace($_) }
    if ($SecondaryFailures.Count -gt 0) {
        $parts += "Secondary cleanup failures: $($SecondaryFailures -join ' | ')"
    }

    return $parts -join " "
}

function Update-InstallManifest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ManifestPath,

        [Parameter(Mandatory = $true)]
        [string]$InstallRoot,

        [Parameter(Mandatory = $true)]
        [string]$EntrypointPath,

        [Parameter(Mandatory = $true)]
        [string]$SettingsPath,

        [Parameter(Mandatory = $true)]
        [string]$CodeAnalysisRoot,

        [Parameter(Mandatory = $true)]
        [string]$BuildConfiguration
    )

    if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
        return
    }

    $manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
    $manifest.updatedUtc = [DateTimeOffset]::UtcNow.ToString("O")
    $manifest.codeAnalytics.configuration = $BuildConfiguration
    $manifest.codeAnalytics.codeAnalysisRepoRoot = $CodeAnalysisRoot
    $manifest.codeAnalytics.settingsPath = $SettingsPath
    $manifest.codeAnalytics.installRoot = $InstallRoot
    $manifest.codeAnalytics.entrypointPath = $EntrypointPath
    $manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $ManifestPath -Encoding utf8
}

function Resolve-CodeAnalyticsMcpInstallContext {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$RepositoryRoot,

        [Parameter(Mandatory = $true)]
        [AllowEmptyString()]
        [string]$McpRepositoryRoot,

        [Parameter(Mandatory = $true)]
        [string]$SmokeRequestPath,

        [Parameter(Mandatory = $true)]
        [string]$ScriptRoot
    )

    if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
        $RepositoryRoot = Join-Path $ScriptRoot "..\.."
    }
    $RepositoryRoot = Resolve-AbsolutePath -PathValue $RepositoryRoot -BasePath $ScriptRoot

    if ([string]::IsNullOrWhiteSpace($McpRepositoryRoot)) {
        $McpRepositoryRoot = Join-Path (Split-Path -Parent $RepositoryRoot) "CanDoItAll.Mcp"
    }
    $McpRepositoryRoot = Resolve-AbsolutePath -PathValue $McpRepositoryRoot -BasePath $RepositoryRoot
    $SmokeRequestPath = Resolve-AbsolutePath -PathValue $SmokeRequestPath -BasePath $RepositoryRoot

    $mcpProjectPath = Join-Path $McpRepositoryRoot "src\CanDoItAll.Mcp.CodeAnalytics\CanDoItAll.Mcp.CodeAnalytics.csproj"
    $harnessProjectPath = Join-Path $McpRepositoryRoot "tools\CanDoItAll.Mcp.ToolHarness\CanDoItAll.Mcp.ToolHarness.csproj"
    $settingsPath = Join-Path $RepositoryRoot "CanDoItAll.Mcp.CodeAnalytics.settings.json"
    $codeAnalysisRoot = Join-Path (Split-Path -Parent $McpRepositoryRoot) "CanDoItAll.CodeAnalysis"
    $requiredFiles = @(
        (Join-Path $RepositoryRoot "CanDoItAll.slnx"),
        (Join-Path $McpRepositoryRoot "CanDoItAll.Mcp.slnx"),
        $mcpProjectPath,
        $harnessProjectPath,
        $settingsPath,
        (Join-Path $codeAnalysisRoot "src\CanDoItAll.CodeAnalytics.Application\CanDoItAll.CodeAnalytics.Application.csproj"),
        $SmokeRequestPath
    )
    foreach ($requiredFile in $requiredFiles) {
        if (-not (Test-Path -LiteralPath $requiredFile -PathType Leaf)) {
            throw "Required file was not found at '$requiredFile'."
        }
    }

    Get-Content -LiteralPath $SmokeRequestPath -Raw | ConvertFrom-Json | Out-Null

    $artifactRoot = Join-Path $RepositoryRoot ".artifacts\mcp-installs"
    $installBasePath = Join-Path $artifactRoot "CanDoItAll.Mcp.CodeAnalytics"
    $currentPath = Join-Path $installBasePath "current"
    $stagingRoot = Join-Path $installBasePath "staging"
    $backupRoot = Join-Path $installBasePath "backups"
    $failedRoot = Join-Path $installBasePath "failed"
    $runStamp = [DateTimeOffset]::UtcNow.ToString("yyyyMMdd-HHmmssfff")
    $stagePath = Join-Path $stagingRoot $runStamp
    $backupPath = Join-Path $backupRoot "current-$runStamp"
    $failedPath = Join-Path $failedRoot "failed-$runStamp"

    Assert-PathBelow -ParentPath $artifactRoot -ChildPath $installBasePath -Description "Install base"
    Assert-PathBelow -ParentPath $installBasePath -ChildPath $currentPath -Description "Current install"
    Assert-PathBelow -ParentPath $installBasePath -ChildPath $stagePath -Description "Staging install"
    Assert-PathBelow -ParentPath $installBasePath -ChildPath $backupPath -Description "Backup install"
    Assert-PathBelow -ParentPath $installBasePath -ChildPath $failedPath -Description "Failed install"

    return [pscustomobject]@{
        RepositoryRoot = $RepositoryRoot
        McpRepositoryRoot = $McpRepositoryRoot
        SmokeRequestPath = $SmokeRequestPath
        McpProjectPath = $mcpProjectPath
        HarnessProjectPath = $harnessProjectPath
        SettingsPath = $settingsPath
        CodeAnalysisRoot = $codeAnalysisRoot
        ArtifactRoot = $artifactRoot
        InstallBasePath = $installBasePath
        CurrentPath = $currentPath
        CurrentExecutable = Join-Path $currentPath "CanDoItAll.Mcp.CodeAnalytics.exe"
        BackupRoot = $backupRoot
        FailedRoot = $failedRoot
        ManifestPath = Join-Path $artifactRoot "install-manifest.json"
        StagePath = $stagePath
        StageExecutable = Join-Path $stagePath "CanDoItAll.Mcp.CodeAnalytics.exe"
        BackupPath = $backupPath
        FailedPath = $failedPath
    }
}
