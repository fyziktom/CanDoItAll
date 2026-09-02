#requires -Version 7.4

[CmdletBinding()]
param(
    [switch]$Reset,
    [switch]$SkipImageBuild,
    [ValidateSet(
        "prepare",
        "normal",
        "unpublished",
        "republished",
        "identity-mismatch",
        "identity-restored",
        "outage",
        "recovery")]
    [string]$StartAt = "prepare"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$PSNativeCommandUseErrorActionPreference = $false

$RepositoryRoot = [System.IO.Path]::GetFullPath((Join-Path $PSScriptRoot "..\.."))
$ArtifactRoot = [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot ".artifacts\shared-providers-e2e"))
$ToolStateRoot = Join-Path $ArtifactRoot "tool-state"
$HandoffRoot = Join-Path $ToolStateRoot "handoff"
$ScenarioResultsRoot = Join-Path $ToolStateRoot "scenario-results"
$CollectedLogsRoot = Join-Path $ToolStateRoot "logs"
$HostCaptureRoot = Join-Path $ArtifactRoot "host-only-captures"
$HostCommandCaptureRoot = Join-Path $HostCaptureRoot "commands"
$DockerLogsRoot = Join-Path $CollectedLogsRoot "docker"
$DockerCommandLogsRoot = Join-Path $DockerLogsRoot "commands"
$DockerServiceLogsRoot = Join-Path $DockerLogsRoot "services"
$RuntimeSecretsRoot = Join-Path $ArtifactRoot "runtime-secrets"
$CredentialsRoot = Join-Path $ToolStateRoot "credentials"
$HostScanTemporaryRoot = Join-Path $ArtifactRoot "host-only-scan-temp"
$HostRunMetadataPath = Join-Path $ArtifactRoot "host-run-metadata.json"
$ToolPublishRoot = Join-Path $ArtifactRoot "tool-publish"
$ComposeFile = Join-Path $RepositoryRoot "compose.shared-providers.e2e.yaml"
$ComposeEnvironmentFile = Join-Path $RepositoryRoot ".env.shared-providers.e2e.example"
$ToolProject = Join-Path $RepositoryRoot "tools\SharedProviders\CanDoItAll.SharedProviders.E2E\CanDoItAll.SharedProviders.E2E.csproj"
$ToolAssembly = Join-Path $RepositoryRoot "tools\SharedProviders\CanDoItAll.SharedProviders.E2E\bin\Release\net10.0\CanDoItAll.SharedProviders.E2E.dll"
$AppDockerfile = Join-Path $RepositoryRoot "src\App\CanDoItAll.Web\Dockerfile"
$UpstreamRoot = Join-Path $RepositoryRoot "tests\Support\CanDoItAll.SharedProviders.TestUpstream"
$UpstreamDockerfile = Join-Path $UpstreamRoot "Dockerfile"
$ComponentsRoot = [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot "..\CanDoItAll.Components"))
$FileToolsRoot = [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot "..\CanDoItAll.FileTools"))
$ComposeProjectName = "candoitall-shared-providers-e2e"
$AppImage = $null
$UpstreamImage = $null
$SourceFingerprint = $null
$SourceState = $null
$RunMarker = $null
$AppUid = $null
$AppGid = $null
$LogMarkerCounts = $null
$LastLogCheckpoint = $null
$ResumeCount = 0
$ContentCanary = "SB07_E2E_CONTENT_CANARY_7f3cc8c4"
$RootMarkerName = ".shared-providers-e2e-root"
$RootMarkerValue = "CanDoItAll.SharedProviders.E2E/v1"
$MaximumCollectedLogBytes = 28MB
$MaximumDatabaseDumpBytes = 64MB
$MaximumScanFileCount = 4096
$BoundedProcessRunnerSource = Join-Path $PSScriptRoot "CanDoItAll.SharedProviders.E2E\BoundedProcessRunner.cs"
$BuildTranscriptRoot = Join-Path `
    (Join-Path $RepositoryRoot ".artifacts") `
    ("shared-providers-e2e-build-{0:N}" -f [Guid]::NewGuid())

$RuntimeSecretNames = @(
    "db-admin-password",
    "db-central-password",
    "db-client-a-password",
    "db-client-b-password",
    "db-central-connection-string",
    "db-client-a-connection-string",
    "db-client-b-connection-string",
    "api-central-signing-key",
    "api-client-a-signing-key",
    "api-client-b-signing-key",
    "upstream-data-token",
    "upstream-control-token",
    "personal-upstream-data-token",
    "personal-upstream-control-token"
)
$CredentialNames = @(
    "central-access.token",
    "central-catalog-only.token",
    "central-invoke-only.token",
    "client-a-access.token",
    "client-b-access.token"
)
$PersistentDockerServices = @(
    "artifact-permissions",
    "central",
    "client-a",
    "client-b",
    "db",
    "deterministic-personal-upstream",
    "deterministic-upstream"
)
$ExpectedDockerServiceCoverage = @(
    "artifact-permissions",
    "central",
    "client-a",
    "client-b",
    "db",
    "deterministic-personal-upstream",
    "deterministic-upstream",
    "e2e-central",
    "e2e-client-a",
    "e2e-client-b",
    "e2e-runner"
)
$ExpectedScenarioIds = @(
    "central-catalog-publication-boundary",
    "client-a-text-import-with-personal-provider",
    "client-b-text-and-image-imports",
    "source-resync-idempotency-and-stable-local-ids",
    "duplicate-upstream-model-routing",
    "chat-completions-and-responses-buffered",
    "chat-completions-and-responses-streaming",
    "function-tool-call-roundtrip",
    "structured-output-capability-allow-deny",
    "openai-and-comfyui-image-generation",
    "catalog-etag-not-modified",
    "catalog-and-inference-scope-isolation",
    "malformed-access-context-rejected",
    "access-context-central-only",
    "unpublish-and-reappearance",
    "central-outage-recovery-no-fallback",
    "source-identity-mismatch",
    "streaming-disconnect-cancellation",
    "secret-content-audit-redaction"
)
$PhaseOrder = @(
    "prepare",
    "normal",
    "unpublished",
    "republished",
    "identity-mismatch",
    "identity-restored",
    "outage",
    "recovery"
)
$ComposeBaseArguments = @(
    "compose",
    "--ansi",
    "never",
    "--project-name",
    $ComposeProjectName,
    "--env-file",
    $ComposeEnvironmentFile,
    "--file",
    $ComposeFile,
    "--profile",
    "orchestrator"
)
$ChildEnvironmentKeysToRemove = @(
    "COMPOSE_FILE",
    "COMPOSE_PROJECT_NAME",
    "COMPOSE_PROFILES",
    "COMPOSE_ENV_FILES",
    "COMPOSE_PATH_SEPARATOR",
    "COMPOSE_CONVERT_WINDOWS_PATHS",
    "DOCKER_API_VERSION",
    "DOCKER_CERT_PATH",
    "DOCKER_CONFIG",
    "DOCKER_CONTEXT",
    "DOCKER_HOST",
    "DOCKER_TLS_VERIFY"
)
$ChildEnvironmentAssignments = @()
$Utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$script:CommandSequence = 0

if ($null -eq ("CanDoItAll.SharedProviders.E2E.BoundedProcessRunner" -as [type])) {
    Add-Type -Path $BoundedProcessRunnerSource
}

function Write-Step {
    param([Parameter(Mandatory = $true)][string]$Message)

    Write-Host "[shared-providers-e2e] $Message"
}

function Write-Utf8File {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Content
    )

    $parent = Split-Path -Parent $Path
    Assert-SafeManagedDirectory -Path $parent
    [System.IO.Directory]::CreateDirectory($parent) | Out-Null
    Assert-SafeManagedRegularFileTarget -Path $Path
    [System.IO.File]::WriteAllText($Path, $Content, $Utf8NoBom)
    if (Test-IsPathWithin -Candidate $Path -Root $HostCaptureRoot) {
        Set-PrivateHostCaptureFile -Path $Path
    }
    Assert-SafeManagedRegularFileTarget -Path $Path
}

function Write-JsonFile {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][object]$Value
    )

    Write-Utf8File -Path $Path -Content ($Value | ConvertTo-Json -Depth 12)
}

function Invoke-NativeCommand {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [string]$CaptureBasePath,
        [int[]]$AllowedExitCodes = @(0),
        [int]$TimeoutSeconds = 1200,
        [long]$MaximumStandardOutputBytes = 6MB,
        [long]$MaximumStandardErrorBytes = 2MB
    )

    $stdoutPath = $null
    $stderrPath = $null
    if (-not [string]::IsNullOrWhiteSpace($CaptureBasePath)) {
        $captureParent = Split-Path -Parent $CaptureBasePath
        Assert-SafeManagedDirectory -Path $captureParent
        [System.IO.Directory]::CreateDirectory($captureParent) | Out-Null
        if (Test-IsPathWithin -Candidate $captureParent -Root $HostCaptureRoot) {
            Protect-HostCaptureDirectory -Path $captureParent
        }
        $stdoutPath = "$CaptureBasePath.stdout.log"
        $stderrPath = "$CaptureBasePath.stderr.log"
        Assert-SafeManagedRegularFileTarget -Path $stdoutPath
        Assert-SafeManagedRegularFileTarget -Path $stderrPath
    }

    $result = [CanDoItAll.SharedProviders.E2E.BoundedProcessRunner]::Run(
        $FilePath,
        $Arguments,
        $RepositoryRoot,
        $stdoutPath,
        $stderrPath,
        $TimeoutSeconds,
        $MaximumStandardOutputBytes,
        $MaximumStandardErrorBytes,
        $ChildEnvironmentKeysToRemove,
        $ChildEnvironmentAssignments)
    if ($null -ne $stdoutPath) {
        if (Test-IsPathWithin -Candidate $stdoutPath -Root $HostCaptureRoot) {
            Set-PrivateHostCaptureFile -Path $stdoutPath
            Set-PrivateHostCaptureFile -Path $stderrPath
        }

        Assert-SafeManagedRegularFileTarget -Path $stdoutPath
        Assert-SafeManagedRegularFileTarget -Path $stderrPath
    }

    if ($result.TimedOut) {
        throw "A required native command exceeded its wall-clock deadline."
    }

    if ($result.OutputLimitExceeded) {
        throw "A required native command exceeded its live output limit."
    }

    $exitCode = $result.ExitCode
    if ($null -ne $stdoutPath) {
        $capturedFiles = @($stdoutPath, $stderrPath) | Where-Object { Test-Path -LiteralPath $_ }
        foreach ($path in $capturedFiles) {
            Assert-SafeManagedRegularFileTarget -Path $path
            if ((Get-Item -LiteralPath $path).Length -eq 0) {
                Remove-Item -LiteralPath $path -Force
                Assert-SafeManagedRegularFileTarget -Path $path
            }
        }
    }

    if ($AllowedExitCodes -notcontains $exitCode) {
        throw "A required native command failed with exit code $exitCode."
    }

    return $exitCode
}

function Invoke-NativeText {
    param(
        [Parameter(Mandatory = $true)][string]$FilePath,
        [Parameter(Mandatory = $true)][string[]]$Arguments
    )

    $result = [CanDoItAll.SharedProviders.E2E.BoundedProcessRunner]::Run(
        $FilePath,
        $Arguments,
        $RepositoryRoot,
        $null,
        $null,
        120,
        1MB,
        1MB,
        $ChildEnvironmentKeysToRemove,
        $ChildEnvironmentAssignments)
    if ($result.TimedOut) {
        throw "A required native query exceeded its wall-clock deadline."
    }

    if ($result.OutputLimitExceeded) {
        throw "A required native query exceeded its live output limit."
    }

    if ($result.ExitCode -ne 0) {
        throw "A required native query failed with exit code $($result.ExitCode)."
    }

    return $result.StandardOutput.Trim()
}

