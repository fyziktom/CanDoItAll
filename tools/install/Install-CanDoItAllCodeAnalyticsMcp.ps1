[CmdletBinding(SupportsShouldProcess, ConfirmImpact = "Medium")]
param(
    [string]$RepositoryRoot = "",

    [string]$McpRepositoryRoot = "",

    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",

    [Parameter(Mandatory = $true)]
    [string]$SmokeRequestPath,

    [ValidateRange(30, 3600)]
    [int]$PublishTimeoutSeconds = 600,

    [ValidateRange(30, 3600)]
    [int]$HarnessBuildTimeoutSeconds = 300,

    [ValidateRange(30, 3600)]
    [int]$ToolListTimeoutSeconds = 120,

    [ValidateRange(30, 3600)]
    [int]$ImpactAnalysisTimeoutSeconds = 900,

    [ValidateRange(5, 120)]
    [int]$ProcessTerminationTimeoutSeconds = 15
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$helperPaths = @(
    (Join-Path $PSScriptRoot "Install-CanDoItAllCodeAnalyticsMcp.Process.ps1"),
    (Join-Path $PSScriptRoot "Install-CanDoItAllCodeAnalyticsMcp.Support.ps1")
)
foreach ($helperPath in $helperPaths) {
    if (-not (Test-Path -LiteralPath $helperPath -PathType Leaf)) {
        throw "Required installer helper was not found at '$helperPath'."
    }
}
foreach ($helperPath in $helperPaths) {
    . $helperPath
}

$installContext = Resolve-CodeAnalyticsMcpInstallContext `
    -RepositoryRoot $RepositoryRoot `
    -McpRepositoryRoot $McpRepositoryRoot `
    -SmokeRequestPath $SmokeRequestPath `
    -ScriptRoot $PSScriptRoot
$RepositoryRoot = $installContext.RepositoryRoot
$McpRepositoryRoot = $installContext.McpRepositoryRoot
$SmokeRequestPath = $installContext.SmokeRequestPath
$mcpProjectPath = $installContext.McpProjectPath
$harnessProjectPath = $installContext.HarnessProjectPath
$settingsPath = $installContext.SettingsPath
$codeAnalysisRoot = $installContext.CodeAnalysisRoot
$installBasePath = $installContext.InstallBasePath
$currentPath = $installContext.CurrentPath
$currentExecutable = $installContext.CurrentExecutable
$backupRoot = $installContext.BackupRoot
$failedRoot = $installContext.FailedRoot
$manifestPath = $installContext.ManifestPath
$stagePath = $installContext.StagePath
$stageExecutable = $installContext.StageExecutable
$backupPath = $installContext.BackupPath
$failedPath = $installContext.FailedPath

$operation = "publish, protocol-smoke, and switch the CodeAnalytics MCP while retaining the previous install"
if (-not $PSCmdlet.ShouldProcess($currentPath, $operation)) {
    [pscustomobject]@{
        Status = "Preview"
        McpProject = $mcpProjectPath
        StagePath = $stagePath
        CurrentPath = $currentPath
        BackupPath = $backupPath
        SmokeRequestPath = $SmokeRequestPath
    }
    return
}

$installMutexName = Get-InstallMutexName -InstallPath $installBasePath
$installMutex = $null
$installMutexAcquired = $false
$operationFailure = $null
$operationResult = $null
$mutexCleanupFailures = @()

try {
    $installMutex = [System.Threading.Mutex]::new($false, $installMutexName)
    try {
        $installMutexAcquired = $installMutex.WaitOne(0)
    }
    catch [System.Threading.AbandonedMutexException] {
        $installMutexAcquired = $true
        Write-Warning "Recovered the abandoned CodeAnalytics MCP install guard '$installMutexName'."
    }

    if (-not $installMutexAcquired) {
        throw "Another CodeAnalytics MCP installation is already running for '$installBasePath'."
    }

    $stoppedProcessIds = @()
    try {
        New-Item -ItemType Directory -Path $stagePath -Force | Out-Null

        Invoke-CheckedDotNet `
            -Arguments @(
                "publish",
                $mcpProjectPath,
                "--configuration",
                $Configuration,
                "--output",
                $stagePath,
                "-p:UseAppHost=true",
                "-p:CopyRepositoryTemplatesToOutput=false"
            ) `
            -WorkingDirectory $McpRepositoryRoot `
            -FailureMessage "CodeAnalytics MCP publish failed." `
            -TimeoutSeconds $PublishTimeoutSeconds `
            -TerminationTimeoutSeconds $ProcessTerminationTimeoutSeconds |
            Out-Null

        Invoke-CheckedDotNet `
            -Arguments @("build", $harnessProjectPath, "--configuration", $Configuration) `
            -WorkingDirectory $McpRepositoryRoot `
            -FailureMessage "MCP ToolHarness build failed." `
            -TimeoutSeconds $HarnessBuildTimeoutSeconds `
            -TerminationTimeoutSeconds $ProcessTerminationTimeoutSeconds |
            Out-Null

        Invoke-CodeAnalyticsProtocolSmoke `
            -ServerExecutable $stageExecutable `
            -SettingsPath $settingsPath `
            -RequestPath $SmokeRequestPath `
            -HarnessProjectPath $harnessProjectPath `
            -McpRoot $McpRepositoryRoot `
            -WorkspaceRoot $RepositoryRoot `
            -BuildConfiguration $Configuration `
            -ListTimeoutSeconds $ToolListTimeoutSeconds `
            -InvocationTimeoutSeconds $ImpactAnalysisTimeoutSeconds `
            -TerminationTimeoutSeconds $ProcessTerminationTimeoutSeconds

        $stoppedProcessIds = @(
            Stop-OwnedCodeAnalyticsProcesses `
                -ExecutablePath $currentExecutable `
                -TimeoutSeconds $ProcessTerminationTimeoutSeconds
        )
    }
    catch {
        $prePromotionFailure = $_
        $secondaryFailures = @()
        $retainedCandidatePath = $null

        try {
            Stop-OwnedCodeAnalyticsProcesses `
                -ExecutablePath $stageExecutable `
                -TimeoutSeconds $ProcessTerminationTimeoutSeconds |
                Out-Null
        }
        catch {
            $secondaryFailures += "Could not stop the staged executable: $($_.Exception.Message)"
        }

        if (Test-Path -LiteralPath $stagePath -PathType Container) {
            try {
                New-Item -ItemType Directory -Path $failedRoot -Force | Out-Null
                Move-Item -LiteralPath $stagePath -Destination $failedPath -ErrorAction Stop
                $retainedCandidatePath = $failedPath
            }
            catch {
                $secondaryFailures += "Could not retain the failed stage at '$failedPath': $($_.Exception.Message)"
                if (Test-Path -LiteralPath $stagePath -PathType Container) {
                    $retainedCandidatePath = $stagePath
                }
                elseif (Test-Path -LiteralPath $failedPath -PathType Container) {
                    $retainedCandidatePath = $failedPath
                }
            }
        }

        $failureDetails = @()
        if (-not [string]::IsNullOrWhiteSpace($retainedCandidatePath)) {
            $failureDetails += "Failed candidate retained at '$retainedCandidatePath'."
        }

        throw (Get-CombinedFailureMessage `
                -Context "CodeAnalytics MCP pre-promotion failed" `
                -OriginalError $prePromotionFailure `
                -SecondaryFailures $secondaryFailures `
                -Details $failureDetails)
    }

    $hadCurrentInstall = Test-Path -LiteralPath $currentPath -PathType Container
    $currentBackedUp = $false
    $stagePromoted = $false
    $backupRestored = $false

    try {
        New-Item -ItemType Directory -Path $backupRoot -Force | Out-Null
        New-Item -ItemType Directory -Path $failedRoot -Force | Out-Null

        if ($hadCurrentInstall) {
            Move-Item -LiteralPath $currentPath -Destination $backupPath -ErrorAction Stop
            $currentBackedUp = $true
        }

        Move-Item -LiteralPath $stagePath -Destination $currentPath -ErrorAction Stop
        $stagePromoted = $true

        Invoke-CodeAnalyticsProtocolSmoke `
            -ServerExecutable $currentExecutable `
            -SettingsPath $settingsPath `
            -RequestPath $SmokeRequestPath `
            -HarnessProjectPath $harnessProjectPath `
            -McpRoot $McpRepositoryRoot `
            -WorkspaceRoot $RepositoryRoot `
            -BuildConfiguration $Configuration `
            -ListTimeoutSeconds $ToolListTimeoutSeconds `
            -InvocationTimeoutSeconds $ImpactAnalysisTimeoutSeconds `
            -TerminationTimeoutSeconds $ProcessTerminationTimeoutSeconds

        Update-InstallManifest `
            -ManifestPath $manifestPath `
            -InstallRoot $currentPath `
            -EntrypointPath $currentExecutable `
            -SettingsPath $settingsPath `
            -CodeAnalysisRoot $codeAnalysisRoot `
            -BuildConfiguration $Configuration
    }
    catch {
        $promotionFailure = $_
        $secondaryFailures = @()
        $candidateSourcePath = $null
        $candidateExecutable = $null

        if ($stagePromoted -and (Test-Path -LiteralPath $currentPath -PathType Container)) {
            $candidateSourcePath = $currentPath
            $candidateExecutable = $currentExecutable
        }
        elseif (Test-Path -LiteralPath $stagePath -PathType Container) {
            $candidateSourcePath = $stagePath
            $candidateExecutable = $stageExecutable
        }

        if (-not [string]::IsNullOrWhiteSpace($candidateExecutable)) {
            try {
                Stop-OwnedCodeAnalyticsProcesses `
                    -ExecutablePath $candidateExecutable `
                    -TimeoutSeconds $ProcessTerminationTimeoutSeconds |
                    Out-Null
            }
            catch {
                $secondaryFailures += "Could not stop the failed candidate executable: $($_.Exception.Message)"
            }
        }

        $retainedCandidatePath = $candidateSourcePath
        if (-not [string]::IsNullOrWhiteSpace($candidateSourcePath)) {
            try {
                New-Item -ItemType Directory -Path $failedRoot -Force | Out-Null
                Move-Item -LiteralPath $candidateSourcePath -Destination $failedPath -ErrorAction Stop
                $retainedCandidatePath = $failedPath
            }
            catch {
                $secondaryFailures += "Could not retain the failed candidate at '$failedPath': $($_.Exception.Message)"
            }
        }

        if ($currentBackedUp) {
            try {
                if (-not (Test-Path -LiteralPath $backupPath -PathType Container)) {
                    throw "The expected backup '$backupPath' is missing."
                }

                if (Test-Path -LiteralPath $currentPath) {
                    throw "The rollback destination '$currentPath' is still occupied."
                }

                Move-Item -LiteralPath $backupPath -Destination $currentPath -ErrorAction Stop
                $backupRestored = $true
            }
            catch {
                $secondaryFailures += "Could not restore the previous install from '$backupPath': $($_.Exception.Message)"
            }
        }

        $failureDetails = @()
        if (-not [string]::IsNullOrWhiteSpace($retainedCandidatePath)) {
            $failureDetails += "Failed candidate retained at '$retainedCandidatePath'."
        }
        if ($currentBackedUp) {
            if ($backupRestored) {
                $failureDetails += "Previous install restored to '$currentPath'."
            }
            elseif (Test-Path -LiteralPath $backupPath -PathType Container) {
                $failureDetails += "Previous install backup remains at '$backupPath'."
            }
        }

        throw (Get-CombinedFailureMessage `
                -Context "CodeAnalytics MCP promotion failed" `
                -OriginalError $promotionFailure `
                -SecondaryFailures $secondaryFailures `
                -Details $failureDetails)
    }

    $operationResult = [pscustomobject]@{
        Status = "Succeeded"
        CurrentPath = $currentPath
        EntrypointPath = $currentExecutable
        BackupPath = if ($hadCurrentInstall) { $backupPath } else { $null }
        StoppedProcessIds = $stoppedProcessIds
        SmokeRequestPath = $SmokeRequestPath
    }
}
catch {
    $operationFailure = $_
}
finally {
    if ($installMutexAcquired) {
        try {
            $installMutex.ReleaseMutex()
        }
        catch {
            $mutexCleanupFailures += "Could not release install guard '$installMutexName': $($_.Exception.Message)"
        }
    }

    if ($null -ne $installMutex) {
        try {
            $installMutex.Dispose()
        }
        catch {
            $mutexCleanupFailures += "Could not dispose install guard '$installMutexName': $($_.Exception.Message)"
        }
    }
}

if ($null -ne $operationFailure) {
    if ($mutexCleanupFailures.Count -gt 0) {
        throw (Get-CombinedFailureMessage `
                -Context "CodeAnalytics MCP installation failed" `
                -OriginalError $operationFailure `
                -SecondaryFailures $mutexCleanupFailures)
    }

    throw $operationFailure
}

if ($mutexCleanupFailures.Count -gt 0) {
    throw "CodeAnalytics MCP installation succeeded, but install-guard cleanup failed: $($mutexCleanupFailures -join ' | ')"
}

$operationResult
