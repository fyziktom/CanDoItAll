[CmdletBinding()]
param(
    [string] $Configuration = "Release",
    [string] $RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path,
    [string] $OutputPath = "",
    [switch] $NoBuild
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($OutputPath)) {
    $OutputPath = Join-Path $RepoRoot "codex\bundles\plugin-runtime-architecture-hardening-followup\reviews\artifacts\candoitall.docker.package.zip"
}

$projectPath = Join-Path $RepoRoot "src\plugins\Implementations\CanDoItAll.Plugin.Docker\CanDoItAll.Plugin.Docker.csproj"
$targetDir = Join-Path $RepoRoot "src\plugins\Implementations\CanDoItAll.Plugin.Docker\bin\$Configuration\net10.0"
$stageRoot = Join-Path $RepoRoot ".codex\plugin-packages\docker"
$manifestPath = Join-Path $stageRoot "plugin.package.json"
$iconPath = Join-Path $stageRoot "icon.svg"

function Get-PackageRelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string] $RootPath,

        [Parameter(Mandatory = $true)]
        [string] $ChildPath
    )

    $rootFullPath = [System.IO.Path]::GetFullPath($RootPath).TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
    $childFullPath = [System.IO.Path]::GetFullPath($ChildPath)
    $rootUri = [System.Uri]::new($rootFullPath + [System.IO.Path]::DirectorySeparatorChar)
    $childUri = [System.Uri]::new($childFullPath)

    return [System.Uri]::UnescapeDataString($rootUri.MakeRelativeUri($childUri).ToString()).Replace("/", "\")
}

if (-not $NoBuild) {
    dotnet build $projectPath -c $Configuration
    if ($LASTEXITCODE -ne 0) {
        throw "Docker plugin build failed with exit code $LASTEXITCODE."
    }
}

$requiredFiles = @(
    "CanDoItAll.Plugin.Docker.dll",
    "CanDoItAll.Plugin.Docker.deps.json"
)
$optionalFiles = @(
    "CanDoItAll.Plugin.Docker.pdb"
)

foreach ($fileName in $requiredFiles) {
    $sourcePath = Join-Path $targetDir $fileName
    if (-not (Test-Path -LiteralPath $sourcePath)) {
        throw "Required Docker plugin package file was not found: $sourcePath"
    }
}