function Test-SourceFingerprintPathIncluded {
    param([Parameter(Mandatory = $true)][string]$RelativePath)

    $normalized = $RelativePath.Replace("\", "/")
    if ($normalized -match "(^|/)(\.artifacts|artifacts|bin|obj|node_modules|proof|TestResults|coverage|playwright-report|test-results)(/|$)") {
        return $false
    }

    return -not ($normalized.EndsWith(".log", [StringComparison]::OrdinalIgnoreCase) -or
        $normalized.EndsWith(".binlog", [StringComparison]::OrdinalIgnoreCase))
}

function Add-FingerprintValue {
    param(
        [Parameter(Mandatory = $true)][System.Security.Cryptography.IncrementalHash]$Hash,
        [Parameter(Mandatory = $true)][AllowEmptyString()][string]$Value
    )

    $bytes = $Utf8NoBom.GetBytes($Value)
    $length = [BitConverter]::GetBytes([int]$bytes.Length)
    if (-not [BitConverter]::IsLittleEndian) {
        [Array]::Reverse($length)
    }

    $Hash.AppendData($length)
    $Hash.AppendData($bytes)
}

function Get-SourceState {
    $repositories = [ordered]@{
        "CanDoItAll" = $RepositoryRoot
        "CanDoItAll.Components" = $ComponentsRoot
        "CanDoItAll.FileTools" = $FileToolsRoot
    }
    $hash = [System.Security.Cryptography.IncrementalHash]::CreateHash(
        [System.Security.Cryptography.HashAlgorithmName]::SHA256)
    $repositoryStates = [System.Collections.Generic.List[object]]::new()
    try {
        foreach ($repository in $repositories.GetEnumerator()) {
            $root = [System.IO.Path]::GetFullPath([string]$repository.Value)
            Assert-NoLinkOrDevice -Path $root
            $revision = Invoke-NativeText `
                -FilePath "git" `
                -Arguments @("-c", "safe.directory=$root", "-C", $root, "rev-parse", "HEAD")
            $listResult = [CanDoItAll.SharedProviders.E2E.BoundedProcessRunner]::Run(
                "git",
                @(
                    "-c",
                    "safe.directory=$root",
                    "-C",
                    $root,
                    "ls-files",
                    "-z",
                    "--cached",
                    "--others",
                    "--exclude-standard"),
                $root,
                $null,
                $null,
                120,
                8MB,
                1MB,
                $ChildEnvironmentKeysToRemove,
                $ChildEnvironmentAssignments)
            if ($listResult.TimedOut -or
                $listResult.OutputLimitExceeded -or
                $listResult.ExitCode -ne 0) {
                throw "A governed source repository could not be enumerated within its bound."
            }

            Add-FingerprintValue -Hash $hash -Value ([string]$repository.Key)
            Add-FingerprintValue -Hash $hash -Value $revision
            $fileCount = 0
            $relativePaths = @($listResult.StandardOutput.Split(
                    [char]0,
                    [StringSplitOptions]::RemoveEmptyEntries) |
                Where-Object { Test-SourceFingerprintPathIncluded -RelativePath $_ } |
                Sort-Object -Unique)
            foreach ($relativePath in $relativePaths) {
                $fullPath = [System.IO.Path]::GetFullPath((Join-Path $root $relativePath))
                if (-not (Test-IsPathWithin -Candidate $fullPath -Root $root)) {
                    throw "A governed source path escaped its repository root."
                }

                Assert-ExistingPathComponents -Root $root -Path $fullPath

                Add-FingerprintValue -Hash $hash -Value $relativePath.Replace("\", "/")
                if (-not (Test-Path -LiteralPath $fullPath)) {
                    Add-FingerprintValue -Hash $hash -Value "<deleted>"
                    $fileCount++
                    continue
                }

                $attributes = [System.IO.File]::GetAttributes($fullPath)
                $invalidAttributes = [System.IO.FileAttributes]::Directory -bor
                    [System.IO.FileAttributes]::ReparsePoint -bor
                    [System.IO.FileAttributes]::Device
                if (($attributes -band $invalidAttributes) -ne 0) {
                    throw "A governed source input is not an exact regular file."
                }

                $stream = [System.IO.File]::OpenRead($fullPath)
                try {
                    $contentHash = [Convert]::ToHexString(
                        [System.Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant()
                }
                finally {
                    $stream.Dispose()
                }

                Add-FingerprintValue -Hash $hash -Value $contentHash
                $fileCount++
            }

            $repositoryStates.Add([ordered]@{
                name = [string]$repository.Key
                revision = $revision
                fileCount = $fileCount
            })
        }

        return [pscustomobject]@{
            Fingerprint = [Convert]::ToHexString($hash.GetHashAndReset()).ToLowerInvariant()
            Repositories = @($repositoryStates)
        }
    }
    finally {
        $hash.Dispose()
    }
}

function Get-HostContainerIdentity {
    if ($IsWindows) {
        return [pscustomobject]@{ Uid = "1654"; Gid = "1654" }
    }

    $uid = Invoke-NativeText -FilePath "id" -Arguments @("-u")
    $gid = Invoke-NativeText -FilePath "id" -Arguments @("-g")
    if ($uid -notmatch "^[1-9][0-9]{0,9}$" -or $gid -notmatch "^[1-9][0-9]{0,9}$") {
        throw "The native Unix proof requires an exact non-root host UID and GID."
    }

    return [pscustomobject]@{ Uid = $uid; Gid = $gid }
}

function Set-ChildEnvironmentAssignments {
    $script:ChildEnvironmentAssignments = @(
        "E2E_COMPOSE_PROJECT_NAME=$ComposeProjectName",
        "E2E_ARTIFACT_ROOT=$ArtifactRoot",
        "E2E_APP_IMAGE=$AppImage",
        "E2E_UPSTREAM_IMAGE=$UpstreamImage",
        "E2E_RUN_MARKER=$RunMarker",
        "E2E_CENTRAL_PORT=5210",
        "E2E_CLIENT_A_PORT=5211",
        "E2E_CLIENT_B_PORT=5212",
        "E2E_UPSTREAM_PORT=5213",
        "E2E_PERSONAL_UPSTREAM_PORT=5214",
        "E2E_POSTGRES_PORT=55432",
        "E2E_LOCAL_INGRESS_SUBNET=10.245.0.0/24",
        "E2E_LOCAL_INGRESS_GATEWAY=10.245.0.1",
        "E2E_DB_ADMIN_PASSWORD_FILE=$(Join-Path $RuntimeSecretsRoot 'db-admin-password')",
        "E2E_DB_CENTRAL_PASSWORD_FILE=$(Join-Path $RuntimeSecretsRoot 'db-central-password')",
        "E2E_DB_CLIENT_A_PASSWORD_FILE=$(Join-Path $RuntimeSecretsRoot 'db-client-a-password')",
        "E2E_DB_CLIENT_B_PASSWORD_FILE=$(Join-Path $RuntimeSecretsRoot 'db-client-b-password')",
        "E2E_DB_CENTRAL_CONNECTION_STRING_FILE=$(Join-Path $RuntimeSecretsRoot 'db-central-connection-string')",
        "E2E_DB_CLIENT_A_CONNECTION_STRING_FILE=$(Join-Path $RuntimeSecretsRoot 'db-client-a-connection-string')",
        "E2E_DB_CLIENT_B_CONNECTION_STRING_FILE=$(Join-Path $RuntimeSecretsRoot 'db-client-b-connection-string')",
        "E2E_API_CENTRAL_SIGNING_KEY_FILE=$(Join-Path $RuntimeSecretsRoot 'api-central-signing-key')",
        "E2E_API_CLIENT_A_SIGNING_KEY_FILE=$(Join-Path $RuntimeSecretsRoot 'api-client-a-signing-key')",
        "E2E_API_CLIENT_B_SIGNING_KEY_FILE=$(Join-Path $RuntimeSecretsRoot 'api-client-b-signing-key')",
        "E2E_UPSTREAM_DATA_TOKEN_FILE=$(Join-Path $RuntimeSecretsRoot 'upstream-data-token')",
        "E2E_UPSTREAM_CONTROL_TOKEN_FILE=$(Join-Path $RuntimeSecretsRoot 'upstream-control-token')",
        "E2E_PERSONAL_UPSTREAM_DATA_TOKEN_FILE=$(Join-Path $RuntimeSecretsRoot 'personal-upstream-data-token')",
        "E2E_PERSONAL_UPSTREAM_CONTROL_TOKEN_FILE=$(Join-Path $RuntimeSecretsRoot 'personal-upstream-control-token')",
        "E2E_APP_UID=$AppUid",
        "E2E_APP_GID=$AppGid",
        "DOCKER_CONTEXT=default",
        "DOTNET_SDK_VERSION=10.0.302",
        "DOTNET_RUNTIME_VERSION=10.0.10",
        "POSTGRES_IMAGE_TAG=16-alpine",
        "BUSYBOX_VERSION=1.37.0-musl"
    )
}

function New-EmptyLogMarkerCounts {
    return [ordered]@{
        "central" = 0
        "client-a" = 0
        "client-b" = 0
        "db" = 0
        "deterministic-personal-upstream" = 0
        "deterministic-upstream" = 0
    }
}

function Assert-SourceStateUnchanged {
    $current = Get-SourceState
    if ($current.Fingerprint -cne $SourceFingerprint) {
        throw "The governed source/worktree fingerprint changed during the E2E lifecycle."
    }
}

function Assert-LocalDefaultDockerContext {
    $endpoint = Invoke-NativeText `
        -FilePath "docker" `
        -Arguments @(
            "context",
            "inspect",
            "default",
            "--format",
            '{{.Endpoints.docker.Host}}')
    $local = if ($IsWindows) {
        $endpoint -in @(
            "npipe:////./pipe/docker_engine",
            "npipe://./pipe/docker_engine")
    }
    else {
        $endpoint.StartsWith("unix:///", [StringComparison]::Ordinal)
    }
    if (-not $local) {
        throw "The governed E2E lifecycle requires Docker's exact local default context."
    }
}

function Write-HostRunMetadata {
    param([bool]$RevalidateSource = $true)

    if ($RevalidateSource) {
        Assert-SourceStateUnchanged
    }

    Write-JsonFile -Path $HostRunMetadataPath -Value ([ordered]@{
        schemaVersion = 1
        sourceFingerprint = $SourceFingerprint
        runMarker = $RunMarker
        appImage = $AppImage
        upstreamImage = $UpstreamImage
        repositories = @($SourceState.Repositories)
        markerCounts = $LogMarkerCounts
        lastLogCheckpoint = $LastLogCheckpoint
        resumeCount = $ResumeCount
    })
    if (-not $IsWindows) {
        [System.IO.File]::SetUnixFileMode(
            $HostRunMetadataPath,
            [System.IO.UnixFileMode]::UserRead -bor [System.IO.UnixFileMode]::UserWrite)
    }

    Assert-OwnedRegularFileTarget -Path $HostRunMetadataPath
}

function Read-HostRunMarker {
    Assert-OwnedRegularFileTarget -Path $HostRunMetadataPath
    if (-not (Test-Path -LiteralPath $HostRunMetadataPath -PathType Leaf) -or
        (Get-Item -LiteralPath $HostRunMetadataPath -Force).Length -gt 32KB) {
        throw "The host-only E2E run metadata is missing or invalid."
    }

    $metadata = Get-Content -LiteralPath $HostRunMetadataPath -Raw | ConvertFrom-Json
    Assert-OwnedRegularFileTarget -Path $HostRunMetadataPath
    $expectedCounts = New-EmptyLogMarkerCounts
    $actualCountNames = @($metadata.markerCounts.PSObject.Properties.Name)
    $actualCountKey = ($actualCountNames | Sort-Object) -join "`n"
    $expectedCountKey = ($expectedCounts.Keys | Sort-Object) -join "`n"
    if ($metadata.schemaVersion -ne 1 -or
        [string]$metadata.sourceFingerprint -cne $SourceFingerprint -or
        [string]$metadata.appImage -cne $AppImage -or
        [string]$metadata.upstreamImage -cne $UpstreamImage -or
        [string]$metadata.runMarker -notmatch "^sb07-[a-f0-9]{32}$" -or
        [string]$metadata.lastLogCheckpoint -notmatch "^[a-z0-9-]{1,64}$" -or
        [int]$metadata.resumeCount -lt 0 -or
        [int]$metadata.resumeCount -gt 100 -or
        $actualCountKey -cne $expectedCountKey) {
        throw "The host-only E2E run metadata does not match the current source state."
    }

    $loadedCounts = New-EmptyLogMarkerCounts
    foreach ($service in @($loadedCounts.Keys)) {
        $value = [int]$metadata.markerCounts.PSObject.Properties[$service].Value
        if ($value -lt 0 -or $value -gt 100) {
            throw "The host-only E2E log continuity state is invalid."
        }

        $loadedCounts[$service] = $value
    }

    $script:LogMarkerCounts = $loadedCounts
    $script:LastLogCheckpoint = [string]$metadata.lastLogCheckpoint
    $script:ResumeCount = [int]$metadata.resumeCount

    return [string]$metadata.runMarker
}

function Get-OptionalPropertyValue {
    param(
        [Parameter(Mandatory = $true)][object]$InputObject,
        [Parameter(Mandatory = $true)][string]$Name
    )

    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Assert-ExactStringSet {
    param(
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]]$Actual,
        [Parameter(Mandatory = $true)][AllowEmptyCollection()][string[]]$Expected,
        [Parameter(Mandatory = $true)][string]$Description
    )

    $actualSorted = @($Actual | Sort-Object)
    $expectedSorted = @($Expected | Sort-Object)
    if (($actualSorted -join "`n") -cne ($expectedSorted -join "`n")) {
        throw "The resolved Compose $Description does not match the exact proof contract."
    }
}

function Get-CanonicalPathKey {
    param([Parameter(Mandatory = $true)][string]$Path)

    $resolved = [System.IO.Path]::TrimEndingDirectorySeparator(
        [System.IO.Path]::GetFullPath($Path))
    return $IsWindows ? $resolved.ToUpperInvariant() : $resolved
}

function Get-VolumeKey {
    param(
        [Parameter(Mandatory = $true)][string]$Service,
        [Parameter(Mandatory = $true)][string]$Type,
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Target,
        [Parameter(Mandatory = $true)][bool]$ReadOnly
    )

    $sourceKey = $Type -eq "bind" ? (Get-CanonicalPathKey -Path $Source) : $Source
    return "$Service|$Type|$sourceKey|$Target|$ReadOnly"
}

function Get-ServiceSecretKey {
    param(
        [Parameter(Mandatory = $true)][string]$Service,
        [Parameter(Mandatory = $true)][string]$Source,
        [Parameter(Mandatory = $true)][string]$Target
    )

    $resolvedSecretRoot = "/run/secrets/"
    $normalizedTarget = $Target.StartsWith(
        $resolvedSecretRoot,
        [StringComparison]::Ordinal) ?
        $Target.Substring($resolvedSecretRoot.Length) : $Target
    return "$Service|$Source|$normalizedTarget"
}

function Write-AndValidateComposeConfig {
    $captureBase = Join-Path $BuildTranscriptRoot "compose-config"
    Invoke-NativeCommand `
        -FilePath "docker" `
        -Arguments ($ComposeBaseArguments + @("config", "--format", "json")) `
        -CaptureBasePath $captureBase `
        -TimeoutSeconds 120 `
        -MaximumStandardOutputBytes 2MB `
        -MaximumStandardErrorBytes 256KB | Out-Null
    $configPath = "$captureBase.stdout.log"
    Assert-SafeManagedRegularFileTarget -Path $configPath
    if (-not (Test-Path -LiteralPath $configPath -PathType Leaf)) {
        throw "Docker Compose did not produce a resolved JSON configuration."
    }

    $configInfo = Get-Item -LiteralPath $configPath -Force
    if ($configInfo.Length -le 0 -or $configInfo.Length -gt 2MB) {
        throw "The resolved Compose JSON configuration is empty or exceeds its bound."
    }

    try {
        $config = [System.IO.File]::ReadAllText($configPath) |
            ConvertFrom-Json -Depth 64
    Assert-SafeManagedRegularFileTarget -Path $configPath
    }
    catch {
        throw "The resolved Compose configuration is not valid bounded JSON."
    }

    if (-not [string]::Equals(
            [string]$config.name,
            $ComposeProjectName,
            [StringComparison]::Ordinal)) {
        throw "The resolved Compose project name is not the dedicated proof project."
    }

    Assert-ExactStringSet `
        -Actual @($config.services.PSObject.Properties.Name) `
        -Expected $ExpectedDockerServiceCoverage `
        -Description "service set"
    Assert-ExactStringSet `
        -Actual @($config.secrets.PSObject.Properties.Name) `
        -Expected $RuntimeSecretNames `
        -Description "secret set"
    Assert-ExactStringSet `
        -Actual @($config.volumes.PSObject.Properties.Name) `
        -Expected @("postgres-data") `
        -Description "named-volume set"
    $expectedNetworkNames = @(
        "app-mesh",
        "central-db",
        "client-a-db",
        "client-a-personal",
        "client-b-db",
        "personal-control",
        "upstream-control",
        "upstream-data"
    )
    Assert-ExactStringSet `
        -Actual @($config.networks.PSObject.Properties.Name) `
        -Expected $expectedNetworkNames `
        -Description "network set"
    foreach ($networkName in $expectedNetworkNames) {
        $network = $config.networks.PSObject.Properties[$networkName].Value
        if ((Get-OptionalPropertyValue -InputObject $network -Name "internal") -ne $true) {
            throw "Every resolved proof network must remain internal."
        }
    }

    foreach ($secretName in $RuntimeSecretNames) {
        $secret = $config.secrets.PSObject.Properties[$secretName].Value
        $actualPath = Get-CanonicalPathKey -Path ([string]$secret.file)
        $expectedPath = Get-CanonicalPathKey -Path (Join-Path $RuntimeSecretsRoot $secretName)
        if ($actualPath -cne $expectedPath) {
            throw "A resolved Compose secret file escaped the exact runtime-secrets root."
        }
    }

    $expectedImages = @{
        "artifact-permissions" = "busybox:1.37.0-musl"
        "central" = $AppImage
        "client-a" = $AppImage
        "client-b" = $AppImage
        "db" = "postgres:16-alpine"
        "deterministic-personal-upstream" = $UpstreamImage
        "deterministic-upstream" = $UpstreamImage
        "e2e-central" = "mcr.microsoft.com/dotnet/aspnet:10.0.10"
        "e2e-client-a" = "mcr.microsoft.com/dotnet/aspnet:10.0.10"
        "e2e-client-b" = "mcr.microsoft.com/dotnet/aspnet:10.0.10"
        "e2e-runner" = "mcr.microsoft.com/dotnet/aspnet:10.0.10"
    }
    $expectedPorts = @{
        "central" = @(5210, 8080)
        "client-a" = @(5211, 8080)
        "client-b" = @(5212, 8080)
        "db" = @(55432, 5432)
        "deterministic-personal-upstream" = @(5214, 8080)
        "deterministic-upstream" = @(5213, 8080)
    }
    $expectedServiceNetworks = @{
        "artifact-permissions" = @()
        "central" = @("app-mesh", "central-db", "upstream-data")
        "client-a" = @("app-mesh", "client-a-db", "client-a-personal")
        "client-b" = @("app-mesh", "client-b-db")
        "db" = @("central-db", "client-a-db", "client-b-db")
        "deterministic-personal-upstream" = @("client-a-personal", "personal-control")
        "deterministic-upstream" = @("upstream-control", "upstream-data")
        "e2e-central" = @("central-db")
        "e2e-client-a" = @("app-mesh", "client-a-db")
        "e2e-client-b" = @("app-mesh", "client-b-db")
        "e2e-runner" = @(
            "app-mesh",
            "central-db",
            "client-a-db",
            "client-b-db",
            "personal-control",
            "upstream-control"
        )
    }
    $toolServices = @("e2e-central", "e2e-client-a", "e2e-client-b", "e2e-runner")
    $portableIdentityServices = @(
        "central",
        "client-a",
        "client-b",
        "deterministic-personal-upstream",
        "deterministic-upstream")
    $expectedContainerIdentity = "$AppUid`:$AppGid"
    $markerPrefix = "printf 'CANDOITALL_E2E_LOG_START %s\n' '$RunMarker'"
    $expectedMarkerCommands = @{
        "central" = $markerPrefix + "`nexec dotnet CanDoItAll.Web.dll"
        "client-a" = $markerPrefix + "`nexec dotnet CanDoItAll.Web.dll"
        "client-b" = $markerPrefix + "`nexec dotnet CanDoItAll.Web.dll"
        "db" = $markerPrefix + "`nexec docker-entrypoint.sh postgres"
        "deterministic-personal-upstream" = $markerPrefix + "`nexec dotnet CanDoItAll.SharedProviders.TestUpstream.dll"
        "deterministic-upstream" = $markerPrefix + "`nexec dotnet CanDoItAll.SharedProviders.TestUpstream.dll"
    }
    foreach ($serviceProperty in $config.services.PSObject.Properties) {
        $serviceName = $serviceProperty.Name
        $service = $serviceProperty.Value
        if (-not [string]::Equals(
                [string]$service.image,
                [string]$expectedImages[$serviceName],
                [StringComparison]::Ordinal)) {
            throw "A resolved Compose service uses an unexpected image."
        }

        $privileged = Get-OptionalPropertyValue -InputObject $service -Name "privileged"
        $networkMode = Get-OptionalPropertyValue -InputObject $service -Name "network_mode"
        if ($privileged -eq $true -or
            [string]::Equals([string]$networkMode, "host", [StringComparison]::OrdinalIgnoreCase)) {
            throw "A resolved Compose service enables a forbidden privilege or host network mode."
        }

        if ($serviceName -eq "artifact-permissions") {
            if (-not [string]::Equals(
                    [string]$networkMode,
                    "none",
                    [StringComparison]::Ordinal)) {
                throw "The artifact permission service must remain network-isolated."
            }
        }

        $serviceNetworks = Get-OptionalPropertyValue -InputObject $service -Name "networks"
        $actualServiceNetworks = $null -eq $serviceNetworks ?
            @() : @($serviceNetworks.PSObject.Properties.Name)
        Assert-ExactStringSet `
            -Actual $actualServiceNetworks `
            -Expected $expectedServiceNetworks[$serviceName] `
            -Description "$serviceName network membership"

        if ($serviceName -ne "db") {
            $capDrop = @(Get-OptionalPropertyValue -InputObject $service -Name "cap_drop")
            $securityOptions = @(Get-OptionalPropertyValue -InputObject $service -Name "security_opt")
            if ((Get-OptionalPropertyValue -InputObject $service -Name "read_only") -ne $true -or
                $capDrop.Count -ne 1 -or $capDrop[0] -cne "ALL" -or
                $securityOptions.Count -ne 1 -or
                $securityOptions[0] -cne "no-new-privileges:true") {
                throw "A resolved Compose service lost its read-only or least-privilege boundary."
            }
        }

        $logging = Get-OptionalPropertyValue -InputObject $service -Name "logging"
        $loggingOptions = $null -eq $logging ? $null :
            (Get-OptionalPropertyValue -InputObject $logging -Name "options")
        if ($null -eq $logging -or
            [string](Get-OptionalPropertyValue -InputObject $logging -Name "driver") -cne "local" -or
            $null -eq $loggingOptions -or
            [string](Get-OptionalPropertyValue -InputObject $loggingOptions -Name "max-size") -cne "1m" -or
            [string](Get-OptionalPropertyValue -InputObject $loggingOptions -Name "max-file") -cne "2") {
            throw "A resolved Compose service lost the bounded complete-log retention contract."
        }

        if ($toolServices -contains $serviceName) {
            if ([string](Get-OptionalPropertyValue -InputObject $service -Name "user") -cne $expectedContainerIdentity -or
                [long](Get-OptionalPropertyValue -InputObject $service -Name "mem_limit") -ne 1GB -or
                [decimal](Get-OptionalPropertyValue -InputObject $service -Name "cpus") -ne 1.0 -or
                [int](Get-OptionalPropertyValue -InputObject $service -Name "pids_limit") -ne 256) {
                throw "A resolved E2E tool service lost its exact identity or resource bounds."
            }
        }

        if ($portableIdentityServices -contains $serviceName -and
            [string](Get-OptionalPropertyValue -InputObject $service -Name "user") -cne $expectedContainerIdentity) {
            throw "A resolved application or fixture service lost its host-portable non-root identity."
        }

        if ($expectedMarkerCommands.ContainsKey($serviceName)) {
            $environment = Get-OptionalPropertyValue -InputObject $service -Name "environment"
            $entrypoint = @(Get-OptionalPropertyValue -InputObject $service -Name "entrypoint")
            $command = @(Get-OptionalPropertyValue -InputObject $service -Name "command")
            if ($null -eq $environment -or
                [string](Get-OptionalPropertyValue -InputObject $environment -Name "E2E_LOG_CONTINUITY_MARKER") -cne $RunMarker -or
                ($entrypoint -join "`n") -cne "/bin/sh`n-ec" -or
                $command.Count -ne 1 -or
                [string]$command[0].Trim() -cne $expectedMarkerCommands[$serviceName]) {
                throw "A persistent proof service lost its pre-start log continuity marker."
            }
        }

        $resolvedPorts = Get-OptionalPropertyValue -InputObject $service -Name "ports"
        $ports = $null -eq $resolvedPorts ? @() : @($resolvedPorts)
        if ($expectedPorts.ContainsKey($serviceName)) {
            if ($ports.Count -ne 1) {
                throw "A resolved Compose service does not expose its one exact loopback port."
            }

            $expectedPort = $expectedPorts[$serviceName]
            $port = $ports[0]
            if ([string]$port.host_ip -cne "127.0.0.1" -or
                [int]$port.target -ne $expectedPort[1] -or
                [int]$port.published -ne $expectedPort[0] -or
                [string]$port.protocol -cne "tcp" -or
                [string]$port.mode -cne "ingress") {
                throw "A resolved Compose port is not the exact loopback-only proof binding."
            }
        }
        elseif ($ports.Count -ne 0) {
            throw "A resolved Compose service exposes an unexpected host port."
        }
    }

    $centralDataRoot = Join-Path $ArtifactRoot "central\data"
    $clientADataRoot = Join-Path $ArtifactRoot "client-a\data"
    $clientBDataRoot = Join-Path $ArtifactRoot "client-b\data"
    $initScript = Join-Path $RepositoryRoot "tools\SharedProviders\postgres\init-e2e-databases.sh"
    $expectedVolumeKeys = @(
        (Get-VolumeKey "artifact-permissions" "bind" $ArtifactRoot "/e2e" $false),
        (Get-VolumeKey "db" "volume" "postgres-data" "/var/lib/postgresql/data" $false),
        (Get-VolumeKey "db" "bind" $initScript "/docker-entrypoint-initdb.d/10-shared-providers-e2e.sh" $true),
        (Get-VolumeKey "central" "bind" $centralDataRoot "/data" $false),
        (Get-VolumeKey "client-a" "bind" $clientADataRoot "/data" $false),
        (Get-VolumeKey "client-b" "bind" $clientBDataRoot "/data" $false),
        (Get-VolumeKey "e2e-central" "bind" $ToolStateRoot "/e2e" $false),
        (Get-VolumeKey "e2e-central" "bind" $centralDataRoot "/instance" $false),
        (Get-VolumeKey "e2e-central" "bind" $ToolPublishRoot "/runner" $true),
        (Get-VolumeKey "e2e-client-a" "bind" $ToolStateRoot "/e2e" $false),
        (Get-VolumeKey "e2e-client-a" "bind" $clientADataRoot "/instance" $false),
        (Get-VolumeKey "e2e-client-a" "bind" $ToolPublishRoot "/runner" $true),
        (Get-VolumeKey "e2e-client-b" "bind" $ToolStateRoot "/e2e" $false),
        (Get-VolumeKey "e2e-client-b" "bind" $clientBDataRoot "/instance" $false),
        (Get-VolumeKey "e2e-client-b" "bind" $ToolPublishRoot "/runner" $true),
        (Get-VolumeKey "e2e-runner" "bind" $ToolStateRoot "/e2e" $false),
        (Get-VolumeKey "e2e-runner" "bind" $ToolPublishRoot "/runner" $true)
    )
    $actualVolumeKeys = [System.Collections.Generic.List[string]]::new()
    foreach ($serviceProperty in $config.services.PSObject.Properties) {
        $resolvedVolumes = Get-OptionalPropertyValue -InputObject $serviceProperty.Value -Name "volumes"
        $volumes = $null -eq $resolvedVolumes ? @() : @($resolvedVolumes)
        foreach ($volume in $volumes) {
            $source = [string]$volume.source
            $target = [string]$volume.target
            if ($source.Contains("docker.sock", [StringComparison]::OrdinalIgnoreCase) -or
                $target.Contains("docker.sock", [StringComparison]::OrdinalIgnoreCase)) {
                throw "The resolved Compose configuration mounts a forbidden Docker socket."
            }

            $type = [string]$volume.type
            if ($type -notin @("bind", "volume")) {
                throw "The resolved Compose configuration contains an unsupported mount type."
            }

            if ($type -eq "bind") {
                $bind = Get-OptionalPropertyValue -InputObject $volume -Name "bind"
                if ($null -eq $bind -or
                    (Get-OptionalPropertyValue -InputObject $bind -Name "create_host_path") -ne $false) {
                    throw "A resolved bind mount may create an unvalidated host path."
                }
            }

            $actualVolumeKeys.Add((Get-VolumeKey `
                $serviceProperty.Name `
                $type `
                $source `
                $target `
                ((Get-OptionalPropertyValue -InputObject $volume -Name "read_only") -eq $true)))
        }
    }
    Assert-ExactStringSet `
        -Actual $actualVolumeKeys.ToArray() `
        -Expected $expectedVolumeKeys `
        -Description "host-mount mapping"

    $expectedServiceSecretKeys = @(
        (Get-ServiceSecretKey "db" "db-admin-password" "db-admin-password"),
        (Get-ServiceSecretKey "db" "db-central-password" "db-central-password"),
        (Get-ServiceSecretKey "db" "db-client-a-password" "db-client-a-password"),
        (Get-ServiceSecretKey "db" "db-client-b-password" "db-client-b-password"),
        (Get-ServiceSecretKey "deterministic-upstream" "upstream-data-token" "upstream-data-token"),
        (Get-ServiceSecretKey "deterministic-upstream" "upstream-control-token" "upstream-control-token"),
        (Get-ServiceSecretKey "deterministic-personal-upstream" "personal-upstream-data-token" "personal-upstream-data-token"),
        (Get-ServiceSecretKey "deterministic-personal-upstream" "personal-upstream-control-token" "personal-upstream-control-token"),
        (Get-ServiceSecretKey "central" "db-central-password" "db-password"),
        (Get-ServiceSecretKey "central" "api-central-signing-key" "api-signing-key"),
        (Get-ServiceSecretKey "client-a" "db-client-a-password" "db-password"),
        (Get-ServiceSecretKey "client-a" "api-client-a-signing-key" "api-signing-key"),
        (Get-ServiceSecretKey "client-b" "db-client-b-password" "db-password"),
        (Get-ServiceSecretKey "client-b" "api-client-b-signing-key" "api-signing-key"),
        (Get-ServiceSecretKey "e2e-central" "db-central-connection-string" "db-connection-string"),
        (Get-ServiceSecretKey "e2e-central" "api-central-signing-key" "api-signing-key"),
        (Get-ServiceSecretKey "e2e-central" "upstream-data-token" "upstream-data-token"),
        (Get-ServiceSecretKey "e2e-client-a" "db-client-a-connection-string" "db-connection-string"),
        (Get-ServiceSecretKey "e2e-client-a" "api-client-a-signing-key" "api-signing-key"),
        (Get-ServiceSecretKey "e2e-client-a" "personal-upstream-data-token" "personal-upstream-data-token"),
        (Get-ServiceSecretKey "e2e-client-b" "db-client-b-connection-string" "db-connection-string"),
        (Get-ServiceSecretKey "e2e-client-b" "api-client-b-signing-key" "api-signing-key"),
        (Get-ServiceSecretKey "e2e-runner" "db-central-connection-string" "central-db-connection-string"),
        (Get-ServiceSecretKey "e2e-runner" "db-client-a-connection-string" "client-a-db-connection-string"),
        (Get-ServiceSecretKey "e2e-runner" "db-client-b-connection-string" "client-b-db-connection-string"),
        (Get-ServiceSecretKey "e2e-runner" "upstream-control-token" "upstream-control-token"),
        (Get-ServiceSecretKey "e2e-runner" "personal-upstream-control-token" "personal-upstream-control-token")
    )
    $actualServiceSecretKeys = [System.Collections.Generic.List[string]]::new()
    foreach ($serviceProperty in $config.services.PSObject.Properties) {
        $resolvedSecrets = Get-OptionalPropertyValue -InputObject $serviceProperty.Value -Name "secrets"
        $serviceSecrets = $null -eq $resolvedSecrets ? @() : @($resolvedSecrets)
        foreach ($secret in $serviceSecrets) {
            $actualServiceSecretKeys.Add((Get-ServiceSecretKey `
                $serviceProperty.Name `
                ([string]$secret.source) `
                ([string]$secret.target)))
        }
    }
    Assert-ExactStringSet `
        -Actual $actualServiceSecretKeys.ToArray() `
        -Expected $expectedServiceSecretKeys `
        -Description "service-secret mapping"
}

function Assert-DockerContainerNameAbsent {
    param(
        [Parameter(Mandatory = $true)][string]$ContainerName,
        [Parameter(Mandatory = $true)][string]$EvidencePath
    )

    $containerNames = Invoke-NativeText `
        -FilePath "docker" `
        -Arguments @(
            "container",
            "ls",
            "--all",
            "--filter",
            "name=$ContainerName",
            "--format",
            "{{.Names}}")
    $containerStillExists = @($containerNames -split "`r?`n" | Where-Object {
        [string]::Equals($_, $ContainerName, [StringComparison]::Ordinal)
    }).Count -ne 0
    if ($containerStillExists) {
        throw "The failed one-off container still exists after bounded cleanup."
    }

    Write-JsonFile -Path $EvidencePath -Value ([ordered]@{
        schemaVersion = 1
        containerName = $ContainerName
        absent = $true
        verifiedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    })
}

function Remove-OwnedOneOffContainer {
    param(
        [Parameter(Mandatory = $true)][string]$ContainerName,
        [Parameter(Mandatory = $true)][string]$Service,
        [Parameter(Mandatory = $true)][string]$CommandId,
        [Parameter(Mandatory = $true)][string]$CaptureBasePath
    )

    $hostCleanupRoot = Join-Path $HostCaptureRoot ("cleanup-{0:N}" -f [Guid]::NewGuid())
    Assert-HostCapturePath -Path $hostCleanupRoot
    [System.IO.Directory]::CreateDirectory($hostCleanupRoot) | Out-Null
    Assert-HostCapturePath -Path $hostCleanupRoot
    $cleanupCompleted = $false
    try {
        $inspectCapture = Join-Path $hostCleanupRoot "inspect"
        $inspectExitCode = Invoke-NativeCommand `
            -FilePath "docker" `
            -Arguments @(
                "inspect",
                "--format",
                '{{ index .Config.Labels "com.docker.compose.project" }}|{{ index .Config.Labels "com.docker.compose.service" }}|{{ index .Config.Labels "com.docker.compose.oneoff" }}|{{ index .Config.Labels "candoitall.shared-providers.e2e.command" }}',
                $ContainerName) `
            -CaptureBasePath $inspectCapture `
            -AllowedExitCodes @(0, 1) `
            -TimeoutSeconds 15 `
            -MaximumStandardOutputBytes 8KB `
            -MaximumStandardErrorBytes 32KB
        if ($inspectExitCode -eq 1) {
            Assert-DockerContainerNameAbsent `
                -ContainerName $ContainerName `
                -EvidencePath (Join-Path $hostCleanupRoot "initial-absence.json")
            $cleanupCompleted = $true
            return
        }

        $ownershipPath = "$inspectCapture.stdout.log"
        Assert-SafeManagedRegularFileTarget -Path $ownershipPath
        if (-not (Test-Path -LiteralPath $ownershipPath -PathType Leaf) -or
            (Get-Item -LiteralPath $ownershipPath).Length -gt 8KB) {
            throw "The failed one-off container has no bounded ownership evidence."
        }

        $parts = [System.IO.File]::ReadAllText($ownershipPath).Trim().Split(
            "|",
            [StringSplitOptions]::None)
        Assert-SafeManagedRegularFileTarget -Path $ownershipPath
        if ($parts.Count -ne 4 -or
            $parts[0] -cne $ComposeProjectName -or
            $parts[1] -cne $Service -or
            -not [string]::Equals($parts[2], "True", [StringComparison]::OrdinalIgnoreCase) -or
            $parts[3] -cne $CommandId) {
            throw "Refusing to remove a failed one-off container without exact proof ownership labels."
        }

        Invoke-NativeCommand `
            -FilePath "docker" `
            -Arguments @("rm", "--force", $ContainerName) `
            -CaptureBasePath (Join-Path $hostCleanupRoot "remove") `
            -TimeoutSeconds 30 `
            -MaximumStandardOutputBytes 64KB `
            -MaximumStandardErrorBytes 64KB | Out-Null
        $confirmExitCode = Invoke-NativeCommand `
            -FilePath "docker" `
            -Arguments @("inspect", $ContainerName) `
            -CaptureBasePath (Join-Path $hostCleanupRoot "confirm") `
            -AllowedExitCodes @(0, 1) `
            -TimeoutSeconds 15 `
            -MaximumStandardOutputBytes 8KB `
            -MaximumStandardErrorBytes 32KB
        if ($confirmExitCode -eq 0) {
            throw "The failed one-off container still exists after bounded removal."
        }

        Assert-DockerContainerNameAbsent `
            -ContainerName $ContainerName `
            -EvidencePath (Join-Path $hostCleanupRoot "final-absence.json")

        $cleanupCompleted = $true
    }
    finally {
        if ($cleanupCompleted) {
            $entries = @(Get-SafeTreeEntries -Root $hostCleanupRoot)
            if (@($entries | Where-Object { $_ -is [System.IO.DirectoryInfo] }).Count -ne 0) {
                throw "The host-only cleanup capture contains an unexpected directory."
            }

            foreach ($entry in $entries) {
                $destinationPath = "$CaptureBasePath.cleanup-$($entry.Name)"
                Assert-SafeManagedRegularFileTarget -Path $entry.FullName
                Assert-SafeManagedRegularFileTarget -Path $destinationPath
                [System.IO.File]::Copy($entry.FullName, $destinationPath, $false)
                Assert-SafeManagedRegularFileTarget -Path $entry.FullName
                Assert-SafeManagedRegularFileTarget -Path $destinationPath
            }

            foreach ($entry in $entries) {
                Assert-SafeManagedRegularFileTarget -Path $entry.FullName
                [System.IO.File]::Delete($entry.FullName)
                Assert-HostCapturePath -Path $entry.FullName
            }

            Assert-HostCapturePath -Path $hostCleanupRoot
            [System.IO.Directory]::Delete($hostCleanupRoot, $false)
        }
    }
}

function Assert-NoComposeOneOffContainers {
    $output = Invoke-NativeText `
        -FilePath "docker" `
        -Arguments @(
            "ps",
            "--all",
            "--filter",
            "label=com.docker.compose.project=$ComposeProjectName",
            "--format",
            '{{.ID}}|{{.Label "com.docker.compose.oneoff"}}')
    foreach ($line in @($output -split "`r?`n" | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })) {
        $parts = $line.Split("|", [StringSplitOptions]::None)
        if ($parts.Count -ne 2 -or [string]::IsNullOrWhiteSpace($parts[0])) {
            throw "The Compose container inventory returned an invalid ownership record."
        }

        if ([string]::Equals($parts[1], "True", [StringComparison]::OrdinalIgnoreCase)) {
            throw "A prior Compose one-off container still exists; refusing host writes into its mounted state."
        }
    }
}

function Assert-ArtifactPermissionServiceStopped {
    $containerId = Invoke-NativeText `
        -FilePath "docker" `
        -Arguments ($ComposeBaseArguments + @("ps", "--all", "--quiet", "artifact-permissions"))
    if ([string]::IsNullOrWhiteSpace($containerId)) {
        throw "The artifact-permission service container is missing."
    }

    $state = Invoke-NativeText `
        -FilePath "docker" `
        -Arguments @("inspect", "--format", "{{.State.Status}}|{{.State.ExitCode}}", $containerId)
    if ($state -cne "exited|0") {
        throw "The artifact-permission service is not safely stopped."
    }
}

function Invoke-ComposeCommand {
    param(
        [Parameter(Mandatory = $true)][string]$CommandId,
        [Parameter(Mandatory = $true)][string[]]$Arguments,
        [int]$TimeoutSeconds = 1200
    )

    $script:CommandSequence++
    $safeCommandId = $CommandId -replace "[^a-zA-Z0-9_-]", "-"
    $captureBase = Join-Path $HostCommandCaptureRoot ("{0:D3}-{1}" -f $script:CommandSequence, $safeCommandId)
    $composeArguments = $Arguments
    $oneOffContainerName = $null
    $oneOffService = $null
    if ($Arguments.Count -gt 0 -and $Arguments[0] -eq "run") {
        $oneOffService = @($Arguments |
            Select-Object -Skip 1 |
            Where-Object { $_ -notin @("--rm", "--no-deps") })[0]
        if ([string]::IsNullOrWhiteSpace($oneOffService)) {
            throw "A Compose one-off command has no exact service identity."
        }

        $runSuffix = [Guid]::NewGuid().ToString("N").Substring(0, 12)
        $oneOffContainerName = "{0}-run-{1:D3}-{2}-{3}" -f `
            $ComposeProjectName,
            $script:CommandSequence,
            $safeCommandId,
            $runSuffix
        $tail = if ($Arguments.Count -gt 1) {
            @($Arguments[1..($Arguments.Count - 1)])
        }
        else {
            @()
        }
        $composeArguments = @(
            "run",
            "--name",
            $oneOffContainerName,
            "--label",
            "candoitall.shared-providers.e2e.command=$safeCommandId"
        ) + $tail
    }

    try {
        $exitCode = Invoke-NativeCommand `
            -FilePath "docker" `
            -Arguments ($ComposeBaseArguments + $composeArguments) `
            -CaptureBasePath $captureBase `
            -TimeoutSeconds $TimeoutSeconds
    }
    catch {
        $commandError = $_
        if ($null -ne $oneOffContainerName) {
            try {
                Remove-OwnedOneOffContainer `
                    -ContainerName $oneOffContainerName `
                    -Service $oneOffService `
                    -CommandId $safeCommandId `
                    -CaptureBasePath $captureBase
            }
            catch {
                throw "A Compose command failed and its exact one-off container could not be removed within the cleanup deadline."
            }
        }

        Import-HostCommandCaptures
        throw $commandError
    }

    Write-JsonFile -Path "$captureBase.result.json" -Value ([ordered]@{
        schemaVersion = 1
        commandId = $CommandId
        exitCode = $exitCode
        completedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    })
    Import-HostCommandCaptures
}

