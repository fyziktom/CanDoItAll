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

$projectPath = Join-Path $RepoRoot "src\plugins\CanDoItAll.Plugin.Docker\CanDoItAll.Plugin.Docker.csproj"
$targetDir = Join-Path $RepoRoot "src\plugins\CanDoItAll.Plugin.Docker\bin\$Configuration\net10.0"
$stageRoot = Join-Path $RepoRoot ".codex\plugin-packages\docker"
$manifestPath = Join-Path $stageRoot "plugin.package.json"
$iconPath = Join-Path $stageRoot "icon.svg"

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

@'
{
  "plugin": {
    "id": "candoitall.docker",
    "displayName": "Docker",
    "description": "Provides guarded workflow executors for listing containers, pulling images, starting containers, and reading bounded logs.",
    "version": "1.0.0",
    "vendor": "CanDoItAll",
    "sourceKind": 1,
    "trustLevel": 2,
    "minAppVersion": "1.0.0",
    "capabilities": 513,
    "workflowExecutors": [
      {
        "executorId": "docker.list-containers",
        "name": "Docker containers",
        "description": "Lists running Docker containers through a constrained docker ps host-tool recipe.",
        "category": 9,
        "settingsRendererKey": "docker.workflow-settings",
        "settingsSchema": { "version": "1.0", "fields": [] },
        "inputShape": { "kind": 0, "schemaJson": "", "description": "Plain text" },
        "resultShape": { "kind": 1, "schemaJson": "{}", "description": "Docker command JSON result" },
        "defaultPolicy": { "timeoutSeconds": 20, "maxRetryAttempts": 0, "retryDelayMilliseconds": 250, "captureOutputArtifact": false }
      },
      {
        "executorId": "docker.pull-image",
        "name": "Docker pull image",
        "description": "Pulls a validated Docker image reference through a constrained docker pull host-tool recipe.",
        "category": 9,
        "settingsRendererKey": "docker.workflow-settings",
        "settingsSchema": { "version": "1.0", "fields": [] },
        "inputShape": { "kind": 0, "schemaJson": "", "description": "Plain text" },
        "resultShape": { "kind": 1, "schemaJson": "{}", "description": "Docker command JSON result" },
        "defaultPolicy": { "timeoutSeconds": 900, "maxRetryAttempts": 0, "retryDelayMilliseconds": 250, "captureOutputArtifact": true }
      },
      {
        "executorId": "docker.start-container",
        "name": "Docker start container",
        "description": "Starts an existing container or creates a container from a validated image through constrained Docker recipes.",
        "category": 9,
        "settingsRendererKey": "docker.workflow-settings",
        "settingsSchema": { "version": "1.0", "fields": [] },
        "inputShape": { "kind": 0, "schemaJson": "", "description": "Plain text" },
        "resultShape": { "kind": 1, "schemaJson": "{}", "description": "Docker command JSON result" },
        "defaultPolicy": { "timeoutSeconds": 120, "maxRetryAttempts": 0, "retryDelayMilliseconds": 250, "captureOutputArtifact": true }
      },
      {
        "executorId": "docker.read-logs",
        "name": "Docker logs",
        "description": "Reads bounded logs from a validated running Docker container.",
        "category": 9,
        "settingsRendererKey": "docker.workflow-settings",
        "settingsSchema": { "version": "1.0", "fields": [] },
        "inputShape": { "kind": 0, "schemaJson": "", "description": "Plain text" },
        "resultShape": { "kind": 1, "schemaJson": "{}", "description": "Docker command JSON result" },
        "defaultPolicy": { "timeoutSeconds": 30, "maxRetryAttempts": 0, "retryDelayMilliseconds": 250, "captureOutputArtifact": true }
      }
    ],
    "settings": { "schema": { "version": "1.0", "fields": [] }, "renderers": [] },
    "connections": [],
    "package": {
      "packageId": "candoitall.docker.package",
      "version": "1.0.0",
      "minAppVersion": "1.0.0",
      "sha256": "",
      "signature": "",
      "catalogUri": null
    },
    "oauth2": null,
    "icon": { "kind": 0, "value": "deployed_code", "packageId": "", "label": "Docker" }
  },
  "entryAssembly": "CanDoItAll.Plugin.Docker.dll",
  "assemblies": [ "CanDoItAll.Plugin.Docker.dll" ],
  "iconPath": "icon.svg",
  "requiresRestart": true
}
'@ | Set-Content -LiteralPath $manifestPath -Encoding UTF8

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
                $relativePath = [System.IO.Path]::GetRelativePath($stageRoot, $_.FullName).Replace("\", "/")
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