if (Test-Path -LiteralPath $stageRoot) {
    Remove-Item -LiteralPath $stageRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $stageRoot | Out-Null
New-Item -ItemType Directory -Path (Split-Path -Parent $OutputPath) -Force | Out-Null

foreach ($fileName in $requiredFiles + $optionalFiles) {
    $sourcePath = Join-Path $targetDir $fileName
    if (Test-Path -LiteralPath $sourcePath) {
        Copy-Item -LiteralPath $sourcePath -Destination (Join-Path $stageRoot $fileName)
    }
}

$dockerSettingsFields = @(
    [ordered]@{
        key = "image"
        label = "Image"
        fieldType = 0
        isRequired = $false
        helpText = "Docker image reference."
    },
    [ordered]@{
        key = "containerName"
        label = "Container name"
        fieldType = 0
        isRequired = $false
        helpText = "Docker container name."
    },
    [ordered]@{
        key = "pullIfMissing"
        label = "Pull if missing"
        fieldType = 3
        isRequired = $false
        helpText = "Pull the image before creating the container when it is not available locally."
    },
    [ordered]@{
        key = "portMappings"
        label = "Port mappings"
        fieldType = 4
        isRequired = $false
        helpText = "JSON array of host:container port mappings."
    },
    [ordered]@{
        key = "tail"
        label = "Log tail"
        fieldType = 2
        isRequired = $false
        helpText = "Maximum number of log lines to read."
    },
    [ordered]@{
        key = "since"
        label = "Logs since"
        fieldType = 0
        isRequired = $false
        helpText = "Optional docker logs --since value."
    },
    [ordered]@{
        key = "maxOutputCharacters"
        label = "Output cap"
        fieldType = 2
        isRequired = $false
        helpText = "Maximum stdout/stderr characters captured."
    }
)
$dockerPermissionPolicy = [ordered]@{
    requiredCapabilities = 448
    approvalRequirement = 2
}
$dockerDeterministicTestMode = [ordered]@{
    isSupported = $true
    description = "Run Preview simulates Docker host-tool output without invoking Docker."
}

function New-DockerWorkflowExecutorManifest {
    param(
        [string] $ExecutorId,
        [string] $Name,
        [string] $Description,
        [int] $TimeoutSeconds,
        [bool] $CaptureOutputArtifact
    )

    return [ordered]@{
        executorId = $ExecutorId
        name = $Name
        description = $Description
        category = 9
        settingsRendererKey = "docker.workflow-settings"
        settingsSchema = [ordered]@{
            version = "1.0"
            fields = $dockerSettingsFields
        }
        inputShape = [ordered]@{
            kind = 0
            schemaJson = ""
            description = "Plain text"
        }
        resultShape = [ordered]@{
            kind = 1
            schemaJson = "{}"
            description = "Docker command JSON result"
        }
        defaultPolicy = [ordered]@{
            timeoutSeconds = $TimeoutSeconds
            maxRetryAttempts = 0
            retryDelayMilliseconds = 250
            captureOutputArtifact = $CaptureOutputArtifact
        }
        permissionPolicy = $dockerPermissionPolicy
        deterministicTestMode = $dockerDeterministicTestMode
    }
}

$manifest = [ordered]@{
    plugin = [ordered]@{
        id = "candoitall.docker"
        displayName = "Docker"
        description = "Provides guarded workflow executors for listing containers, pulling images, starting containers, and reading bounded logs."
        version = "1.0.0"
        vendor = "CanDoItAll"
        sourceKind = 1
        trustLevel = 2
        minAppVersion = "1.0.0"
        capabilities = 513
        workflowExecutors = @(
            New-DockerWorkflowExecutorManifest "docker.list-containers" "Docker containers" "Lists running Docker containers through the guarded Docker plugin." 20 $false
            New-DockerWorkflowExecutorManifest "docker.pull-image" "Docker pull image" "Pulls a validated Docker image through the guarded Docker plugin." 900 $true
            New-DockerWorkflowExecutorManifest "docker.start-container" "Docker start container" "Starts an existing container or creates one from a validated image through the guarded Docker plugin." 120 $true
            New-DockerWorkflowExecutorManifest "docker.read-logs" "Docker logs" "Reads bounded logs from a Docker container through the guarded Docker plugin." 30 $true
        )
        settings = [ordered]@{
            schema = [ordered]@{
                version = "1.0"
                fields = @()
            }
            renderers = @()
        }
        connections = @()
        package = [ordered]@{
            packageId = "candoitall.docker.package"
            version = "1.0.0"
            minAppVersion = "1.0.0"
            sha256 = ""
            signature = ""
            catalogUri = $null
        }
        oauth2 = $null
        icon = [ordered]@{
            kind = 0
            value = "deployed_code"
            packageId = ""
            label = "Docker"
        }
        tags = @("docker", "host-command", "workflow")
    }
    entryAssembly = "CanDoItAll.Plugin.Docker.dll"
    assemblies = @("CanDoItAll.Plugin.Docker.dll")
    iconPath = "icon.svg"
    requiresRestart = $true
}

$manifest | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $manifestPath -Encoding UTF8

@'
<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 64 64" role="img" aria-label="Docker">
  <rect width="64" height="64" rx="10" fill="#0f766e"/>
  <path fill="#ffffff" d="M16 33h32c-.8 8.4-6.8 14-16 14-8.7 0-14.5-5.1-16-14Zm4-14h6v6h-6v-6Zm8 0h6v6h-6v-6Zm8 0h6v6h-6v-6Zm-16 8h6v6h-6v-6Zm8 0h6v6h-6v-6Zm8 0h6v6h-6v-6Zm8 0h6v6h-6v-6Z"/>
</svg>
'@ | Set-Content -LiteralPath $iconPath -Encoding UTF8

if (Test-Path -LiteralPath $OutputPath) {
    Remove-Item -LiteralPath $OutputPath -Force
}

Add-Type -AssemblyName System.IO.Compression
$outputStream = [System.IO.File]::Create($OutputPath)
try {
    $zip = [System.IO.Compression.ZipArchive]::new($outputStream, [System.IO.Compression.ZipArchiveMode]::Create, $false)
    try {
        $fixedTimestamp = [DateTimeOffset]::Parse("2026-05-14T00:00:00Z")
        Get-ChildItem -LiteralPath $stageRoot -File -Recurse |
            Sort-Object FullName |
            ForEach-Object {
                $relativePath = (Get-PackageRelativePath -RootPath $stageRoot -ChildPath $_.FullName).Replace("\", "/")
                $entry = $zip.CreateEntry($relativePath, [System.IO.Compression.CompressionLevel]::Optimal)
                $entry.LastWriteTime = $fixedTimestamp
                $entryStream = $entry.Open()
                try {
                    $fileStream = [System.IO.File]::OpenRead($_.FullName)
                    try {
                        $fileStream.CopyTo($entryStream)
                    }
                    finally {
                        $fileStream.Dispose()
                    }
                }
                finally {
                    $entryStream.Dispose()
                }
            }
    }
    finally {
        $zip.Dispose()
    }
}
finally {
    $outputStream.Dispose()
}

$hash = Get-FileHash -LiteralPath $OutputPath -Algorithm SHA256
[PSCustomObject]@{
    PackagePath = $OutputPath
    Sha256 = $hash.Hash
    Configuration = $Configuration
}