function Invoke-ToolService {
    param(
        [Parameter(Mandatory = $true)][string]$Service,
        [Parameter(Mandatory = $true)][string]$Command,
        [switch]$NoDependencies
    )

    $arguments = @("run", "--rm")
    if ($NoDependencies) {
        $arguments += "--no-deps"
    }

    $arguments += @($Service, $Command)
    Invoke-ComposeCommand -CommandId "$Service-$Command" -Arguments $arguments
}

function Invoke-ScenarioPhase {
    param(
        [Parameter(Mandatory = $true)][string]$Phase,
        [switch]$NoDependencies
    )

    $arguments = @("run", "--rm")
    if ($NoDependencies) {
        $arguments += "--no-deps"
    }

    $arguments += @("e2e-runner", "run-scenarios", "--phase", $Phase)
    Invoke-ComposeCommand `
        -CommandId "e2e-runner-$Phase" `
        -Arguments $arguments
}

function Test-PhaseEnabled {
    param([Parameter(Mandatory = $true)][string]$Phase)

    return [Array]::IndexOf($PhaseOrder, $Phase) -ge [Array]::IndexOf($PhaseOrder, $StartAt)
}

function Assert-NoLinkOrDevice {
    param([Parameter(Mandatory = $true)][string]$Path)

    $fileEntry = [System.IO.FileInfo]::new([System.IO.Path]::GetFullPath($Path))
    $directoryEntry = [System.IO.DirectoryInfo]::new($fileEntry.FullName)
    if ($null -ne $fileEntry.LinkTarget -or $null -ne $directoryEntry.LinkTarget) {
        throw "A managed E2E path contains a symbolic link or junction."
    }

    if (-not $fileEntry.Exists -and -not $directoryEntry.Exists) {
        return
    }

    $attributes = [System.IO.File]::GetAttributes($fileEntry.FullName)
    if (($attributes -band ([System.IO.FileAttributes]::ReparsePoint -bor [System.IO.FileAttributes]::Device)) -ne 0) {
        throw "A managed E2E path contains a link, reparse point, or device."
    }
}

function Test-IsPathWithin {
    param(
        [Parameter(Mandatory = $true)][string]$Candidate,
        [Parameter(Mandatory = $true)][string]$Root
    )

    $comparison = if ($IsWindows) {
        [StringComparison]::OrdinalIgnoreCase
    }
    else {
        [StringComparison]::Ordinal
    }
    $resolvedCandidate = [System.IO.Path]::GetFullPath($Candidate)
    $resolvedRoot = [System.IO.Path]::TrimEndingDirectorySeparator(
        [System.IO.Path]::GetFullPath($Root))
    return [string]::Equals($resolvedCandidate, $resolvedRoot, $comparison) -or
        $resolvedCandidate.StartsWith(
            $resolvedRoot + [System.IO.Path]::DirectorySeparatorChar,
            $comparison)
}

function Assert-ExistingPathComponents {
    param(
        [Parameter(Mandatory = $true)][string]$Root,
        [Parameter(Mandatory = $true)][string]$Path
    )

    $resolvedRoot = [System.IO.Path]::TrimEndingDirectorySeparator(
        [System.IO.Path]::GetFullPath($Root))
    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    if (-not (Test-IsPathWithin -Candidate $resolvedPath -Root $resolvedRoot)) {
        throw "A managed E2E path escaped its exact owned root."
    }

    Assert-NoLinkOrDevice -Path $resolvedRoot
    $relativePath = [System.IO.Path]::GetRelativePath($resolvedRoot, $resolvedPath)
    if ([string]::Equals($relativePath, ".", [StringComparison]::Ordinal)) {
        return
    }

    $current = $resolvedRoot
    foreach ($segment in $relativePath.Split(
        [char[]]@(
            [System.IO.Path]::DirectorySeparatorChar,
            [System.IO.Path]::AltDirectorySeparatorChar),
        [StringSplitOptions]::RemoveEmptyEntries)) {
        $current = Join-Path $current $segment
        Assert-NoLinkOrDevice -Path $current
    }
}

function Assert-OwnedArtifactPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    Assert-OwnedArtifactRoot
    Assert-ExistingPathComponents -Root $ArtifactRoot -Path $Path
}

function Assert-OwnedRegularFileTarget {
    param([Parameter(Mandatory = $true)][string]$Path)

    Assert-OwnedArtifactPath -Path $Path
    Assert-RegularFileTarget -Path $Path
}

function Assert-SafeManagedRegularFileTarget {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (Test-IsPathWithin -Candidate $Path -Root $HostCaptureRoot) {
        Assert-HostCapturePath -Path $Path
    }
    elseif (Test-IsPathWithin -Candidate $Path -Root $ArtifactRoot) {
        Assert-OwnedArtifactPath -Path $Path
    }
    elseif (Test-IsPathWithin -Candidate $Path -Root $BuildTranscriptRoot) {
        Assert-BuildTranscriptPath -Path $Path
    }
    else {
        throw "A managed E2E file target is outside the exact artifact, build-transcript, or host-capture roots."
    }

    Assert-RegularFileTarget -Path $Path
}

function Assert-RegularFileTarget {
    param([Parameter(Mandatory = $true)][string]$Path)

    Assert-NoLinkOrDevice -Path $Path
    $fileEntry = [System.IO.FileInfo]::new([System.IO.Path]::GetFullPath($Path))
    $directoryEntry = [System.IO.DirectoryInfo]::new($fileEntry.FullName)
    if (-not $fileEntry.Exists -and -not $directoryEntry.Exists) {
        return
    }

    $attributes = [System.IO.File]::GetAttributes($fileEntry.FullName)
    $invalidAttributes = [System.IO.FileAttributes]::Directory -bor
        [System.IO.FileAttributes]::ReparsePoint -bor
        [System.IO.FileAttributes]::Device
    if (($attributes -band $invalidAttributes) -ne 0) {
        throw "A managed E2E file target is not an exact regular file."
    }
}

function Assert-BuildTranscriptPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    $artifactParent = [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot ".artifacts"))
    $buildRootParent = [System.IO.Path]::GetFullPath((Split-Path -Parent $BuildTranscriptRoot))
    $buildRootName = Split-Path -Leaf $BuildTranscriptRoot
    if (-not [string]::Equals(
            $artifactParent,
            $buildRootParent,
            [StringComparison]::OrdinalIgnoreCase) -or
        -not $buildRootName.StartsWith(
            "shared-providers-e2e-build-",
            [StringComparison]::Ordinal)) {
        throw "The temporary build transcript root is not an exact repository-owned sibling."
    }

    Assert-NoLinkOrDevice -Path $RepositoryRoot
    Assert-NoLinkOrDevice -Path $artifactParent
    Assert-ExistingPathComponents -Root $BuildTranscriptRoot -Path $Path
}

function Assert-HostCapturePath {
    param([Parameter(Mandatory = $true)][string]$Path)

    $expectedRoot = [System.IO.Path]::GetFullPath((Join-Path $ArtifactRoot "host-only-captures"))
    if (-not [string]::Equals(
            $expectedRoot,
            $HostCaptureRoot,
            [StringComparison]::OrdinalIgnoreCase)) {
        throw "The host-capture root is not the exact marked-artifact child."
    }

    Assert-OwnedArtifactRoot
    Assert-ExistingPathComponents -Root $HostCaptureRoot -Path $Path
}

function Assert-SafeManagedDirectory {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (Test-IsPathWithin -Candidate $Path -Root $HostCaptureRoot) {
        Assert-HostCapturePath -Path $Path
        return
    }

    if (Test-IsPathWithin -Candidate $Path -Root $ArtifactRoot) {
        Assert-OwnedArtifactPath -Path $Path
        return
    }

    if (Test-IsPathWithin -Candidate $Path -Root $BuildTranscriptRoot) {
        Assert-BuildTranscriptPath -Path $Path
        return
    }

    throw "A managed E2E write target is outside the exact artifact, build-transcript, or host-capture roots."
}

function Get-SafeTreeEntries {
    param([Parameter(Mandatory = $true)][string]$Root)

    Assert-NoLinkOrDevice -Path $Root
    $pending = [System.Collections.Generic.Stack[System.IO.DirectoryInfo]]::new()
    $entries = [System.Collections.Generic.List[System.IO.FileSystemInfo]]::new()
    $pending.Push([System.IO.DirectoryInfo]::new($Root))
    while ($pending.Count -gt 0) {
        $directory = $pending.Pop()
        foreach ($entry in $directory.EnumerateFileSystemInfos()) {
            if (($entry.Attributes -band ([System.IO.FileAttributes]::ReparsePoint -bor [System.IO.FileAttributes]::Device)) -ne 0) {
                throw "A managed E2E tree contains a link, reparse point, or device."
            }

            $entries.Add($entry)
            if ($entries.Count -gt 100000) {
                throw "A managed E2E tree exceeded its bounded entry limit."
            }

            if ($entry -is [System.IO.DirectoryInfo]) {
                $pending.Push($entry)
            }
        }
    }

    return $entries.ToArray()
}

function Assert-RepositoryLayout {
    $requiredPaths = @(
        (Join-Path $RepositoryRoot ".git"),
        $ComposeFile,
        $ComposeEnvironmentFile,
        $ToolProject,
        $AppDockerfile,
        $UpstreamDockerfile,
        $ComponentsRoot,
        $FileToolsRoot
    )
    foreach ($path in $requiredPaths) {
        if (-not (Test-Path -LiteralPath $path)) {
            throw "The Shared Providers E2E repository layout is incomplete."
        }
    }

    $expectedArtifactRoot = [System.IO.Path]::GetFullPath((Join-Path $RepositoryRoot ".artifacts\shared-providers-e2e"))
    if (-not [string]::Equals($expectedArtifactRoot, $ArtifactRoot, [StringComparison]::OrdinalIgnoreCase)) {
        throw "The E2E artifact root is not the exact repository-owned path."
    }

    Assert-NoLinkOrDevice -Path $RepositoryRoot
    Assert-NoLinkOrDevice -Path (Join-Path $RepositoryRoot ".artifacts")
    Assert-NoLinkOrDevice -Path $ArtifactRoot
}

function Assert-OwnedArtifactRoot {
    if (-not (Test-Path -LiteralPath $ArtifactRoot -PathType Container)) {
        throw "The exact E2E artifact root does not exist."
    }

    Assert-NoLinkOrDevice -Path $ArtifactRoot
    $markerPath = Join-Path $ArtifactRoot $RootMarkerName
    if (-not (Test-Path -LiteralPath $markerPath -PathType Leaf)) {
        throw "The exact E2E artifact root has no ownership marker."
    }

    $markerInfo = Get-Item -LiteralPath $markerPath -Force
    if (($markerInfo.Attributes -band ([System.IO.FileAttributes]::ReparsePoint -bor [System.IO.FileAttributes]::Device)) -ne 0 -or
        $markerInfo.Length -le 0 -or
        $markerInfo.Length -gt 256 -or
        -not [string]::Equals([System.IO.File]::ReadAllText($markerPath).Trim(), $RootMarkerValue, [StringComparison]::Ordinal)) {
        throw "The exact E2E artifact ownership marker is invalid."
    }
}

function Reset-OwnedChildDirectory {
    param([Parameter(Mandatory = $true)][string]$Path)

    $resolvedPath = [System.IO.Path]::GetFullPath($Path)
    if ([string]::Equals($resolvedPath, $ArtifactRoot, [StringComparison]::OrdinalIgnoreCase) -or
        -not (Test-IsPathWithin -Candidate $resolvedPath -Root $ArtifactRoot)) {
        throw "A managed cleanup target escaped the exact E2E artifact root."
    }

    Assert-OwnedArtifactPath -Path $resolvedPath
    if (Test-Path -LiteralPath $resolvedPath) {
        [void](Get-SafeTreeEntries -Root $resolvedPath)
        Assert-OwnedArtifactPath -Path $resolvedPath
        Remove-Item -LiteralPath $resolvedPath -Recurse -Force
    }

    Assert-OwnedArtifactPath -Path (Split-Path -Parent $resolvedPath)
    [System.IO.Directory]::CreateDirectory($resolvedPath) | Out-Null
    Assert-OwnedArtifactPath -Path $resolvedPath
}

function Assert-PortAvailable {
    param([Parameter(Mandatory = $true)][int]$Port)

    $listener = [System.Net.Sockets.TcpListener]::new([System.Net.IPAddress]::Loopback, $Port)
    try {
        $listener.Start()
    }
    catch {
        throw "Required loopback port $Port is already in use."
    }
    finally {
        $listener.Stop()
    }
}

function Read-BoundedPrivateValue {
    param([Parameter(Mandatory = $true)][string]$Path)

    Assert-OwnedRegularFileTarget -Path $Path
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "A required private E2E input is missing."
    }

    $info = Get-Item -LiteralPath $Path -Force
    if (($info.Attributes -band ([System.IO.FileAttributes]::ReparsePoint -bor [System.IO.FileAttributes]::Device)) -ne 0 -or
        $info.Length -le 0 -or
        $info.Length -gt 16KB) {
        throw "A required private E2E input is not a bounded regular file."
    }

    $value = [System.IO.File]::ReadAllText($Path).Trim()
    Assert-OwnedRegularFileTarget -Path $Path
    if ([string]::IsNullOrWhiteSpace($value)) {
        throw "A required private E2E input is empty."
    }

    return $value
}

function Get-ScanCandidates {
    $runtimeValues = foreach ($name in $RuntimeSecretNames) {
        Read-BoundedPrivateValue -Path (Join-Path $RuntimeSecretsRoot $name)
    }
    $credentialValues = foreach ($name in $CredentialNames) {
        Read-BoundedPrivateValue -Path (Join-Path $CredentialsRoot $name)
    }
    $allValues = @($runtimeValues) + @($credentialValues) + @($ContentCanary)
    $unique = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    foreach ($value in $allValues) {
        if (-not $unique.Add($value)) {
            throw "The E2E scan candidate set is not unique."
        }
    }

    if ($runtimeValues.Count -ne 14 -or $credentialValues.Count -ne 5 -or $allValues.Count -ne 20) {
        throw "The E2E scan candidate set is incomplete."
    }

    return [pscustomobject]@{
        RuntimeValues = @($runtimeValues)
        CredentialValues = @($credentialValues)
        AllValues = @($allValues)
    }
}

function Test-FileContainsCandidate {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string[]]$Candidates
    )

    Assert-OwnedRegularFileTarget -Path $Path
    $text = [System.IO.File]::ReadAllText($Path)
    Assert-OwnedRegularFileTarget -Path $Path
    foreach ($candidate in $Candidates) {
        if ($text.Contains($candidate, [StringComparison]::Ordinal)) {
            return $true
        }
    }

    return $false
}

function Get-TotalFileBytes {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [System.IO.FileInfo[]]$Files
    )

    $totalBytes = 0L
    foreach ($file in $Files) {
        $totalBytes += $file.Length
    }

    return $totalBytes
}

function Write-CollectionManifest {
    param(
        [Parameter(Mandatory = $true)][string]$SourceRoot,
        [Parameter(Mandatory = $true)][string]$SourceId,
        [string[]]$ServiceCoverage
    )

    $manifestPath = Join-Path $SourceRoot "collection.json"
    $payloadFiles = @(Get-ChildItem -LiteralPath $SourceRoot -File -Force -Recurse |
        Where-Object { -not [string]::Equals($_.FullName, $manifestPath, [StringComparison]::OrdinalIgnoreCase) })
    $payloadBytes = Get-TotalFileBytes -Files $payloadFiles

    if ($payloadFiles.Count -le 0 -or $payloadBytes -le 0) {
        throw "A required E2E log source produced no non-marker payload."
    }

    Write-JsonFile -Path $manifestPath -Value ([ordered]@{
        schemaVersion = 1
        sourceId = $SourceId
        successful = $true
        payloadFileCount = $payloadFiles.Count
        payloadBytes = [long]$payloadBytes
        serviceCoverage = $ServiceCoverage
        collectedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    })
}

function Copy-ApplicationLogs {
    param(
        [Parameter(Mandatory = $true)][string]$Role,
        [Parameter(Mandatory = $true)][string]$Service
    )

    $sourceRoot = Join-Path $ArtifactRoot "$Role\data\logs"
    $targetRoot = Join-Path $CollectedLogsRoot $Role
    Reset-OwnedChildDirectory -Path $targetRoot
    $payloadRoot = Join-Path $targetRoot "payload"
    Assert-OwnedArtifactPath -Path $payloadRoot
    [System.IO.Directory]::CreateDirectory($payloadRoot) | Out-Null

    $copiedCount = 0
    $copiedBytes = 0L
    if (Test-Path -LiteralPath $sourceRoot -PathType Container) {
        Assert-OwnedArtifactPath -Path $sourceRoot
        $entries = @(Get-SafeTreeEntries -Root $sourceRoot)

        foreach ($file in $entries | Where-Object { $_ -is [System.IO.FileInfo] }) {
            $copiedCount++
            $copiedBytes += $file.Length
            if ($copiedCount -gt 512 -or $copiedBytes -gt 8MB) {
                throw "An application log tree exceeded its bounded collection limit."
            }

            $relativePath = [System.IO.Path]::GetRelativePath($sourceRoot, $file.FullName)
            $targetPath = Join-Path (Join-Path $payloadRoot "data-logs") $relativePath
            Assert-OwnedArtifactPath -Path (Split-Path -Parent $targetPath)
            [System.IO.Directory]::CreateDirectory((Split-Path -Parent $targetPath)) | Out-Null
            Assert-OwnedRegularFileTarget -Path $file.FullName
            Assert-OwnedRegularFileTarget -Path $targetPath
            [System.IO.File]::Copy($file.FullName, $targetPath, $true)
            Assert-OwnedRegularFileTarget -Path $file.FullName
            Assert-OwnedRegularFileTarget -Path $targetPath
        }
    }

    $containerRoot = Join-Path $payloadRoot "container"
    Assert-OwnedArtifactPath -Path $containerRoot
    [System.IO.Directory]::CreateDirectory($containerRoot) | Out-Null
    foreach ($suffix in @("stdout.log", "stderr.log")) {
        $containerLog = Join-Path $DockerServiceLogsRoot "$Service.$suffix"
        if (Test-Path -LiteralPath $containerLog -PathType Leaf) {
            $targetPath = Join-Path $containerRoot "$Service.$suffix"
            Assert-OwnedRegularFileTarget -Path $containerLog
            Assert-OwnedRegularFileTarget -Path $targetPath
            [System.IO.File]::Copy(
                $containerLog,
                $targetPath,
                $true)
            Assert-OwnedRegularFileTarget -Path $containerLog
            Assert-OwnedRegularFileTarget -Path $targetPath
        }
    }

    Write-CollectionManifest -SourceRoot $targetRoot -SourceId $Role -ServiceCoverage $null
}

function Collect-DockerServiceLogs {
    Reset-OwnedChildDirectory -Path $DockerServiceLogsRoot
    foreach ($service in $PersistentDockerServices) {
        Invoke-NativeCommand `
            -FilePath "docker" `
            -Arguments ($ComposeBaseArguments + @("logs", "--no-color", "--timestamps", $service)) `
            -CaptureBasePath (Join-Path $DockerServiceLogsRoot $service) | Out-Null
    }
}

function Get-CurrentLogMarkerCounts {
    $marker = "CANDOITALL_E2E_LOG_START $RunMarker"
    $counts = New-EmptyLogMarkerCounts
    foreach ($service in @($counts.Keys)) {
        $content = [System.Text.StringBuilder]::new()
        foreach ($suffix in @("stdout.log", "stderr.log")) {
            $path = Join-Path $DockerServiceLogsRoot "$service.$suffix"
            Assert-OwnedRegularFileTarget -Path $path
            if (Test-Path -LiteralPath $path -PathType Leaf) {
                [void]$content.Append([System.IO.File]::ReadAllText($path))
                Assert-OwnedRegularFileTarget -Path $path
            }
        }

        $counts[$service] = [regex]::Matches(
            $content.ToString(),
            [regex]::Escape($marker),
            [System.Text.RegularExpressions.RegexOptions]::CultureInvariant).Count
    }

    return $counts
}

function Invoke-LogContinuityCheckpoint {
    param(
        [Parameter(Mandatory = $true)][ValidatePattern("^[a-z0-9-]{1,64}$")][string]$Checkpoint,
        [Parameter(Mandatory = $true)][hashtable]$ExpectedIncrements
    )

    foreach ($service in $ExpectedIncrements.Keys) {
        if (-not $LogMarkerCounts.Contains($service)) {
            throw "A log continuity checkpoint contains an unknown service."
        }

        $increment = [int]$ExpectedIncrements[$service]
        if ($increment -lt 0 -or $increment -gt 1) {
            throw "A log continuity checkpoint contains an invalid restart increment."
        }
    }

    Collect-DockerServiceLogs
    $actualCounts = Get-CurrentLogMarkerCounts
    $coverage = [System.Collections.Generic.List[object]]::new()
    foreach ($service in @($LogMarkerCounts.Keys)) {
        $increment = $ExpectedIncrements.ContainsKey($service) ?
            [int]$ExpectedIncrements[$service] : 0
        $expected = [int]$LogMarkerCounts[$service] + $increment
        $actual = [int]$actualCounts[$service]
        if ($actual -ne $expected) {
            throw "A persistent service log lost lifecycle continuity or has an unexpected restart count."
        }

        $LogMarkerCounts[$service] = $actual
        $coverage.Add([ordered]@{
            service = $service
            previousMarkerCount = $expected - $increment
            expectedIncrement = $increment
            actualMarkerCount = $actual
        })
    }

    $script:LastLogCheckpoint = $Checkpoint
    Write-HostRunMetadata -RevalidateSource $false
    Write-JsonFile -Path (Join-Path $CollectedLogsRoot "host-log-continuity-checkpoint.json") -Value ([ordered]@{
        schemaVersion = 1
        clean = $true
        checkpoint = $Checkpoint
        resumeCount = $ResumeCount
        serviceCoverage = @($coverage)
    })
}

function Collect-Logs {
    Collect-DockerServiceLogs

    $pausedServices = [System.Collections.Generic.List[string]]::new()
    try {
        foreach ($service in @("central", "client-a", "client-b")) {
            $pausedServices.Add($service)
            Invoke-ComposeCommand `
                -CommandId "pause-$service-for-log-copy" `
                -Arguments @("pause", $service) `
                -TimeoutSeconds 60
        }

        Copy-ApplicationLogs -Role "central" -Service "central"
        Copy-ApplicationLogs -Role "client-a" -Service "client-a"
        Copy-ApplicationLogs -Role "client-b" -Service "client-b"
    }
    finally {
        $unpauseFailures = [System.Collections.Generic.List[string]]::new()
        foreach ($service in @($pausedServices.ToArray() | Select-Object -Reverse)) {
            try {
                Invoke-ComposeCommand `
                    -CommandId "unpause-$service-after-log-copy" `
                    -Arguments @("unpause", $service) `
                    -TimeoutSeconds 60
            }
            catch {
                $unpauseFailures.Add($service)
            }
        }

        if ($unpauseFailures.Count -ne 0) {
            throw "One or more paused app services could not be unpaused within the bounded cleanup deadline."
        }
    }

    $dockerPayload = @(Get-ChildItem -LiteralPath $DockerLogsRoot -File -Force -Recurse |
        Where-Object { $_.Name -ne "collection.json" })
    $dockerBytes = Get-TotalFileBytes -Files $dockerPayload

    if ($dockerPayload.Count -le 0 -or $dockerBytes -le 0 -or $dockerBytes -gt $MaximumCollectedLogBytes) {
        throw "The Docker E2E log collection is empty or exceeded its bounded limit."
    }

    Write-CollectionManifest `
        -SourceRoot $DockerLogsRoot `
        -SourceId "docker" `
        -ServiceCoverage $ExpectedDockerServiceCoverage
}

function Write-LogContinuityScan {
    $actualCounts = Get-CurrentLogMarkerCounts
    $coverage = [System.Collections.Generic.List[object]]::new()
    $markerCount = 0
    foreach ($service in $LogMarkerCounts.Keys) {
        $expected = [int]$LogMarkerCounts[$service]
        $actual = [int]$actualCounts[$service]
        if ($actual -ne $expected) {
            throw "A persistent service log lost continuity after its last durable checkpoint."
        }

        $markerCount += $actual
        $coverage.Add([ordered]@{
            service = $service
            expectedMarkerCount = $expected
            actualMarkerCount = $actual
        })
    }

    Write-JsonFile -Path (Join-Path $CollectedLogsRoot "host-log-continuity-scan.json") -Value ([ordered]@{
        schemaVersion = 1
        clean = $true
        freshLifecycle = $ResumeCount -eq 0
        resumeCount = $ResumeCount
        lastCheckpoint = $LastLogCheckpoint
        markerCount = $markerCount
        serviceCoverage = @($coverage)
    })
}

function Write-DatabaseScan {
    $candidates = Get-ScanCandidates
    $databases = @(
        [pscustomobject]@{ Role = "central"; Name = "candoitall_e2e_central" },
        [pscustomobject]@{ Role = "client-a"; Name = "candoitall_e2e_client_a" },
        [pscustomobject]@{ Role = "client-b"; Name = "candoitall_e2e_client_b" }
    )
    $coverage = [System.Collections.Generic.List[object]]::new()
    $clean = $true
    $missingDatabaseCount = 0
    $totalBytes = 0L

    Reset-OwnedChildDirectory -Path $HostScanTemporaryRoot
    try {
        foreach ($database in $databases) {
            $dumpPath = Join-Path $HostScanTemporaryRoot "database-$($database.Role).tmp"
            $errorPath = "$dumpPath.stderr"
            foreach ($path in @($dumpPath, $errorPath)) {
                Assert-OwnedRegularFileTarget -Path $path
                if (Test-Path -LiteralPath $path) {
                    Remove-Item -LiteralPath $path -Force
                }
            }

            $successful = $false
            $scannedBytes = 0L
            try {
                Assert-OwnedRegularFileTarget -Path $dumpPath
                Assert-OwnedRegularFileTarget -Path $errorPath
                $result = [CanDoItAll.SharedProviders.E2E.BoundedProcessRunner]::Run(
                    "docker",
                    ($ComposeBaseArguments + @(
                        "exec",
                        "-T",
                        "db",
                        "pg_dump",
                        "--username",
                        "candoitall_e2e_admin",
                        "--dbname",
                        $database.Name,
                        "--format",
                        "plain",
                        "--data-only",
                        "--no-owner",
                        "--no-privileges")),
                    $RepositoryRoot,
                    $dumpPath,
                    $errorPath,
                    300,
                    $MaximumDatabaseDumpBytes,
                    1MB,
                    $ChildEnvironmentKeysToRemove,
                    $ChildEnvironmentAssignments)
                Assert-OwnedRegularFileTarget -Path $dumpPath
                Assert-OwnedRegularFileTarget -Path $errorPath
                if (-not $result.TimedOut -and
                    -not $result.OutputLimitExceeded -and
                    $result.ExitCode -eq 0 -and
                    (Test-Path -LiteralPath $dumpPath -PathType Leaf)) {
                    $dumpInfo = Get-Item -LiteralPath $dumpPath -Force
                    if ($dumpInfo.Length -gt 0 -and
                        $dumpInfo.Length -le $MaximumDatabaseDumpBytes) {
                        $successful = $true
                        $scannedBytes = $dumpInfo.Length
                        $totalBytes += $scannedBytes
                        if (Test-FileContainsCandidate -Path $dumpPath -Candidates $candidates.AllValues) {
                            $clean = $false
                        }
                    }
                }

                if (-not $successful) {
                    $missingDatabaseCount++
                    $clean = $false
                }

                $coverage.Add([ordered]@{
                    role = $database.Role
                    successful = $successful
                    scannedBytes = $scannedBytes
                })
            }
            finally {
                foreach ($path in @($dumpPath, $errorPath)) {
                    Assert-OwnedRegularFileTarget -Path $path
                    if (Test-Path -LiteralPath $path) {
                        Remove-Item -LiteralPath $path -Force
                    }
                }
            }
        }
    }
    finally {
        Assert-OwnedArtifactPath -Path $HostScanTemporaryRoot
        if (-not (Test-Path -LiteralPath $HostScanTemporaryRoot -PathType Container)) {
            throw "The host-only database scan directory is missing or invalid."
        }

        $remainingEntries = @(Get-SafeTreeEntries -Root $HostScanTemporaryRoot)
        if ($remainingEntries.Count -ne 0) {
            throw "The host-only database scan directory is not empty."
        }

        Assert-OwnedArtifactPath -Path $HostScanTemporaryRoot
        [System.IO.Directory]::Delete($HostScanTemporaryRoot, $false)
    }

    Write-JsonFile -Path (Join-Path $CollectedLogsRoot "host-database-scan.json") -Value ([ordered]@{
        schemaVersion = 1
        clean = $clean
        databaseCount = 3
        missingDatabaseCount = $missingDatabaseCount
        runtimeSecretCount = 14
        generatedCredentialCount = 5
        contentCanaryCount = 1
        candidateCount = 20
        scannedBytes = $totalBytes
        databaseCoverage = @($coverage)
    })
    if (-not $clean) {
        throw "The bounded E2E database scan did not pass."
    }
}

function Write-HostSecretScan {
    $candidates = Get-ScanCandidates
    $outputPath = Join-Path $CollectedLogsRoot "host-secret-scan.json"
    $roots = @($CollectedLogsRoot, $ScenarioResultsRoot, $HandoffRoot, $HostCaptureRoot)
    $files = [System.Collections.Generic.List[System.IO.FileInfo]]::new()
    foreach ($root in $roots) {
        if (-not (Test-Path -LiteralPath $root -PathType Container)) {
            continue
        }

        foreach ($entry in Get-SafeTreeEntries -Root $root) {
            if ($entry -is [System.IO.FileInfo] -and
                -not [string]::Equals($entry.FullName, $outputPath, [StringComparison]::OrdinalIgnoreCase)) {
                $files.Add($entry)
            }
        }
    }

    $uniqueFiles = @($files | Sort-Object -Property FullName -Unique)
    $scannedBytes = Get-TotalFileBytes -Files $uniqueFiles

    if ($uniqueFiles.Count -le 0 -or
        $uniqueFiles.Count -gt $MaximumScanFileCount -or
        $scannedBytes -le 0 -or
        $scannedBytes -gt $MaximumCollectedLogBytes) {
        throw "The host secret scan input is empty or exceeded its bounded limit."
    }

    $clean = $true
    foreach ($file in $uniqueFiles) {
        if (Test-FileContainsCandidate -Path $file.FullName -Candidates $candidates.AllValues) {
            $clean = $false
            break
        }
    }

    Write-JsonFile -Path $outputPath -Value ([ordered]@{
        schemaVersion = 1
        clean = $clean
        secretCount = 14
        missingInputCount = 0
        scannedFileCount = $uniqueFiles.Count
        scannedBytes = [long]$scannedBytes
    })
    if (-not $clean) {
        throw "The host E2E secret/content scan did not pass."
    }
}

function Test-WindowsAclRestricted {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [switch]$RequireProtected
    )

    $acl = Get-Acl -LiteralPath $Path
    if ($RequireProtected -and -not $acl.AreAccessRulesProtected) {
        return $false
    }

    $allowedSids = [System.Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    [void]$allowedSids.Add([System.Security.Principal.WindowsIdentity]::GetCurrent().User.Value)
    [void]$allowedSids.Add(([System.Security.Principal.SecurityIdentifier]::new(
        [System.Security.Principal.WellKnownSidType]::LocalSystemSid,
        $null)).Value)
    [void]$allowedSids.Add(([System.Security.Principal.SecurityIdentifier]::new(
        [System.Security.Principal.WellKnownSidType]::BuiltinAdministratorsSid,
        $null)).Value)
    foreach ($rule in $acl.Access) {
        $sid = $rule.IdentityReference.Translate([System.Security.Principal.SecurityIdentifier]).Value
        if ($rule.AccessControlType -eq [System.Security.AccessControl.AccessControlType]::Allow -and
            -not $allowedSids.Contains($sid)) {
            return $false
        }
    }

    return $true
}

function Protect-HostCaptureDirectory {
    param([Parameter(Mandatory = $true)][string]$Path)

    Assert-HostCapturePath -Path $Path
    [System.IO.Directory]::CreateDirectory($Path) | Out-Null
    Assert-HostCapturePath -Path $Path
    if ($IsWindows) {
        if (Test-WindowsAclRestricted -Path $Path -RequireProtected) {
            return
        }

        $acl = Get-Acl -LiteralPath $Path
        $acl.SetAccessRuleProtection($true, $true)
        Set-Acl -LiteralPath $Path -AclObject $acl
        if (-not (Test-WindowsAclRestricted -Path $Path -RequireProtected)) {
            throw "A host-capture directory does not have an exact restricted ACL."
        }

        return
    }

    $privateDirectoryMode = [System.IO.UnixFileMode]::UserRead -bor
        [System.IO.UnixFileMode]::UserWrite -bor
        [System.IO.UnixFileMode]::UserExecute
    [System.IO.File]::SetUnixFileMode($Path, $privateDirectoryMode)
    if ((Get-Item -LiteralPath $Path).UnixFileMode -ne $privateDirectoryMode) {
        throw "A host-capture directory does not have mode 0700."
    }
}

function Set-PrivateHostCaptureFile {
    param([Parameter(Mandatory = $true)][string]$Path)

    Assert-HostCapturePath -Path $Path
    Assert-RegularFileTarget -Path $Path
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "A required host-capture file is missing."
    }

    if ($IsWindows) {
        if (-not (Test-WindowsAclRestricted -Path $Path)) {
            throw "A host-capture file does not inherit only the allowed identities."
        }

        return
    }

    $privateFileMode = [System.IO.UnixFileMode]::UserRead -bor
        [System.IO.UnixFileMode]::UserWrite
    [System.IO.File]::SetUnixFileMode($Path, $privateFileMode)
    if ((Get-Item -LiteralPath $Path).UnixFileMode -ne $privateFileMode) {
        throw "A host-capture file does not have mode 0600."
    }
}

function Assert-PrivateHostCaptureTree {
    Assert-HostCapturePath -Path $HostCaptureRoot
    if (-not (Test-Path -LiteralPath $HostCaptureRoot -PathType Container)) {
        throw "The exact host-capture root is missing."
    }

    $entries = @(Get-SafeTreeEntries -Root $HostCaptureRoot)
    $directories = @((Get-Item -LiteralPath $HostCaptureRoot)) +
        @($entries | Where-Object { $_ -is [System.IO.DirectoryInfo] })
    $files = @($entries | Where-Object { $_ -is [System.IO.FileInfo] })
    $totalBytes = Get-TotalFileBytes -Files $files
    if ($entries.Count -gt $MaximumScanFileCount -or
        $totalBytes -gt $MaximumCollectedLogBytes) {
        throw "The host-capture tree exceeded its bounded evidence limits."
    }

    if ($IsWindows) {
        $restrictedDirectoryCount = @($directories | Where-Object {
            Test-WindowsAclRestricted -Path $_.FullName -RequireProtected
        }).Count
        $restrictedFileCount = @($files | Where-Object {
            Test-WindowsAclRestricted -Path $_.FullName
        }).Count
    }
    else {
        $privateDirectoryMode = [System.IO.UnixFileMode]::UserRead -bor
            [System.IO.UnixFileMode]::UserWrite -bor
            [System.IO.UnixFileMode]::UserExecute
        $privateFileMode = [System.IO.UnixFileMode]::UserRead -bor
            [System.IO.UnixFileMode]::UserWrite
        $restrictedDirectoryCount = @($directories | Where-Object {
            $_.UnixFileMode -eq $privateDirectoryMode
        }).Count
        $restrictedFileCount = @($files | Where-Object {
            $_.UnixFileMode -eq $privateFileMode
        }).Count
    }

    if ($restrictedDirectoryCount -ne $directories.Count -or
        $restrictedFileCount -ne $files.Count) {
        throw "The host-capture tree is not private."
    }

    return [ordered]@{
        directoryCount = $directories.Count
        restrictedDirectoryCount = $restrictedDirectoryCount
        fileCount = $files.Count
        restrictedFileCount = $restrictedFileCount
    }
}

function Get-SafeManagedFileSha256 {
    param([Parameter(Mandatory = $true)][string]$Path)

    Assert-SafeManagedRegularFileTarget -Path $Path
    $stream = [System.IO.File]::OpenRead($Path)
    try {
        return [Convert]::ToHexString(
            [System.Security.Cryptography.SHA256]::HashData($stream)).ToLowerInvariant()
    }
    finally {
        $stream.Dispose()
        Assert-SafeManagedRegularFileTarget -Path $Path
    }
}

function Import-HostCommandCaptures {
    Assert-NoComposeOneOffContainers
    [void](Assert-PrivateHostCaptureTree)
    if (-not (Test-Path -LiteralPath $HostCommandCaptureRoot -PathType Container)) {
        return
    }

    $entries = @(Get-SafeTreeEntries -Root $HostCommandCaptureRoot)
    if (@($entries | Where-Object { $_ -is [System.IO.DirectoryInfo] }).Count -ne 0) {
        throw "The host command-capture staging directory contains an unexpected directory."
    }

    $files = @($entries | Where-Object { $_ -is [System.IO.FileInfo] })
    $totalBytes = Get-TotalFileBytes -Files $files

    if ($files.Count -gt $MaximumScanFileCount -or $totalBytes -gt $MaximumCollectedLogBytes) {
        throw "The host command-capture staging directory exceeded its bounded evidence limits."
    }

    Assert-OwnedArtifactPath -Path $DockerCommandLogsRoot
    [System.IO.Directory]::CreateDirectory($DockerCommandLogsRoot) | Out-Null
    foreach ($file in $files) {
        Set-PrivateHostCaptureFile -Path $file.FullName
        $destinationPath = Join-Path $DockerCommandLogsRoot $file.Name
        Assert-OwnedRegularFileTarget -Path $destinationPath
        if (Test-Path -LiteralPath $destinationPath) {
            $destination = Get-Item -LiteralPath $destinationPath -Force
            if ($file.Length -ne $destination.Length -or
                (Get-SafeManagedFileSha256 -Path $file.FullName) -cne
                    (Get-SafeManagedFileSha256 -Path $destinationPath)) {
                throw "A Docker command-capture destination already exists with different content."
            }
        }
        else {
            [System.IO.File]::Copy($file.FullName, $destinationPath, $false)
            Set-PrivateHostCaptureFile -Path $file.FullName
            Assert-OwnedRegularFileTarget -Path $destinationPath
        }

        [System.IO.File]::Delete($file.FullName)
        Assert-HostCapturePath -Path $file.FullName
        if (Test-Path -LiteralPath $file.FullName) {
            throw "A staged host command capture still exists after exact deletion."
        }
    }

    [void](Assert-PrivateHostCaptureTree)
}

function Remove-HostCaptureRoot {
    Assert-NoComposeOneOffContainers
    Import-HostCommandCaptures
    [void](Assert-PrivateHostCaptureTree)
    Reset-OwnedChildDirectory -Path $HostCaptureRoot
    Assert-HostCapturePath -Path $HostCaptureRoot
    [System.IO.Directory]::Delete($HostCaptureRoot, $false)
    Assert-OwnedArtifactPath -Path $HostCaptureRoot
    if (Test-Path -LiteralPath $HostCaptureRoot) {
        throw "The staged host-capture root still exists after exact deletion."
    }

    Protect-HostCaptureDirectory -Path $HostCaptureRoot
    Protect-HostCaptureDirectory -Path $HostCommandCaptureRoot
}

function Write-PermissionScan {
    $hostCapturePermissions = Assert-PrivateHostCaptureTree
    $runtimeFiles = @($RuntimeSecretNames | ForEach-Object { Join-Path $RuntimeSecretsRoot $_ })
    $credentialFiles = @($CredentialNames | ForEach-Object { Join-Path $CredentialsRoot $_ })
    $allFiles = $runtimeFiles + $credentialFiles
    foreach ($file in $allFiles) {
        [void](Read-BoundedPrivateValue -Path $file)
    }

    if ($IsWindows) {
        $artifactRootRestricted = Test-WindowsAclRestricted -Path $ArtifactRoot -RequireProtected
        $runtimeSecretDirectoryRestricted = Test-WindowsAclRestricted -Path $RuntimeSecretsRoot -RequireProtected
        $credentialDirectoryRestricted = Test-WindowsAclRestricted -Path $CredentialsRoot -RequireProtected
        $restrictedRuntimeSecretCount = @($runtimeFiles | Where-Object { Test-WindowsAclRestricted -Path $_ }).Count
        $restrictedCredentialCount = @($credentialFiles | Where-Object { Test-WindowsAclRestricted -Path $_ }).Count
    }
    else {
        $privateDirectoryMode = [System.IO.UnixFileMode]::UserRead -bor
            [System.IO.UnixFileMode]::UserWrite -bor
            [System.IO.UnixFileMode]::UserExecute
        $privateFileMode = [System.IO.UnixFileMode]::UserRead -bor
            [System.IO.UnixFileMode]::UserWrite
        $artifactRootRestricted =
            (Get-Item -LiteralPath $ArtifactRoot).UnixFileMode -eq $privateDirectoryMode
        $runtimeSecretDirectoryRestricted =
            (Get-Item -LiteralPath $RuntimeSecretsRoot).UnixFileMode -eq $privateDirectoryMode
        $credentialDirectoryRestricted =
            (Get-Item -LiteralPath $CredentialsRoot).UnixFileMode -eq $privateDirectoryMode
        $restrictedRuntimeSecretCount = @($runtimeFiles | Where-Object {
            (Get-Item -LiteralPath $_).UnixFileMode -eq $privateFileMode
        }).Count
        $restrictedCredentialCount = @($credentialFiles | Where-Object {
            (Get-Item -LiteralPath $_).UnixFileMode -eq $privateFileMode
        }).Count
    }

    $clean = $artifactRootRestricted -and
        $runtimeSecretDirectoryRestricted -and
        $credentialDirectoryRestricted -and
        $hostCapturePermissions.directoryCount -eq $hostCapturePermissions.restrictedDirectoryCount -and
        $hostCapturePermissions.fileCount -eq $hostCapturePermissions.restrictedFileCount -and
        $restrictedRuntimeSecretCount -eq 14 -and
        $restrictedCredentialCount -eq 5
    Write-JsonFile -Path (Join-Path $CollectedLogsRoot "host-permission-scan.json") -Value ([ordered]@{
        schemaVersion = 1
        clean = $clean
        artifactRootRestricted = $artifactRootRestricted
        runtimeSecretDirectoryRestricted = $runtimeSecretDirectoryRestricted
        runtimeSecretFileCount = 14
        restrictedRuntimeSecretFileCount = $restrictedRuntimeSecretCount
        credentialDirectoryRestricted = $credentialDirectoryRestricted
        credentialFileCount = 5
        restrictedCredentialFileCount = $restrictedCredentialCount
        hostCaptureDirectoryCount = $hostCapturePermissions.directoryCount
        restrictedHostCaptureDirectoryCount = $hostCapturePermissions.restrictedDirectoryCount
        hostCaptureFileCount = $hostCapturePermissions.fileCount
        restrictedHostCaptureFileCount = $hostCapturePermissions.restrictedFileCount
    })
    if (-not $clean) {
        throw "The E2E host permission scan did not pass."
    }
}

function Collect-AndScanEvidence {
    Import-HostCommandCaptures
    Write-PermissionScan
    Write-DatabaseScan
    Collect-Logs
    Write-LogContinuityScan
    Write-HostSecretScan
}

function Restart-AppServices {
    param([Parameter(Mandatory = $true)][string[]]$Services)

    Invoke-ComposeCommand -CommandId ("restart-" + ($Services -join "-")) -Arguments (@("restart") + $Services)
    Invoke-ComposeCommand `
        -CommandId ("wait-healthy-" + ($Services -join "-")) `
        -Arguments (@("up", "--detach", "--no-deps", "--wait", "--wait-timeout", "360") + $Services)
}

function Assert-RepeatSyncOutcome {
    param([Parameter(Mandatory = $true)][string]$Role)

    $path = Join-Path $HandoffRoot "$Role-sync-outcome.json"
    Assert-OwnedRegularFileTarget -Path $path
    $outcome = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    Assert-OwnedRegularFileTarget -Path $path
    if ($outcome.schemaVersion -ne 1 -or
        -not [string]::Equals($outcome.outcome, "notModified", [StringComparison]::Ordinal)) {
        throw "The repeated source synchronization was not a persisted NotModified outcome."
    }

    $repeatPath = Join-Path $HandoffRoot "$Role-repeat-sync-outcome.json"
    Assert-OwnedRegularFileTarget -Path $repeatPath
    [System.IO.File]::Copy($path, $repeatPath, $true)
    Assert-OwnedRegularFileTarget -Path $path
    Assert-OwnedRegularFileTarget -Path $repeatPath
}

function Assert-FinalScenarioReport {
    $path = Join-Path $HandoffRoot "scenario-results.json"
    Assert-OwnedRegularFileTarget -Path $path
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "The final E2E scenario report is missing."
    }

    $report = Get-Content -LiteralPath $path -Raw | ConvertFrom-Json
    Assert-OwnedRegularFileTarget -Path $path
    $actualIds = @($report.scenarios | ForEach-Object { $_.scenarioId })
    if ($report.schemaVersion -ne 1 -or
        $report.scenarioCount -ne 19 -or
        $report.passedCount -ne 19 -or
        $report.failedCount -ne 0 -or
        $report.pendingCount -ne 0 -or
        -not [string]::Equals($report.status, "passed", [StringComparison]::Ordinal) -or
        ($actualIds -join "`n") -cne ($ExpectedScenarioIds -join "`n")) {
        throw "The final E2E scenario report is not the exact 19/19 PASS contract."
    }
}

function Get-ImageIdentity {
    param([Parameter(Mandatory = $true)][string]$Image)

    $identity = Invoke-NativeText -FilePath "docker" -Arguments @(
        "image",
        "inspect",
        "--format",
        '{{.Id}}|{{ index .Config.Labels "org.opencontainers.image.revision" }}|{{ index .Config.Labels "io.candoitall.source-fingerprint" }}',
        $Image)
    $parts = $identity.Split("|", [StringSplitOptions]::None)
    if ($parts.Count -ne 3 -or
        [string]::IsNullOrWhiteSpace($parts[0]) -or
        [string]::IsNullOrWhiteSpace($parts[1]) -or
        [string]::IsNullOrWhiteSpace($parts[2])) {
        throw "A required proof image has no exact identity, revision, or worktree fingerprint label."
    }

    return [pscustomobject]@{
        Image = $Image
        ImageId = $parts[0]
        Revision = $parts[1]
        SourceFingerprint = $parts[2]
    }
}

function Assert-CurrentImageRevisions {
    Assert-SourceStateUnchanged
    $sourceRevision = Invoke-NativeText `
        -FilePath "git" `
        -Arguments @(
            "-c",
            "safe.directory=$RepositoryRoot",
            "-C",
            $RepositoryRoot,
            "rev-parse",
            "HEAD")
    $appIdentity = Get-ImageIdentity -Image $AppImage
    $upstreamIdentity = Get-ImageIdentity -Image $UpstreamImage
    if ($appIdentity.Revision -cne $sourceRevision -or
        $upstreamIdentity.Revision -cne $sourceRevision -or
        $appIdentity.SourceFingerprint -cne $SourceFingerprint -or
        $upstreamIdentity.SourceFingerprint -cne $SourceFingerprint) {
        throw "The proof image labels do not match the current revision and governed worktree fingerprint."
    }

    return [pscustomobject]@{
        SourceRevision = $sourceRevision
        App = $appIdentity
        Upstream = $upstreamIdentity
    }
}

function Assert-RunningContainerImageReuse {
    param([switch]$AllowStopped)

    $identities = Assert-CurrentImageRevisions
    $expectedImages = @{
        "central" = $identities.App
        "client-a" = $identities.App
        "client-b" = $identities.App
        "deterministic-upstream" = $identities.Upstream
        "deterministic-personal-upstream" = $identities.Upstream
    }
    foreach ($service in $expectedImages.Keys) {
        $psArguments = $ComposeBaseArguments + @("ps", "--quiet")
        if ($AllowStopped) {
            $psArguments += "--all"
        }

        $psArguments += $service
        $containerId = Invoke-NativeText -FilePath "docker" -Arguments $psArguments
        if ([string]::IsNullOrWhiteSpace($containerId)) {
            throw "A required app or upstream proof container is missing."
        }

        $containerIdentity = Invoke-NativeText -FilePath "docker" -Arguments @(
            "inspect",
            "--format",
            "{{.Config.Image}}|{{.Image}}",
            $containerId)
        $parts = $containerIdentity.Split("|", [StringSplitOptions]::None)
        $expected = $expectedImages[$service]
        if ($parts.Count -ne 2 -or
            $parts[0] -cne $expected.Image -or
            $parts[1] -cne $expected.ImageId) {
            throw "A proof container does not reuse the exact current app/upstream image ID."
        }
    }
}

function Import-BuildTranscripts {
    if (-not (Test-Path -LiteralPath $BuildTranscriptRoot -PathType Container)) {
        throw "The bounded build transcript root was not produced."
    }

    Assert-BuildTranscriptPath -Path $BuildTranscriptRoot
    $entries = @(Get-SafeTreeEntries -Root $BuildTranscriptRoot)
    if ($entries.Count -eq 0 -or
        @($entries | Where-Object { $_ -is [System.IO.DirectoryInfo] }).Count -ne 0) {
        throw "The bounded build transcript root is empty or contains an unexpected directory."
    }

    $destinationRoot = Join-Path `
        (Join-Path $HandoffRoot "build-transcripts") `
        (Split-Path -Leaf $BuildTranscriptRoot)
    Assert-SafeManagedDirectory -Path $destinationRoot
    [System.IO.Directory]::CreateDirectory($destinationRoot) | Out-Null
    foreach ($entry in $entries) {
        Assert-BuildTranscriptPath -Path $entry.FullName
        $destinationPath = Join-Path $destinationRoot $entry.Name
        Assert-BuildTranscriptPath -Path $entry.FullName
        Assert-OwnedRegularFileTarget -Path $destinationPath
        Assert-OwnedArtifactPath -Path $destinationPath
        [System.IO.File]::Copy($entry.FullName, $destinationPath, $false)
        Assert-BuildTranscriptPath -Path $entry.FullName
        Assert-OwnedRegularFileTarget -Path $destinationPath
    }

    Assert-BuildTranscriptPath -Path $BuildTranscriptRoot
    foreach ($entry in $entries) {
        Assert-BuildTranscriptPath -Path $entry.FullName
        Assert-RegularFileTarget -Path $entry.FullName
        [System.IO.File]::Delete($entry.FullName)
        Assert-BuildTranscriptPath -Path $entry.FullName
    }

    [System.IO.Directory]::Delete($BuildTranscriptRoot, $false)
}

function Write-StackHandoff {
    $imageIdentities = Assert-CurrentImageRevisions
    $serviceStates = [System.Collections.Generic.List[object]]::new()
    foreach ($service in @(
        "central",
        "client-a",
        "client-b",
        "db",
        "deterministic-upstream",
        "deterministic-personal-upstream")) {
        $containerId = Invoke-NativeText -FilePath "docker" -Arguments ($ComposeBaseArguments + @("ps", "--quiet", $service))
        if ([string]::IsNullOrWhiteSpace($containerId)) {
            throw "A required E2E service container is missing."
        }

        $state = Invoke-NativeText -FilePath "docker" -Arguments @(
            "inspect",
            "--format",
            "{{.State.Status}}|{{if .State.Health}}{{.State.Health.Status}}{{else}}none{{end}}|{{.Config.Image}}|{{.Image}}",
            $containerId)
        $parts = $state.Split("|", [StringSplitOptions]::None)
        if ($parts.Count -ne 4 -or $parts[0] -ne "running" -or $parts[1] -ne "healthy") {
            throw "A required E2E service is not running and healthy."
        }

        $serviceStates.Add([ordered]@{
            service = $service
            state = $parts[0]
            health = $parts[1]
            image = $parts[2]
            imageId = $parts[3]
        })
    }

    $appStates = @($serviceStates | Where-Object { $_.service -in @("central", "client-a", "client-b") })
    $upstreamStates = @($serviceStates | Where-Object {
        $_.service -in @("deterministic-upstream", "deterministic-personal-upstream")
    })
    if ($appStates.Count -ne 3 -or
        @($appStates | Where-Object {
            $_.image -ne $AppImage -or $_.imageId -ne $imageIdentities.App.ImageId
        }).Count -ne 0 -or
        $upstreamStates.Count -ne 2 -or
        @($upstreamStates | Where-Object {
            $_.image -ne $UpstreamImage -or $_.imageId -ne $imageIdentities.Upstream.ImageId
        }).Count -ne 0) {
        throw "The running E2E containers do not reuse the current exact app/upstream image IDs."
    }

    $statusText = Invoke-NativeText -FilePath "docker" -Arguments ($ComposeBaseArguments + @("ps", "--all"))

    Write-Utf8File -Path (Join-Path $HandoffRoot "container-status.txt") -Content ($statusText + "`n")
    Write-JsonFile -Path (Join-Path $HandoffRoot "health.json") -Value ([ordered]@{
        schemaVersion = 1
        healthy = $true
        services = @($serviceStates)
        observedAtUtc = [DateTimeOffset]::UtcNow.ToString("O")
    })
    Write-JsonFile -Path (Join-Path $HandoffRoot "image-reuse.json") -Value ([ordered]@{
        schemaVersion = 1
        appImage = $AppImage
        appImageId = $imageIdentities.App.ImageId
        appImageRevision = $imageIdentities.App.Revision
        appContainers = $appStates
        upstreamImage = $UpstreamImage
        upstreamImageId = $imageIdentities.Upstream.ImageId
        upstreamImageRevision = $imageIdentities.Upstream.Revision
        sourceFingerprint = $SourceFingerprint
        upstreamContainers = $upstreamStates
        sourceRevision = $imageIdentities.SourceRevision
    })
    Write-Utf8File -Path (Join-Path $HandoffRoot "manual-handoff.md") -Content @"
# Shared Providers E2E handoff

Status: PASS (19/19 backend checkpoint scenarios).

- Central: http://127.0.0.1:5210
- Client A: http://127.0.0.1:5211
- Client B: http://127.0.0.1:5212
- Artifact root: .artifacts/shared-providers-e2e
- The dedicated stack is intentionally left running.
- Generated credentials remain only in the ignored, access-restricted artifact root and are not printed here.
"@
}

function Build-E2eInputs {
    param([switch]$VerifyImagesOnly)

    Write-Step "Building the E2E orchestrator sequentially."
    Invoke-NativeCommand -FilePath "dotnet" -Arguments @(
        "build",
        $ToolProject,
        "--configuration",
        "Release",
        "--no-restore",
        "-m:1",
        "--nologo") `
        -CaptureBasePath (Join-Path $BuildTranscriptRoot "tool-build") `
        -TimeoutSeconds 1200 `
        -MaximumStandardOutputBytes 16MB `
        -MaximumStandardErrorBytes 8MB | Out-Null

    if (-not (Test-Path -LiteralPath $ToolAssembly -PathType Leaf)) {
        throw "The E2E orchestrator assembly was not produced."
    }

    if ($VerifyImagesOnly) {
        Write-Step "Verifying the prebuilt app and upstream images."
        [void](Assert-CurrentImageRevisions)
        return
    }

    $revision = Invoke-NativeText `
        -FilePath "git" `
        -Arguments @(
            "-c",
            "safe.directory=$RepositoryRoot",
            "-C",
            $RepositoryRoot,
            "rev-parse",
            "HEAD")
    $shortFingerprint = $SourceFingerprint.Substring(0, 12)
    $buildDate = [DateTimeOffset]::UtcNow.ToString("O")
    Write-Step "Building the application image once for all three app roles."
    Invoke-NativeCommand -FilePath "docker" -Arguments @(
        "build",
        "--progress=plain",
        "--file",
        $AppDockerfile,
        "--tag",
        $AppImage,
        "--build-context",
        "components=$ComponentsRoot",
        "--build-context",
        "filetools=$FileToolsRoot",
        "--build-arg",
        "BUILD_DATE=$buildDate",
        "--build-arg",
        "BUILD_REVISION=$revision",
        "--build-arg",
        "BUILD_SOURCE_FINGERPRINT=$SourceFingerprint",
        "--build-arg",
        "BUILD_VERSION=sb07-$shortFingerprint",
        $RepositoryRoot) `
        -CaptureBasePath (Join-Path $BuildTranscriptRoot "app-image-build") `
        -TimeoutSeconds 1200 `
        -MaximumStandardOutputBytes 32MB `
        -MaximumStandardErrorBytes 16MB | Out-Null

    Write-Step "Building the deterministic upstream image once for both fixtures."
    Invoke-NativeCommand -FilePath "docker" -Arguments @(
        "build",
        "--progress=plain",
        "--file",
        $UpstreamDockerfile,
        "--tag",
        $UpstreamImage,
        "--build-arg",
        "BUILD_DATE=$buildDate",
        "--build-arg",
        "BUILD_REVISION=$revision",
        "--build-arg",
        "BUILD_SOURCE_FINGERPRINT=$SourceFingerprint",
        "--build-arg",
        "BUILD_VERSION=sb07-$shortFingerprint",
        $UpstreamRoot) `
        -CaptureBasePath (Join-Path $BuildTranscriptRoot "upstream-image-build") `
        -TimeoutSeconds 1200 `
        -MaximumStandardOutputBytes 32MB `
        -MaximumStandardErrorBytes 16MB | Out-Null
    [void](Assert-CurrentImageRevisions)
}

function Prepare-FreshTopology {
    if (-not $Reset) {
        throw "Every governed fresh E2E proof requires the explicit -Reset switch."
    }

    Write-Step "Stopping only the dedicated Compose project before credential reset."
    Invoke-NativeCommand -FilePath "docker" -Arguments ($ComposeBaseArguments + @(
        "down",
        "--volumes",
        "--remove-orphans")) | Out-Null
    Assert-NoComposeOneOffContainers
    foreach ($port in @(5210, 5211, 5212, 5213, 5214, 55432)) {
        Assert-PortAvailable -Port $port
    }

    Write-Step "Preparing the exact marked artifact root and ephemeral credentials."
    Invoke-NativeCommand -FilePath "dotnet" -Arguments @(
        $ToolAssembly,
        "prepare",
        "--repository-root",
        $RepositoryRoot,
        "--artifact-root",
        $ArtifactRoot,
        "--reset",
        $Reset.ToString().ToLowerInvariant()) | Out-Null
    Assert-OwnedArtifactRoot
    Reset-OwnedChildDirectory -Path $HostCaptureRoot
    Protect-HostCaptureDirectory -Path $HostCaptureRoot
    Protect-HostCaptureDirectory -Path $HostCommandCaptureRoot
    Reset-OwnedChildDirectory -Path $DockerCommandLogsRoot
    Reset-OwnedChildDirectory -Path $ToolPublishRoot
    Invoke-NativeCommand -FilePath "dotnet" -Arguments @(
        "publish",
        $ToolProject,
        "--configuration",
        "Release",
        "--no-build",
        "--output",
        $ToolPublishRoot,
        "--nologo") `
        -CaptureBasePath (Join-Path $BuildTranscriptRoot "tool-publish") `
        -TimeoutSeconds 600 `
        -MaximumStandardOutputBytes 8MB `
        -MaximumStandardErrorBytes 4MB | Out-Null
    Write-AndValidateComposeConfig
    Import-BuildTranscripts

    Write-Step "Starting the isolated database and deterministic upstreams."
    Invoke-NativeCommand `
        -FilePath "docker" `
        -Arguments ($ComposeBaseArguments + @("up", "--detach", "artifact-permissions")) | Out-Null
    Invoke-NativeCommand `
        -FilePath "docker" `
        -Arguments ($ComposeBaseArguments + @("wait", "artifact-permissions")) | Out-Null
    Assert-ArtifactPermissionServiceStopped
    [void](Assert-PrivateHostCaptureTree)
    Write-HostRunMetadata
    Invoke-ComposeCommand -CommandId "up-dependencies" -Arguments @(
        "up",
        "--detach",
        "--wait",
        "--wait-timeout",
        "360",
        "db",
        "deterministic-upstream",
        "deterministic-personal-upstream")

    Write-Step "Seeding central through canonical application services."
    Invoke-ToolService -Service "e2e-central" -Command "seed-central"
    Invoke-ComposeCommand -CommandId "up-central" -Arguments @(
        "up",
        "--detach",
        "--no-deps",
        "--wait",
        "--wait-timeout",
        "360",
        "central")

    Write-Step "Seeding both independent clients through canonical application services."
    Invoke-ToolService -Service "e2e-client-a" -Command "seed-client-a"
    Invoke-ToolService -Service "e2e-client-b" -Command "seed-client-b"
    Invoke-ComposeCommand -CommandId "up-clients" -Arguments @(
        "up",
        "--detach",
        "--no-deps",
        "--wait",
        "--wait-timeout",
        "360",
        "client-a",
        "client-b")
}

function Prepare-ResumeTopology {
    if ($Reset) {
        throw "The -Reset switch is valid only when -StartAt is prepare."
    }

    Assert-NoComposeOneOffContainers
    Assert-ArtifactPermissionServiceStopped
    [void](Assert-PrivateHostCaptureTree)
    Import-HostCommandCaptures
    Invoke-NativeCommand -FilePath "dotnet" -Arguments @(
        $ToolAssembly,
        "prepare",
        "--repository-root",
        $RepositoryRoot,
        "--artifact-root",
        $ArtifactRoot,
        "--reset",
        "false") | Out-Null
    Assert-OwnedArtifactRoot
    Reset-OwnedChildDirectory -Path $ToolPublishRoot
    Invoke-NativeCommand -FilePath "dotnet" -Arguments @(
        "publish",
        $ToolProject,
        "--configuration",
        "Release",
        "--no-build",
        "--output",
        $ToolPublishRoot,
        "--nologo") `
        -CaptureBasePath (Join-Path $BuildTranscriptRoot "tool-publish") `
        -TimeoutSeconds 600 `
        -MaximumStandardOutputBytes 8MB `
        -MaximumStandardErrorBytes 4MB | Out-Null
    Write-AndValidateComposeConfig
    Import-BuildTranscripts
    Assert-RunningContainerImageReuse -AllowStopped
    Assert-OwnedArtifactPath -Path $DockerCommandLogsRoot
    [System.IO.Directory]::CreateDirectory($DockerCommandLogsRoot) | Out-Null
    $sequenceValues = @(Get-ChildItem -LiteralPath $DockerCommandLogsRoot -File -Force | ForEach-Object {
        if ($_.Name -match "^(?<sequence>[0-9]+)-") {
            [int]$Matches.sequence
        }
    })
    $script:CommandSequence = $sequenceValues.Count -eq 0 ?
        0 : [int](($sequenceValues | Measure-Object -Maximum).Maximum)
}

Assert-RepositoryLayout
if ($SkipImageBuild) {
    throw "The governed E2E lifecycle does not permit -SkipImageBuild."
}

$SourceState = Get-SourceState
$SourceFingerprint = $SourceState.Fingerprint
$imageTag = $SourceFingerprint.Substring(0, 12)
$AppImage = "candoitall-shared-providers:$imageTag"
$UpstreamImage = "candoitall-shared-providers-upstream:$imageTag"
$containerIdentity = Get-HostContainerIdentity
$AppUid = $containerIdentity.Uid
$AppGid = $containerIdentity.Gid
$LogMarkerCounts = New-EmptyLogMarkerCounts
$LastLogCheckpoint = "created"
if ($StartAt -ceq "prepare") {
    $RunMarker = "sb07-$([Guid]::NewGuid().ToString('N'))"
}
else {
    Assert-OwnedArtifactRoot
    $RunMarker = Read-HostRunMarker
    $ResumeCount++
}

Set-ChildEnvironmentAssignments
Write-Step "Verifying Docker Engine and Compose."
Assert-LocalDefaultDockerContext
[void](Invoke-NativeText -FilePath "docker" -Arguments @("info", "--format", "{{.ServerVersion}}"))
[void](Invoke-NativeText -FilePath "docker" -Arguments @("compose", "version", "--short"))
Build-E2eInputs -VerifyImagesOnly:($StartAt -ne "prepare")

if (Test-PhaseEnabled -Phase "prepare") {
    Prepare-FreshTopology
    Invoke-LogContinuityCheckpoint -Checkpoint "prepare-complete" -ExpectedIncrements @{
        "central" = 1
        "client-a" = 1
        "client-b" = 1
        "db" = 1
        "deterministic-personal-upstream" = 1
        "deterministic-upstream" = 1
    }
}
else {
    Prepare-ResumeTopology
    Invoke-LogContinuityCheckpoint -Checkpoint "resume-entry-$StartAt" -ExpectedIncrements @{}
}

if (Test-PhaseEnabled -Phase "normal") {
    Write-Step "Proving persisted idempotent source synchronization."
    Invoke-ToolService -Service "e2e-client-a" -Command "sync-client-a"
    Assert-RepeatSyncOutcome -Role "client-a"
    Invoke-ToolService -Service "e2e-client-b" -Command "sync-client-b"
    Assert-RepeatSyncOutcome -Role "client-b"
    Restart-AppServices -Services @("client-a", "client-b")
    Invoke-LogContinuityCheckpoint -Checkpoint "normal-before-scenarios" -ExpectedIncrements @{
        "client-a" = 1
        "client-b" = 1
    }
    Invoke-ScenarioPhase -Phase "normal"
    Invoke-LogContinuityCheckpoint -Checkpoint "normal-complete" -ExpectedIncrements @{}
}

if (Test-PhaseEnabled -Phase "unpublished") {
    Write-Step "Proving authoritative unpublish without deletion or fallback."
    Invoke-ToolService -Service "e2e-central" -Command "unpublish-text"
    Restart-AppServices -Services @("central")
    Invoke-ToolService -Service "e2e-client-a" -Command "sync-client-a"
    Invoke-ToolService -Service "e2e-client-b" -Command "sync-client-b"
    Restart-AppServices -Services @("client-a", "client-b")
    Invoke-LogContinuityCheckpoint -Checkpoint "unpublished-before-scenarios" -ExpectedIncrements @{
        "central" = 1
        "client-a" = 1
        "client-b" = 1
    }
    Invoke-ScenarioPhase -Phase "unpublished"
    Invoke-LogContinuityCheckpoint -Checkpoint "unpublished-complete" -ExpectedIncrements @{}
}

if (Test-PhaseEnabled -Phase "republished") {
    Write-Step "Proving publication reappearance with stable local identities."
    Invoke-ToolService -Service "e2e-central" -Command "republish-text"
    Restart-AppServices -Services @("central")
    Invoke-ToolService -Service "e2e-client-a" -Command "sync-client-a"
    Invoke-ToolService -Service "e2e-client-b" -Command "sync-client-b"
    Restart-AppServices -Services @("client-a", "client-b")
    Invoke-LogContinuityCheckpoint -Checkpoint "republished-before-scenarios" -ExpectedIncrements @{
        "central" = 1
        "client-a" = 1
        "client-b" = 1
    }
    Invoke-ScenarioPhase -Phase "republished"
    Invoke-LogContinuityCheckpoint -Checkpoint "republished-complete" -ExpectedIncrements @{}
}

if (Test-PhaseEnabled -Phase "identity-mismatch") {
    Write-Step "Proving source identity mismatch is fail-closed."
    Invoke-ToolService -Service "e2e-client-a" -Command "point-client-a-at-client-b"
    Restart-AppServices -Services @("client-a")
    Invoke-LogContinuityCheckpoint -Checkpoint "identity-mismatch-before-scenarios" -ExpectedIncrements @{
        "client-a" = 1
    }
    Invoke-ScenarioPhase -Phase "identity-mismatch"
    Invoke-LogContinuityCheckpoint -Checkpoint "identity-mismatch-complete" -ExpectedIncrements @{}
}

if (Test-PhaseEnabled -Phase "identity-restored") {
    Write-Step "Restoring the original source identity and synchronized runtime projection."
    Invoke-ToolService -Service "e2e-client-a" -Command "restore-client-a-source"
    Restart-AppServices -Services @("client-a")
    Invoke-LogContinuityCheckpoint -Checkpoint "identity-restored-before-scenarios" -ExpectedIncrements @{
        "client-a" = 1
    }
    Invoke-ScenarioPhase -Phase "identity-restored"
    Invoke-LogContinuityCheckpoint -Checkpoint "identity-restored-complete" -ExpectedIncrements @{}
}

if (Test-PhaseEnabled -Phase "outage") {
    Write-Step "Proving central outage behavior without dependency auto-start or personal fallback."
    Invoke-ComposeCommand -CommandId "stop-central" -Arguments @("stop", "central")
    Invoke-ToolService -Service "e2e-client-a" -Command "sync-client-a-expect-offline" -NoDependencies
    Invoke-ToolService -Service "e2e-client-b" -Command "sync-client-b-expect-offline" -NoDependencies
    Restart-AppServices -Services @("client-a", "client-b")
    Invoke-LogContinuityCheckpoint -Checkpoint "outage-before-scenarios" -ExpectedIncrements @{
        "client-a" = 1
        "client-b" = 1
    }
    Invoke-ScenarioPhase -Phase "outage" -NoDependencies
    Invoke-LogContinuityCheckpoint -Checkpoint "outage-complete" -ExpectedIncrements @{}
}

if (Test-PhaseEnabled -Phase "recovery") {
    Write-Step "Recovering central and refreshing both client projections."
    Invoke-ComposeCommand -CommandId "prepare-recover-central" -Arguments @("stop", "central")
    Invoke-ComposeCommand -CommandId "recover-central" -Arguments @(
        "up",
        "--detach",
        "--no-deps",
        "--wait",
        "--wait-timeout",
        "360",
        "central")
    Invoke-ToolService -Service "e2e-client-a" -Command "sync-client-a"
    Invoke-ToolService -Service "e2e-client-b" -Command "sync-client-b"
    Restart-AppServices -Services @("client-a", "client-b")
    Invoke-LogContinuityCheckpoint -Checkpoint "recovery-before-scenarios" -ExpectedIncrements @{
        "central" = 1
        "client-a" = 1
        "client-b" = 1
    }

    Write-Step "Collecting bounded logs and database/secret evidence for the recovery gate."
    Collect-AndScanEvidence
    Invoke-ScenarioPhase -Phase "recovery"
    Assert-FinalScenarioReport

    Write-Step "Refreshing final evidence after the recovery runner completed."
    Collect-AndScanEvidence
    Assert-FinalScenarioReport
    Remove-HostCaptureRoot
    Write-StackHandoff
}

Write-Step "PASS: exact backend checkpoint is 19/19 and the dedicated stack remains running."
