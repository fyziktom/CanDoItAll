[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = "Medium")]
param(
    [string]$InstallRoot = "",
    [int]$Port = 55432,
    [string]$DatabaseName = "candoitall",
    [string]$AppUsername = "candoitall_app"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$script:SchemaVersion = 1
$script:DatabaseHost = "127.0.0.1"
$script:AdminUsername = "candoitall_admin"
$script:DockerContainerName = "candoitall-webapp-db"
$script:DockerVolumeName = "candoitall-webapp-db-data"
$script:DockerImage = "postgres:16.14-alpine@sha256:57c72fd2a128e416c7fcc499958864df5301e940bca0a56f58fddf30ffc07777"
$script:DockerOwnerLabel = "com.candoitall.owner"
$script:DockerOwnerLabelValue = "webapp-install"
$script:DockerSchemaLabel = "com.candoitall.database-schema"
$script:DockerRoleLabel = "com.candoitall.database-role"
$script:DockerStableRoleLabelValue = "stable"
$script:DockerInitializerRoleLabelValue = "initializer"
$script:DockerMemoryLimit = "1g"
$script:DockerMemoryLimitBytes = [int64](1GB)
$script:DockerCpuLimit = "1.0"
$script:DockerNanoCpus = [int64]1000000000
$script:DockerPidsLimit = 256
$script:DockerStopTimeoutSeconds = 60
$script:AppPasswordRelativePath = "secrets\app-password.dpapi"
$script:NativeBinRelativePath = "native\pgsql\bin"
$script:NativeDataRelativePath = "native\data"
$script:NativeLogRelativePath = "native\logs\postgresql.log"
$script:EdBArchiveUri = "https://get.enterprisedb.com/postgresql/postgresql-16.14-2-windows-x64-binaries.zip"
$script:EdBArchiveName = "postgresql-16.14-2-windows-x64-binaries.zip"
$script:EdBArchiveLength = [int64]325741585
$script:EdBArchiveSha256 = "8A7F54C1968D5D49BDCD3F66B1291F736C74B8CB6A26E9874771FCC7837DBF38"
$script:DockerExe = $null

function Write-Status {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    Write-Host "[CanDoItAll Web Database] $Message"
}

function Resolve-ValidatedInstallRoot {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue
    )

    if ([string]::IsNullOrWhiteSpace($PathValue)) {
        throw "InstallRoot cannot be empty."
    }

    if ([System.Management.Automation.WildcardPattern]::ContainsWildcardCharacters($PathValue)) {
        throw "InstallRoot must not contain wildcard characters."
    }

    try {
        $resolved = [System.IO.Path]::GetFullPath($PathValue)
    }
    catch {
        throw "InstallRoot '$PathValue' is not a valid filesystem path."
    }

    $pathRoot = [System.IO.Path]::GetPathRoot($resolved)
    if ([string]::IsNullOrWhiteSpace($pathRoot) -or
        [string]::Equals(
            $resolved.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar),
            $pathRoot.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar),
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "InstallRoot must name a directory below a filesystem root."
    }

    if (Test-Path -LiteralPath $resolved -PathType Leaf) {
        throw "InstallRoot '$resolved' is an existing file."
    }

    Assert-PathHasNoReparsePoints -PathValue $resolved -Description "InstallRoot"

    return $resolved.TrimEnd([System.IO.Path]::DirectorySeparatorChar, [System.IO.Path]::AltDirectorySeparatorChar)
}

function Assert-PathHasNoReparsePoints {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue,
        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $currentPath = [System.IO.Path]::GetFullPath($PathValue)
    while (-not [string]::IsNullOrWhiteSpace($currentPath)) {
        $item = Get-Item -LiteralPath $currentPath -Force -ErrorAction SilentlyContinue
        if ($null -ne $item -and
            ($item.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
            throw "$Description traverses reparse point '$($item.FullName)'. Refusing managed filesystem mutation."
        }

        $parentPath = Split-Path -Parent $currentPath
        if ([string]::IsNullOrWhiteSpace($parentPath) -or
            [string]::Equals($parentPath, $currentPath, [System.StringComparison]::OrdinalIgnoreCase)) {
            break
        }

        $currentPath = $parentPath
    }
}

function Assert-DirectoryTreeHasNoReparsePoints {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PathValue,
        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    Assert-PathHasNoReparsePoints -PathValue $PathValue -Description $Description
    if (-not (Test-Path -LiteralPath $PathValue -PathType Container)) {
        return
    }

    $pendingDirectories = New-Object System.Collections.Generic.Stack[string]
    $pendingDirectories.Push([System.IO.Path]::GetFullPath($PathValue))
    while ($pendingDirectories.Count -gt 0) {
        $currentDirectory = $pendingDirectories.Pop()
        foreach ($child in @(Get-ChildItem -LiteralPath $currentDirectory -Force -ErrorAction Stop)) {
            if (($child.Attributes -band [System.IO.FileAttributes]::ReparsePoint) -ne 0) {
                throw "$Description contains reparse point '$($child.FullName)'. Refusing recursive removal."
            }

            if ($child.PSIsContainer) {
                $pendingDirectories.Push($child.FullName)
            }
        }
    }
}

function Assert-NativeInstallPathSupported {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DatabaseRoot
    )

    $resolved = [System.IO.Path]::GetFullPath($DatabaseRoot)
    if ($resolved.StartsWith("\\", [System.StringComparison]::Ordinal)) {
        throw "The native PostgreSQL fallback requires a local drive path; UNC InstallRoot values are not supported."
    }

    if ($resolved.Length -gt 120) {
        throw "The native PostgreSQL fallback requires the database path to be at most 120 characters so the pinned archive can be extracted safely. Choose a shorter InstallRoot."
    }
}

function Resolve-ContainedRelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Root,
        [Parameter(Mandatory = $true)]
        [string]$RelativePath,
        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    if ([string]::IsNullOrWhiteSpace($RelativePath) -or [System.IO.Path]::IsPathRooted($RelativePath)) {
        throw "$Description must be a non-empty relative path."
    }

    if ([System.Management.Automation.WildcardPattern]::ContainsWildcardCharacters($RelativePath)) {
        throw "$Description must not contain wildcard characters."
    }

    $rootPath = [System.IO.Path]::GetFullPath($Root).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $candidate = [System.IO.Path]::GetFullPath((Join-Path $rootPath $RelativePath))
    $requiredPrefix = $rootPath + [System.IO.Path]::DirectorySeparatorChar

    if (-not $candidate.StartsWith($requiredPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "$Description must remain below '$rootPath'."
    }

    return $candidate
}

function Assert-PostgreSqlIdentifier {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value,
        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    if ($Value -notmatch '^[a-z][a-z0-9_]{0,62}$') {
        throw "$Description '$Value' must start with a lowercase letter, contain only lowercase letters, digits, and underscores, and be at most 63 characters."
    }
}

function Assert-ConfigurationValues {
    param(
        [Parameter(Mandatory = $true)]
        [int]$ConfiguredPort,
        [Parameter(Mandatory = $true)]
        [string]$ConfiguredDatabaseName,
        [Parameter(Mandatory = $true)]
        [string]$ConfiguredAppUsername
    )

    if ($ConfiguredPort -lt 1024 -or $ConfiguredPort -gt 65535) {
        throw "Port must be between 1024 and 65535."
    }

    Assert-PostgreSqlIdentifier -Value $ConfiguredDatabaseName -Description "DatabaseName"
    Assert-PostgreSqlIdentifier -Value $ConfiguredAppUsername -Description "AppUsername"
    Assert-PostgreSqlIdentifier -Value $script:AdminUsername -Description "Administrative role name"

    if ([string]::Equals($ConfiguredAppUsername, $script:AdminUsername, [System.StringComparison]::Ordinal)) {
        throw "AppUsername must be different from the PostgreSQL administrative role."
    }
}

function Get-RequiredPropertyValue {
    param(
        [Parameter(Mandatory = $true)]
        [object]$InputObject,
        [Parameter(Mandatory = $true)]
        [string]$PropertyName,
        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    $property = $InputObject.PSObject.Properties[$PropertyName]
    if ($null -eq $property) {
        throw "$Description is missing required property '$PropertyName'."
    }

    return $property.Value
}

function Read-DatabaseManifest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ManifestPath,
        [Parameter(Mandatory = $true)]
        [string]$DatabaseRoot
    )

    foreach ($managedPath in @($DatabaseRoot, $ManifestPath)) {
        Assert-PathHasNoReparsePoints `
            -PathValue $managedPath `
            -Description "Database manifest read path"
    }

    if (-not (Test-Path -LiteralPath $ManifestPath)) {
        return $null
    }

    if (-not (Test-Path -LiteralPath $ManifestPath -PathType Leaf)) {
        throw "Database manifest '$ManifestPath' is not a file."
    }

    try {
        $manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json
    }
    catch {
        throw "Database manifest '$ManifestPath' is not valid JSON: $($_.Exception.Message)"
    }

    $schemaVersion = [int](Get-RequiredPropertyValue -InputObject $manifest -PropertyName "schemaVersion" -Description "Database manifest")
    if ($schemaVersion -ne $script:SchemaVersion) {
        throw "Database manifest schema version '$schemaVersion' is unsupported; expected '$($script:SchemaVersion)'."
    }

    $engine = [string](Get-RequiredPropertyValue -InputObject $manifest -PropertyName "engine" -Description "Database manifest")
    if ($engine -notin @("docker", "native")) {
        throw "Database manifest engine '$engine' is unsupported."
    }

    $hostName = [string](Get-RequiredPropertyValue -InputObject $manifest -PropertyName "host" -Description "Database manifest")
    if (-not [string]::Equals($hostName, $script:DatabaseHost, [System.StringComparison]::Ordinal)) {
        throw "Database manifest host must be '$($script:DatabaseHost)'."
    }

    $manifestPort = [int](Get-RequiredPropertyValue -InputObject $manifest -PropertyName "port" -Description "Database manifest")
    $manifestDatabaseName = [string](Get-RequiredPropertyValue -InputObject $manifest -PropertyName "databaseName" -Description "Database manifest")
    $manifestAppUsername = [string](Get-RequiredPropertyValue -InputObject $manifest -PropertyName "appUsername" -Description "Database manifest")
    Assert-ConfigurationValues `
        -ConfiguredPort $manifestPort `
        -ConfiguredDatabaseName $manifestDatabaseName `
        -ConfiguredAppUsername $manifestAppUsername

    $appPasswordFile = [string](Get-RequiredPropertyValue -InputObject $manifest -PropertyName "appPasswordFile" -Description "Database manifest")
    if (-not [string]::Equals(
            $appPasswordFile,
            $script:AppPasswordRelativePath,
            [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Database manifest appPasswordFile does not match the managed installed-web-app credential path."
    }

    Resolve-ContainedRelativePath `
        -Root $DatabaseRoot `
        -RelativePath $appPasswordFile `
        -Description "Database manifest appPasswordFile" | Out-Null

    if ($engine -eq "docker") {
        $docker = Get-RequiredPropertyValue -InputObject $manifest -PropertyName "docker" -Description "Docker database manifest"
        if ($null -eq $docker) {
            throw "Docker database manifest metadata cannot be null."
        }

        $containerName = [string](Get-RequiredPropertyValue -InputObject $docker -PropertyName "containerName" -Description "Docker database manifest")
        $volumeName = [string](Get-RequiredPropertyValue -InputObject $docker -PropertyName "volumeName" -Description "Docker database manifest")
        $image = [string](Get-RequiredPropertyValue -InputObject $docker -PropertyName "image" -Description "Docker database manifest")
        if ($containerName -ne $script:DockerContainerName -or
            $volumeName -ne $script:DockerVolumeName -or
            $image -ne $script:DockerImage) {
            throw "Docker database manifest metadata does not match the dedicated installed-web-app resources."
        }
    }
    else {
        $native = Get-RequiredPropertyValue -InputObject $manifest -PropertyName "native" -Description "Native database manifest"
        if ($null -eq $native) {
            throw "Native database manifest metadata cannot be null."
        }

        $expectedNativePaths = @{
            binPath = $script:NativeBinRelativePath
            dataPath = $script:NativeDataRelativePath
            logPath = $script:NativeLogRelativePath
        }
        foreach ($propertyName in @("binPath", "dataPath", "logPath")) {
            $relativePath = [string](Get-RequiredPropertyValue -InputObject $native -PropertyName $propertyName -Description "Native database manifest")
            if (-not [string]::Equals(
                    $relativePath,
                    [string]$expectedNativePaths[$propertyName],
                    [System.StringComparison]::OrdinalIgnoreCase)) {
                throw "Native database manifest $propertyName does not match the managed installed-web-app path."
            }

            Resolve-ContainedRelativePath `
                -Root $DatabaseRoot `
                -RelativePath $relativePath `
                -Description "Native database manifest $propertyName" | Out-Null
        }
    }

    return $manifest
}

function Read-PendingDatabaseState {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    Assert-PathHasNoReparsePoints `
        -PathValue $Path `
        -Description "Pending database state read path"
    if (-not (Test-Path -LiteralPath $Path)) {
        return $null
    }
    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Pending database state '$Path' is not a file."
    }

    try {
        $state = Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    }
    catch {
        throw "Pending database state '$Path' is not valid JSON."
    }

    $schemaVersion = [int](Get-RequiredPropertyValue -InputObject $state -PropertyName "schemaVersion" -Description "Pending database state")
    $engine = [string](Get-RequiredPropertyValue -InputObject $state -PropertyName "engine" -Description "Pending database state")
    $configuredPort = [int](Get-RequiredPropertyValue -InputObject $state -PropertyName "port" -Description "Pending database state")
    $configuredDatabaseName = [string](Get-RequiredPropertyValue -InputObject $state -PropertyName "databaseName" -Description "Pending database state")
    $configuredAppUsername = [string](Get-RequiredPropertyValue -InputObject $state -PropertyName "appUsername" -Description "Pending database state")
    if ($schemaVersion -ne $script:SchemaVersion -or $engine -notin @("docker", "native")) {
        throw "Pending database state '$Path' has an unsupported schema or engine."
    }

    Assert-ConfigurationValues `
        -ConfiguredPort $configuredPort `
        -ConfiguredDatabaseName $configuredDatabaseName `
        -ConfiguredAppUsername $configuredAppUsername
    return $state
}

function Write-PendingDatabaseState {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Engine,
        [Parameter(Mandatory = $true)]
        [int]$ConfiguredPort,
        [Parameter(Mandatory = $true)]
        [string]$ConfiguredDatabaseName,
        [Parameter(Mandatory = $true)]
        [string]$ConfiguredAppUsername
    )

    $state = [ordered]@{
        schemaVersion = $script:SchemaVersion
        engine = $Engine
        port = $ConfiguredPort
        databaseName = $ConfiguredDatabaseName
        appUsername = $ConfiguredAppUsername
    }
    Assert-PathHasNoReparsePoints `
        -PathValue $Path `
        -Description "Pending database state path"
    $temporaryPath = "$Path.new-$([Guid]::NewGuid().ToString('N'))"
    try {
        Write-Utf8NoBomFile `
            -Path $temporaryPath `
            -Content (($state | ConvertTo-Json -Depth 3) + [Environment]::NewLine)
        Assert-PathHasNoReparsePoints `
            -PathValue $Path `
            -Description "Pending database state path"
        Move-Item -LiteralPath $temporaryPath -Destination $Path -Force
    }
    finally {
        Assert-PathHasNoReparsePoints `
            -PathValue $temporaryPath `
            -Description "Pending database state temporary path"
        Remove-Item -LiteralPath $temporaryPath -Force -ErrorAction SilentlyContinue
    }
}

function Find-DockerExecutable {
    $commands = @(Get-Command docker -CommandType Application -All -ErrorAction SilentlyContinue)
    if ($commands.Count -eq 0) {
        return $null
    }

    $command = $commands |
        Where-Object { [string]$_.Source -match '(?i)\.exe$' } |
        Select-Object -First 1
    if ($null -eq $command) {
        $command = $commands[0]
    }

    return [string]$command.Source
}

function Test-LinuxDockerEngine {
    param(
        [string]$DockerPath
    )

    if ([string]::IsNullOrWhiteSpace($DockerPath)) {
        return $false
    }

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        $output = @(& $DockerPath info --format "{{.OSType}}" 2>&1)
        $exitCode = $LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }
    if ($exitCode -ne 0) {
        return $false
    }

    foreach ($line in $output) {
        if ([string]::Equals(
                ([string]$line).Trim(),
                "linux",
                [System.StringComparison]::OrdinalIgnoreCase)) {
            return $true
        }
    }

    return $false
}

function Invoke-ExternalProbe {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    $previousErrorActionPreference = $ErrorActionPreference
    try {
        $ErrorActionPreference = "Continue"
        $output = @(& $FilePath @Arguments 2>&1)
        $exitCode = [int]$LASTEXITCODE
    }
    finally {
        $ErrorActionPreference = $previousErrorActionPreference
    }

    return [pscustomobject]@{
        ExitCode = $exitCode
        Output = $output
    }
}

function Invoke-CheckedExternal {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FilePath,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [int[]]$AllowedExitCodes = @(0),
        [switch]$SensitiveOutput
    )

    $result = Invoke-ExternalProbe -FilePath $FilePath -Arguments $Arguments
    if ($result.ExitCode -notin $AllowedExitCodes) {
        $summary = "Command failed with exit code $($result.ExitCode): $FilePath $($Arguments -join ' ')"
        if (-not $SensitiveOutput.IsPresent -and $result.Output.Count -gt 0) {
            $summary += [Environment]::NewLine + (($result.Output | ForEach-Object { [string]$_ }) -join [Environment]::NewLine)
        }

        throw $summary
    }

    return $result.Output
}

function Invoke-DockerProbe {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    return Invoke-ExternalProbe -FilePath $script:DockerExe -Arguments $Arguments
}

function Invoke-CheckedDocker {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [int[]]$AllowedExitCodes = @(0),
        [switch]$SensitiveOutput
    )

    return Invoke-CheckedExternal `
        -FilePath $script:DockerExe `
        -Arguments $Arguments `
        -AllowedExitCodes $AllowedExitCodes `
        -SensitiveOutput:$SensitiveOutput
}

function Test-DockerMissingObjectOutput {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Output
    )

    $message = ($Output | ForEach-Object { [string]$_ }) -join [Environment]::NewLine
    return $message -match '(?i)no such (object|container|volume)'
}

function Test-DockerContainerExists {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $result = Invoke-DockerProbe -Arguments @("container", "inspect", $Name)
    if ($result.ExitCode -eq 0) {
        return $true
    }

    if ($result.ExitCode -eq 1 -and (Test-DockerMissingObjectOutput -Output $result.Output)) {
        return $false
    }

    throw "Docker could not inspect container '$Name' (exit code $($result.ExitCode))."
}

function Test-DockerVolumeExists {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $result = Invoke-DockerProbe -Arguments @("volume", "inspect", $Name)
    if ($result.ExitCode -eq 0) {
        return $true
    }

    if ($result.ExitCode -eq 1 -and (Test-DockerMissingObjectOutput -Output $result.Output)) {
        return $false
    }

    throw "Docker could not inspect volume '$Name' (exit code $($result.ExitCode))."
}

function ConvertFrom-DockerJson {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Output,
        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    try {
        $parsed = (($Output | ForEach-Object { [string]$_ }) -join [Environment]::NewLine) | ConvertFrom-Json
        $items = @($parsed)
        if ($items.Count -ne 1) {
            throw "Expected exactly one object."
        }

        return $items[0]
    }
    catch {
        throw "Docker returned invalid inspection data for $Description."
    }
}

function Get-DockerContainerInspect {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $output = @(Invoke-CheckedDocker -Arguments @("container", "inspect", $Name))
    return ConvertFrom-DockerJson -Output $output -Description "container '$Name'"
}

function Get-DockerVolumeInspect {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $output = @(Invoke-CheckedDocker -Arguments @("volume", "inspect", $Name))
    return ConvertFrom-DockerJson -Output $output -Description "volume '$Name'"
}

function Get-ObjectPropertyText {
    param(
        [object]$InputObject,
        [Parameter(Mandatory = $true)]
        [string]$PropertyName
    )

    if ($null -eq $InputObject) {
        return ""
    }

    $property = $InputObject.PSObject.Properties[$PropertyName]
    if ($null -eq $property -or $null -eq $property.Value) {
        return ""
    }

    return [string]$property.Value
}

function Assert-ManagedDockerVolume {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Inspect
    )

    $owner = Get-ObjectPropertyText -InputObject $Inspect.Labels -PropertyName $script:DockerOwnerLabel
    $schema = Get-ObjectPropertyText -InputObject $Inspect.Labels -PropertyName $script:DockerSchemaLabel
    if ($owner -ne $script:DockerOwnerLabelValue -or $schema -ne [string]$script:SchemaVersion) {
        throw "Docker volume '$($script:DockerVolumeName)' already exists but is not owned by this installed-web-app database setup."
    }
}

function Assert-ManagedDockerContainer {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Inspect,
        [Parameter(Mandatory = $true)]
        [int]$ConfiguredPort
    )

    $owner = Get-ObjectPropertyText -InputObject $Inspect.Config.Labels -PropertyName $script:DockerOwnerLabel
    $schema = Get-ObjectPropertyText -InputObject $Inspect.Config.Labels -PropertyName $script:DockerSchemaLabel
    $role = Get-ObjectPropertyText -InputObject $Inspect.Config.Labels -PropertyName $script:DockerRoleLabel
    if ($owner -ne $script:DockerOwnerLabelValue -or
        $schema -ne [string]$script:SchemaVersion -or
        $role -ne $script:DockerStableRoleLabelValue) {
        throw "Docker container '$($script:DockerContainerName)' already exists but is not owned by this installed-web-app database setup."
    }

    if ([string]$Inspect.Config.Image -ne $script:DockerImage) {
        throw "Docker container '$($script:DockerContainerName)' uses image '$($Inspect.Config.Image)' instead of '$($script:DockerImage)'."
    }

    $passwordEnvironment = @($Inspect.Config.Env | Where-Object { [string]$_ -match '^POSTGRES_PASSWORD(_FILE)?=' })
    if ($passwordEnvironment.Count -gt 0) {
        throw "Stable Docker container '$($script:DockerContainerName)' retains a PostgreSQL password environment setting and must not be adopted."
    }

    $memory = Get-ObjectPropertyText -InputObject $Inspect.HostConfig -PropertyName "Memory"
    $nanoCpus = Get-ObjectPropertyText -InputObject $Inspect.HostConfig -PropertyName "NanoCpus"
    $pidsLimit = Get-ObjectPropertyText -InputObject $Inspect.HostConfig -PropertyName "PidsLimit"
    $stopTimeout = Get-ObjectPropertyText -InputObject $Inspect.Config -PropertyName "StopTimeout"
    if ([int64]$memory -ne $script:DockerMemoryLimitBytes -or
        [int64]$nanoCpus -ne $script:DockerNanoCpus -or
        [int64]$pidsLimit -ne [int64]$script:DockerPidsLimit -or
        [int]$stopTimeout -ne $script:DockerStopTimeoutSeconds) {
        throw "Docker container '$($script:DockerContainerName)' does not use the expected memory, CPU, PID, and stop-timeout limits."
    }

    $restartPolicyName = Get-ObjectPropertyText `
        -InputObject $Inspect.HostConfig.RestartPolicy `
        -PropertyName "Name"
    $logDriver = Get-ObjectPropertyText -InputObject $Inspect.HostConfig.LogConfig -PropertyName "Type"
    $logMaxSize = Get-ObjectPropertyText -InputObject $Inspect.HostConfig.LogConfig.Config -PropertyName "max-size"
    $logMaxFile = Get-ObjectPropertyText -InputObject $Inspect.HostConfig.LogConfig.Config -PropertyName "max-file"
    if ($restartPolicyName -ne "unless-stopped" -or
        $logDriver -ne "local" -or
        $logMaxSize -ne "10m" -or
        $logMaxFile -ne "3") {
        throw "Docker container '$($script:DockerContainerName)' does not use the expected restart or bounded logging policy."
    }

    $dataMount = @($Inspect.Mounts | Where-Object {
        [string]$_.Type -eq "volume" -and
        [string]$_.Name -eq $script:DockerVolumeName -and
        [string]$_.Destination -eq "/var/lib/postgresql/data" -and
        [bool]$_.RW
    })
    if ($dataMount.Count -ne 1) {
        throw "Docker container '$($script:DockerContainerName)' is not attached to the expected dedicated database volume."
    }

    $portProperty = $Inspect.HostConfig.PortBindings.PSObject.Properties["5432/tcp"]
    if ($null -eq $portProperty -or $null -eq $portProperty.Value) {
        throw "Docker container '$($script:DockerContainerName)' does not publish PostgreSQL on loopback."
    }

    $bindings = @($portProperty.Value)
    if ($bindings.Count -ne 1 -or
        [string]$bindings[0].HostIp -ne $script:DatabaseHost -or
        [string]$bindings[0].HostPort -ne [string]$ConfiguredPort) {
        throw "Docker container '$($script:DockerContainerName)' does not use the expected $($script:DatabaseHost):$ConfiguredPort publication."
    }
}

function Assert-ManagedDockerInitializerContainer {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Inspect,
        [Parameter(Mandatory = $true)]
        [string]$ContainerIdentifier
    )

    $owner = Get-ObjectPropertyText -InputObject $Inspect.Config.Labels -PropertyName $script:DockerOwnerLabel
    $schema = Get-ObjectPropertyText -InputObject $Inspect.Config.Labels -PropertyName $script:DockerSchemaLabel
    $role = Get-ObjectPropertyText -InputObject $Inspect.Config.Labels -PropertyName $script:DockerRoleLabel
    if ($owner -ne $script:DockerOwnerLabelValue -or
        $schema -ne [string]$script:SchemaVersion -or
        $role -ne $script:DockerInitializerRoleLabelValue) {
        throw "Refusing to remove Docker container '$ContainerIdentifier' because it is not an exact installed-web-app database initializer match."
    }

    $expectedNamePattern = '^/' + [regex]::Escape($script:DockerContainerName) + '-init-[a-f0-9]{8}$'
    if ([string]$Inspect.Name -cnotmatch $expectedNamePattern) {
        throw "Refusing to remove Docker container '$ContainerIdentifier' because its name is not an exact installed-web-app database initializer match."
    }

    $dataMount = @($Inspect.Mounts | Where-Object {
        [string]$_.Type -eq "volume" -and
        [string]$_.Name -eq $script:DockerVolumeName -and
        [string]$_.Destination -eq "/var/lib/postgresql/data" -and
        [bool]$_.RW
    })
    if ($dataMount.Count -ne 1) {
        throw "Refusing to remove Docker container '$ContainerIdentifier' because it does not have exactly one expected writable database-volume mount."
    }
}

function Remove-ManagedDockerInitializerContainer {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ContainerIdentifier
    )

    if (-not (Test-DockerContainerExists -Name $ContainerIdentifier)) {
        return
    }

    $inspect = Get-DockerContainerInspect -Name $ContainerIdentifier
    Assert-ManagedDockerInitializerContainer `
        -Inspect $inspect `
        -ContainerIdentifier $ContainerIdentifier

    $managedContainerId = ([string]$inspect.Id).Trim()
    if ($managedContainerId -notmatch '^[a-f0-9]{64}$') {
        throw "Refusing to remove Docker container '$ContainerIdentifier' because Docker returned an invalid immutable container ID."
    }

    if ([bool]$inspect.State.Running) {
        Write-Status "Stopping Docker database initializer '$ContainerIdentifier'"
        Invoke-CheckedDocker -Arguments @(
            "stop",
            "--time",
            [string]$script:DockerStopTimeoutSeconds,
            $managedContainerId
        ) | Out-Null
    }

    Write-Status "Removing Docker database initializer '$ContainerIdentifier'"
    Invoke-CheckedDocker -Arguments @("rm", $managedContainerId) | Out-Null
}

function Remove-StaleDockerInitializerContainers {
    $output = @(Invoke-CheckedDocker -Arguments @(
        "container",
        "ls",
        "--all",
        "--quiet",
        "--filter",
        "label=$($script:DockerOwnerLabel)=$($script:DockerOwnerLabelValue)",
        "--filter",
        "label=$($script:DockerSchemaLabel)=$($script:SchemaVersion)",
        "--filter",
        "label=$($script:DockerRoleLabel)=$($script:DockerInitializerRoleLabelValue)"
    ))

    $containerIdentifiers = @($output |
        ForEach-Object { ([string]$_).Trim() } |
        Where-Object { $_ -match '^[a-f0-9]{12,64}$' } |
        Select-Object -Unique)
    foreach ($containerIdentifier in $containerIdentifiers) {
        Remove-ManagedDockerInitializerContainer -ContainerIdentifier $containerIdentifier
    }
}

function Get-DockerVolumeState {
    $probeScript = 'if [ -f /var/lib/postgresql/data/PG_VERSION ]; then exit 0; fi; if [ -z "$(find /var/lib/postgresql/data -mindepth 1 -maxdepth 1 -print -quit)" ]; then exit 10; fi; exit 20'
    $result = Invoke-DockerProbe -Arguments @(
        "run",
        "--rm",
        "--mount",
        "type=volume,source=$($script:DockerVolumeName),target=/var/lib/postgresql/data,readonly",
        "--entrypoint",
        "sh",
        $script:DockerImage,
        "-ec",
        $probeScript
    )

    switch ($result.ExitCode) {
        0 { return "Initialized" }
        10 { return "Empty" }
        20 { return "Partial" }
        default { throw "Docker could not inspect the installed-web-app database volume (exit code $($result.ExitCode))." }
    }
}

function Wait-DockerDatabaseReady {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ContainerName,
        [int]$TimeoutSeconds = 120
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $probe = Invoke-DockerProbe -Arguments @(
            "exec",
            $ContainerName,
            "pg_isready",
            "-h",
            "127.0.0.1",
            "-p",
            "5432",
            "-d",
            "postgres"
        )

        if ($probe.ExitCode -eq 0) {
            return
        }

        $inspect = Get-DockerContainerInspect -Name $ContainerName
        if (-not [bool]$inspect.State.Running) {
            throw "Docker container '$ContainerName' exited before PostgreSQL became ready. Inspect it with 'docker logs $ContainerName'."
        }

        Start-Sleep -Seconds 1
    }

    throw "PostgreSQL in Docker container '$ContainerName' did not become ready within $TimeoutSeconds seconds."
}

function Set-RestrictedAcl {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [bool]$IsDirectory
    )

    Assert-PathHasNoReparsePoints `
        -PathValue $Path `
        -Description "Managed ACL target"

    $currentSid = [System.Security.Principal.WindowsIdentity]::GetCurrent().User
    $systemSid = New-Object System.Security.Principal.SecurityIdentifier(
        [System.Security.Principal.WellKnownSidType]::LocalSystemSid,
        $null)

    if ($IsDirectory) {
        $acl = New-Object System.Security.AccessControl.DirectorySecurity
        $inheritance = [System.Security.AccessControl.InheritanceFlags]::ContainerInherit -bor
            [System.Security.AccessControl.InheritanceFlags]::ObjectInherit
    }
    else {
        $acl = New-Object System.Security.AccessControl.FileSecurity
        $inheritance = [System.Security.AccessControl.InheritanceFlags]::None
    }

    $acl.SetAccessRuleProtection($true, $false)
    foreach ($sid in @($currentSid, $systemSid)) {
        $rule = New-Object System.Security.AccessControl.FileSystemAccessRule(
            $sid,
            [System.Security.AccessControl.FileSystemRights]::FullControl,
            $inheritance,
            [System.Security.AccessControl.PropagationFlags]::None,
            [System.Security.AccessControl.AccessControlType]::Allow)
        [void]$acl.AddAccessRule($rule)
    }

    Set-Acl -LiteralPath $Path -AclObject $acl
}

function Write-Utf8NoBomFile {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$Content
    )

    Assert-PathHasNoReparsePoints `
        -PathValue $Path `
        -Description "Managed file write target"

    $encoding = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Content, $encoding)
}

function New-StrongSecureString {
    $bytes = New-Object byte[] 32
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try {
        $rng.GetBytes($bytes)
        $plainText = [Convert]::ToBase64String($bytes)
        return ConvertTo-SecureString -String $plainText -AsPlainText -Force
    }
    finally {
        [Array]::Clear($bytes, 0, $bytes.Length)
        $rng.Dispose()
        $plainText = $null
    }
}

function Save-DpapiSecret {
    param(
        [Parameter(Mandatory = $true)]
        [Security.SecureString]$Secret,
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $protected = ConvertFrom-SecureString -SecureString $Secret
    Write-Utf8NoBomFile -Path $Path -Content ($protected + [Environment]::NewLine)
    Set-RestrictedAcl -Path $Path -IsDirectory $false
}

function Read-DpapiSecret {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    Assert-PathHasNoReparsePoints `
        -PathValue $Path `
        -Description "Protected database credential path"
    try {
        $protected = (Get-Content -LiteralPath $Path -Raw).Trim()
        if ([string]::IsNullOrWhiteSpace($protected)) {
            throw "Protected secret is empty."
        }

        return ConvertTo-SecureString -String $protected
    }
    catch {
        throw "Protected credential '$Path' cannot be decrypted by the current Windows user."
    }
}

function Get-OrCreateProtectedSecrets {
    param(
        [Parameter(Mandatory = $true)]
        [string]$SecretRoot,
        [Parameter(Mandatory = $true)]
        [string]$AppPasswordPath,
        [Parameter(Mandatory = $true)]
        [string]$AdminPasswordPath,
        [Parameter(Mandatory = $true)]
        [bool]$DatabaseAlreadyExists
    )

    foreach ($managedPath in @($SecretRoot, $AppPasswordPath, $AdminPasswordPath)) {
        Assert-PathHasNoReparsePoints `
            -PathValue $managedPath `
            -Description "Managed database credential path"
    }

    $appExists = Test-Path -LiteralPath $AppPasswordPath -PathType Leaf
    $adminExists = Test-Path -LiteralPath $AdminPasswordPath -PathType Leaf
    if ($appExists -xor $adminExists) {
        throw "The installed database credential set is incomplete. Restore both protected credential files or remove the incomplete new installation."
    }

    if ($appExists -and $adminExists) {
        return [pscustomobject]@{
            App = Read-DpapiSecret -Path $AppPasswordPath
            Admin = Read-DpapiSecret -Path $AdminPasswordPath
        }
    }

    if ($DatabaseAlreadyExists) {
        throw "The database already exists, but its current-user protected credentials are missing. Refusing to generate replacement credentials that cannot authenticate to existing data."
    }

    Assert-PathHasNoReparsePoints `
        -PathValue $SecretRoot `
        -Description "Managed database credential directory"
    New-Item -ItemType Directory -Path $SecretRoot -Force | Out-Null
    Set-RestrictedAcl -Path $SecretRoot -IsDirectory $true

    $appSecret = New-StrongSecureString
    $adminSecret = New-StrongSecureString
    Save-DpapiSecret -Secret $appSecret -Path $AppPasswordPath
    Save-DpapiSecret -Secret $adminSecret -Path $AdminPasswordPath

    return [pscustomobject]@{
        App = $appSecret
        Admin = $adminSecret
    }
}

function ConvertFrom-SecureStringToPlainText {
    param(
        [Parameter(Mandatory = $true)]
        [Security.SecureString]$Secret
    )

    $pointer = [IntPtr]::Zero
    try {
        $pointer = [Runtime.InteropServices.Marshal]::SecureStringToBSTR($Secret)
        return [Runtime.InteropServices.Marshal]::PtrToStringBSTR($pointer)
    }
    finally {
        if ($pointer -ne [IntPtr]::Zero) {
            [Runtime.InteropServices.Marshal]::ZeroFreeBSTR($pointer)
        }
    }
}

function Quote-PostgreSqlIdentifier {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    return '"' + $Value.Replace('"', '""') + '"'
}

function Quote-PostgreSqlLiteral {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    return "'" + $Value.Replace("'", "''") + "'"
}

function New-ProvisioningSql {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ConfiguredDatabaseName,
        [Parameter(Mandatory = $true)]
        [string]$ConfiguredAppUsername,
        [Parameter(Mandatory = $true)]
        [string]$AppPassword
    )

    $template = @'
\set ON_ERROR_STOP on
SET password_encryption = 'scram-sha-256';

DO $candoitall$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = __APP_ROLE_LITERAL__) THEN
        CREATE ROLE __APP_ROLE_IDENTIFIER__;
    END IF;
END
$candoitall$;

ALTER ROLE __APP_ROLE_IDENTIFIER__ WITH
    LOGIN
    NOSUPERUSER
    NOCREATEDB
    NOCREATEROLE
    INHERIT
    NOREPLICATION
    NOBYPASSRLS
    PASSWORD __APP_PASSWORD_LITERAL__;

DO $candoitall$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_authid
        WHERE rolname = __APP_ROLE_LITERAL__
          AND rolpassword LIKE 'SCRAM-SHA-256$%'
    ) THEN
        RAISE EXCEPTION 'The application role password was not stored as SCRAM-SHA-256.';
    END IF;
END
$candoitall$;

SELECT format('CREATE DATABASE %I OWNER %I', __DATABASE_LITERAL__, __APP_ROLE_LITERAL__)
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = __DATABASE_LITERAL__)
\gexec

ALTER DATABASE __DATABASE_IDENTIFIER__ OWNER TO __APP_ROLE_IDENTIFIER__;
\connect __DATABASE_RAW__
ALTER SCHEMA public OWNER TO __APP_ROLE_IDENTIFIER__;
GRANT ALL ON SCHEMA public TO __APP_ROLE_IDENTIFIER__;
'@

    return $template.
        Replace("__APP_ROLE_LITERAL__", (Quote-PostgreSqlLiteral -Value $ConfiguredAppUsername)).
        Replace("__APP_ROLE_IDENTIFIER__", (Quote-PostgreSqlIdentifier -Value $ConfiguredAppUsername)).
        Replace("__APP_PASSWORD_LITERAL__", (Quote-PostgreSqlLiteral -Value $AppPassword)).
        Replace("__DATABASE_LITERAL__", (Quote-PostgreSqlLiteral -Value $ConfiguredDatabaseName)).
        Replace("__DATABASE_IDENTIFIER__", (Quote-PostgreSqlIdentifier -Value $ConfiguredDatabaseName)).
        Replace("__DATABASE_RAW__", $ConfiguredDatabaseName)
}

function New-PlaintextCredentialFiles {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DatabaseRoot,
        [Parameter(Mandatory = $true)]
        [Security.SecureString]$AppSecret,
        [Parameter(Mandatory = $true)]
        [Security.SecureString]$AdminSecret,
        [Parameter(Mandatory = $true)]
        [string]$ConfiguredDatabaseName,
        [Parameter(Mandatory = $true)]
        [string]$ConfiguredAppUsername,
        [Parameter(Mandatory = $true)]
        [int]$ServerPort
    )

    Assert-PathHasNoReparsePoints `
        -PathValue $DatabaseRoot `
        -Description "Managed database root"
    $temporaryRoot = Join-Path $DatabaseRoot (".temporary-" + [Guid]::NewGuid().ToString("N"))
    Assert-PathHasNoReparsePoints `
        -PathValue $temporaryRoot `
        -Description "Plaintext credential directory"
    New-Item -ItemType Directory -Path $temporaryRoot | Out-Null
    Set-RestrictedAcl -Path $temporaryRoot -IsDirectory $true

    $adminPasswordPath = Join-Path $temporaryRoot "admin-password"
    $adminPgPassPath = Join-Path $temporaryRoot "admin.pgpass"
    $appPgPassPath = Join-Path $temporaryRoot "app.pgpass"
    $provisioningSqlPath = Join-Path $temporaryRoot "provision.sql"
    $adminPlainText = $null
    $appPlainText = $null

    try {
        $adminPlainText = ConvertFrom-SecureStringToPlainText -Secret $AdminSecret
        $appPlainText = ConvertFrom-SecureStringToPlainText -Secret $AppSecret

        Write-Utf8NoBomFile -Path $adminPasswordPath -Content ($adminPlainText + [Environment]::NewLine)
        Write-Utf8NoBomFile `
            -Path $adminPgPassPath `
            -Content ("$($script:DatabaseHost):${ServerPort}:*:$($script:AdminUsername):$adminPlainText" + [Environment]::NewLine)
        Write-Utf8NoBomFile `
            -Path $appPgPassPath `
            -Content ("$($script:DatabaseHost):${ServerPort}:$ConfiguredDatabaseName`:$ConfiguredAppUsername`:$appPlainText" + [Environment]::NewLine)
        Write-Utf8NoBomFile `
            -Path $provisioningSqlPath `
            -Content (New-ProvisioningSql `
                -ConfiguredDatabaseName $ConfiguredDatabaseName `
                -ConfiguredAppUsername $ConfiguredAppUsername `
                -AppPassword $appPlainText)

        foreach ($path in @($adminPasswordPath, $adminPgPassPath, $appPgPassPath, $provisioningSqlPath)) {
            Set-RestrictedAcl -Path $path -IsDirectory $false
        }

        return [pscustomobject]@{
            Root = $temporaryRoot
            AdminPassword = $adminPasswordPath
            AdminPgPass = $adminPgPassPath
            AppPgPass = $appPgPassPath
            ProvisioningSql = $provisioningSqlPath
        }
    }
    catch {
        Remove-SafeTemporaryDirectory -Path $temporaryRoot -DatabaseRoot $DatabaseRoot
        throw
    }
    finally {
        $adminPlainText = $null
        $appPlainText = $null
    }
}

function Remove-SafeTemporaryDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [Parameter(Mandatory = $true)]
        [string]$DatabaseRoot
    )

    if (-not (Test-Path -LiteralPath $Path)) {
        return
    }

    $rootPath = [System.IO.Path]::GetFullPath($DatabaseRoot).TrimEnd(
        [System.IO.Path]::DirectorySeparatorChar,
        [System.IO.Path]::AltDirectorySeparatorChar)
    $targetPath = [System.IO.Path]::GetFullPath($Path)
    $requiredPrefix = $rootPath + [System.IO.Path]::DirectorySeparatorChar
    $leafName = Split-Path -Leaf $targetPath
    if (-not $targetPath.StartsWith($requiredPrefix, [System.StringComparison]::OrdinalIgnoreCase) -or
        $leafName -notmatch '^\.temporary-[a-f0-9]{32}$') {
        throw "Refusing to remove unexpected temporary directory '$targetPath'."
    }

    Assert-DirectoryTreeHasNoReparsePoints `
        -PathValue $targetPath `
        -Description "Plaintext credential cleanup target"
    Remove-Item -LiteralPath $targetPath -Recurse -Force
}

function Remove-StalePlaintextCredentialDirectories {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DatabaseRoot
    )

    Assert-PathHasNoReparsePoints `
        -PathValue $DatabaseRoot `
        -Description "Managed database root before credential cleanup"
    Get-ChildItem -LiteralPath $DatabaseRoot -Directory -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^\.temporary-[a-f0-9]{32}$' } |
        ForEach-Object {
            Write-Status "Removing credentials left by an interrupted database setup"
            Remove-SafeTemporaryDirectory -Path $_.FullName -DatabaseRoot $DatabaseRoot
        }
}

function Invoke-DockerClientPsql {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ServerContainerName,
        [Parameter(Mandatory = $true)]
        [string]$TemporaryRoot,
        [Parameter(Mandatory = $true)]
        [ValidateSet("admin", "app")]
        [string]$Credential,
        [Parameter(Mandatory = $true)]
        [string[]]$PsqlArguments,
        [switch]$SensitiveOutput
    )

    if ($TemporaryRoot.Contains(',')) {
        throw "InstallRoot cannot contain a comma when Docker bind mounts are required."
    }

    $pgPassName = if ($Credential -eq "admin") { "admin.pgpass" } else { "app.pgpass" }
    $shellCommand = "cp /input/$pgPassName /tmp/pgpass; chmod 600 /tmp/pgpass; export PGPASSFILE=/tmp/pgpass; exec psql `"`$@`""
    $arguments = @(
        "run",
        "--rm",
        "--network",
        "container:$ServerContainerName",
        "--mount",
        "type=bind,source=$TemporaryRoot,target=/input,readonly",
        "--entrypoint",
        "sh",
        $script:DockerImage,
        "-ec",
        $shellCommand,
        "psql"
    ) + $PsqlArguments

    return Invoke-CheckedDocker -Arguments $arguments -SensitiveOutput:$SensitiveOutput
}

function Initialize-DockerVolume {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TemporaryRoot,
        [Parameter(Mandatory = $true)]
        [string]$ConfiguredDatabaseName,
        [Parameter(Mandatory = $true)]
        [string]$ConfiguredAppUsername
    )

    if ($TemporaryRoot.Contains(',')) {
        throw "InstallRoot cannot contain a comma when Docker bind mounts are required."
    }

    $transientName = "$($script:DockerContainerName)-init-$([Guid]::NewGuid().ToString('N').Substring(0, 8))"
    try {
        Write-Status "Initializing the dedicated Docker PostgreSQL volume"
        Invoke-CheckedDocker -Arguments @(
            "run",
            "-d",
            "--name",
            $transientName,
            "--label",
            "$($script:DockerOwnerLabel)=$($script:DockerOwnerLabelValue)",
            "--label",
            "$($script:DockerSchemaLabel)=$($script:SchemaVersion)",
            "--label",
            "$($script:DockerRoleLabel)=$($script:DockerInitializerRoleLabelValue)",
            "--stop-timeout",
            [string]$script:DockerStopTimeoutSeconds,
            "--mount",
            "type=volume,source=$($script:DockerVolumeName),target=/var/lib/postgresql/data",
            "--mount",
            "type=bind,source=$TemporaryRoot,target=/input,readonly",
            "--env",
            "POSTGRES_USER=$($script:AdminUsername)",
            "--env",
            "POSTGRES_DB=postgres",
            "--env",
            "POSTGRES_PASSWORD_FILE=/input/admin-password",
            "--env",
            "POSTGRES_INITDB_ARGS=--auth-host=scram-sha-256 --auth-local=scram-sha-256 --encoding=UTF8",
            $script:DockerImage,
            "postgres",
            "-c",
            "password_encryption=scram-sha-256"
        ) | Out-Null

        Wait-DockerDatabaseReady -ContainerName $transientName
        Invoke-DockerClientPsql `
            -ServerContainerName $transientName `
            -TemporaryRoot $TemporaryRoot `
            -Credential admin `
            -PsqlArguments @(
                "-X",
                "-w",
                "-h",
                "127.0.0.1",
                "-p",
                "5432",
                "-U",
                $script:AdminUsername,
                "-d",
                "postgres",
                "-f",
                "/input/provision.sql"
            ) `
            -SensitiveOutput | Out-Null
    }
    finally {
        Remove-ManagedDockerInitializerContainer -ContainerIdentifier $transientName
    }
}

function Start-OrCreateStableDockerContainer {
    param(
        [Parameter(Mandatory = $true)]
        [int]$ConfiguredPort
    )

    if (Test-DockerContainerExists -Name $script:DockerContainerName) {
        $inspect = Get-DockerContainerInspect -Name $script:DockerContainerName
        Assert-ManagedDockerContainer -Inspect $inspect -ConfiguredPort $ConfiguredPort
        if (-not [bool]$inspect.State.Running) {
            Write-Status "Starting Docker container '$($script:DockerContainerName)'"
            Invoke-CheckedDocker -Arguments @("start", $script:DockerContainerName) | Out-Null
        }
    }
    else {
        Write-Status "Creating stable Docker container '$($script:DockerContainerName)'"
        Invoke-CheckedDocker -Arguments @(
            "run",
            "-d",
            "--name",
            $script:DockerContainerName,
            "--label",
            "$($script:DockerOwnerLabel)=$($script:DockerOwnerLabelValue)",
            "--label",
            "$($script:DockerSchemaLabel)=$($script:SchemaVersion)",
            "--label",
            "$($script:DockerRoleLabel)=$($script:DockerStableRoleLabelValue)",
            "--restart",
            "unless-stopped",
            "--stop-timeout",
            [string]$script:DockerStopTimeoutSeconds,
            "--memory",
            $script:DockerMemoryLimit,
            "--cpus",
            $script:DockerCpuLimit,
            "--pids-limit",
            [string]$script:DockerPidsLimit,
            "--publish",
            "$($script:DatabaseHost):${ConfiguredPort}:5432",
            "--mount",
            "type=volume,source=$($script:DockerVolumeName),target=/var/lib/postgresql/data",
            "--health-cmd",
            "pg_isready -h 127.0.0.1 -p 5432 -d postgres",
            "--health-interval",
            "5s",
            "--health-timeout",
            "5s",
            "--health-retries",
            "24",
            "--health-start-period",
            "10s",
            "--log-driver",
            "local",
            "--log-opt",
            "max-size=10m",
            "--log-opt",
            "max-file=3",
            $script:DockerImage,
            "postgres",
            "-c",
            "password_encryption=scram-sha-256"
        ) | Out-Null
    }

    Wait-DockerDatabaseReady -ContainerName $script:DockerContainerName
    $stableInspect = Get-DockerContainerInspect -Name $script:DockerContainerName
    Assert-ManagedDockerContainer -Inspect $stableInspect -ConfiguredPort $ConfiguredPort
}

function Invoke-DockerDatabaseSetup {
    param(
        [Parameter(Mandatory = $true)]
        [string]$TemporaryRoot,
        [Parameter(Mandatory = $true)]
        [int]$ConfiguredPort,
        [Parameter(Mandatory = $true)]
        [string]$ConfiguredDatabaseName,
        [Parameter(Mandatory = $true)]
        [string]$ConfiguredAppUsername,
        [Parameter(Mandatory = $true)]
        [bool]$ManifestExists
    )

    Remove-StaleDockerInitializerContainers

    $volumeExists = Test-DockerVolumeExists -Name $script:DockerVolumeName
    $containerExists = Test-DockerContainerExists -Name $script:DockerContainerName

    if ($ManifestExists -and -not $volumeExists) {
        throw "The Docker database manifest exists, but dedicated volume '$($script:DockerVolumeName)' is missing. Refusing to create an empty replacement."
    }

    if ($containerExists -and -not $volumeExists) {
        throw "Dedicated Docker container '$($script:DockerContainerName)' exists without its expected volume."
    }

    if ($volumeExists) {
        Assert-ManagedDockerVolume -Inspect (Get-DockerVolumeInspect -Name $script:DockerVolumeName)
    }
    else {
        Write-Status "Creating Docker volume '$($script:DockerVolumeName)'"
        Invoke-CheckedDocker -Arguments @(
            "volume",
            "create",
            "--label",
            "$($script:DockerOwnerLabel)=$($script:DockerOwnerLabelValue)",
            "--label",
            "$($script:DockerSchemaLabel)=$($script:SchemaVersion)",
            $script:DockerVolumeName
        ) | Out-Null
        $volumeExists = $true
    }

    if ($containerExists) {
        Assert-ManagedDockerContainer `
            -Inspect (Get-DockerContainerInspect -Name $script:DockerContainerName) `
            -ConfiguredPort $ConfiguredPort
    }

    $volumeState = Get-DockerVolumeState
    if ($volumeState -eq "Partial") {
        throw "Docker volume '$($script:DockerVolumeName)' contains a partial PostgreSQL initialization. Refusing to overwrite potentially recoverable data."
    }

    if ($volumeState -eq "Empty") {
        if ($containerExists) {
            $inspect = Get-DockerContainerInspect -Name $script:DockerContainerName
            if ([bool]$inspect.State.Running) {
                throw "Docker container '$($script:DockerContainerName)' is running against an empty database volume."
            }

            Invoke-CheckedDocker -Arguments @("rm", $script:DockerContainerName) | Out-Null
            $containerExists = $false
        }

        Initialize-DockerVolume `
            -TemporaryRoot $TemporaryRoot `
            -ConfiguredDatabaseName $ConfiguredDatabaseName `
            -ConfiguredAppUsername $ConfiguredAppUsername
    }

    Start-OrCreateStableDockerContainer -ConfiguredPort $ConfiguredPort

    Write-Status "Provisioning PostgreSQL application role and database"
    Invoke-DockerClientPsql `
        -ServerContainerName $script:DockerContainerName `
        -TemporaryRoot $TemporaryRoot `
        -Credential admin `
        -PsqlArguments @(
            "-X",
            "-w",
            "-h",
            "127.0.0.1",
            "-p",
            "5432",
            "-U",
            $script:AdminUsername,
            "-d",
            "postgres",
            "-f",
            "/input/provision.sql"
        ) `
        -SensitiveOutput | Out-Null

    $testOutput = @(Invoke-DockerClientPsql `
        -ServerContainerName $script:DockerContainerName `
        -TemporaryRoot $TemporaryRoot `
        -Credential app `
        -PsqlArguments @(
            "-X",
            "-w",
            "-tA",
            "-h",
            "127.0.0.1",
            "-p",
            "5432",
            "-U",
            $ConfiguredAppUsername,
            "-d",
            $ConfiguredDatabaseName,
            "-c",
            "select current_user || '|' || current_database();"
        ) `
        -SensitiveOutput)

    $expected = "$ConfiguredAppUsername|$ConfiguredDatabaseName"
    if (-not ($testOutput | ForEach-Object { ([string]$_).Trim() } | Where-Object { $_ -eq $expected })) {
        throw "The Docker PostgreSQL application-role authentication test returned an unexpected result."
    }
}

function Test-ArchiveIntegrity {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $false
    }

    $item = Get-Item -LiteralPath $Path
    if ($item.Length -ne $script:EdBArchiveLength) {
        return $false
    }

    $hash = (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash
    return [string]::Equals($hash, $script:EdBArchiveSha256, [System.StringComparison]::OrdinalIgnoreCase)
}

function Get-VerifiedEdBArchive {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DownloadRoot
    )

    Assert-PathHasNoReparsePoints `
        -PathValue $DownloadRoot `
        -Description "Native PostgreSQL download directory"
    New-Item -ItemType Directory -Path $DownloadRoot -Force | Out-Null
    Get-ChildItem -LiteralPath $DownloadRoot -File -Force -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match ('^\.' + [regex]::Escape($script:EdBArchiveName) + '\.partial-[a-f0-9]{32}$') } |
        ForEach-Object {
            Assert-PathHasNoReparsePoints `
                -PathValue $_.FullName `
                -Description "Interrupted native PostgreSQL download"
            Remove-Item -LiteralPath $_.FullName -Force
        }

    $archivePath = Join-Path $DownloadRoot $script:EdBArchiveName
    Assert-PathHasNoReparsePoints `
        -PathValue $archivePath `
        -Description "Native PostgreSQL archive path"
    if (Test-ArchiveIntegrity -Path $archivePath) {
        return $archivePath
    }

    if (Test-Path -LiteralPath $archivePath) {
        Assert-PathHasNoReparsePoints `
            -PathValue $archivePath `
            -Description "Invalid native PostgreSQL archive"
        Remove-Item -LiteralPath $archivePath -Force
    }

    $partialPath = Join-Path $DownloadRoot (".$($script:EdBArchiveName).partial-" + [Guid]::NewGuid().ToString("N"))
    Assert-PathHasNoReparsePoints `
        -PathValue $partialPath `
        -Description "Native PostgreSQL partial download path"
    Write-Status "Downloading the pinned EnterpriseDB PostgreSQL 16.14 Windows x64 archive"
    $previousSecurityProtocol = [Net.ServicePointManager]::SecurityProtocol
    try {
        [Net.ServicePointManager]::SecurityProtocol = $previousSecurityProtocol -bor [Net.SecurityProtocolType]::Tls12
        Invoke-WebRequest -Uri $script:EdBArchiveUri -OutFile $partialPath -UseBasicParsing
    }
    catch {
        Assert-PathHasNoReparsePoints `
            -PathValue $partialPath `
            -Description "Failed native PostgreSQL partial download"
        Remove-Item -LiteralPath $partialPath -Force -ErrorAction SilentlyContinue
        throw
    }
    finally {
        [Net.ServicePointManager]::SecurityProtocol = $previousSecurityProtocol
    }

    if (-not (Test-ArchiveIntegrity -Path $partialPath)) {
        Assert-PathHasNoReparsePoints `
            -PathValue $partialPath `
            -Description "Invalid native PostgreSQL partial download"
        Remove-Item -LiteralPath $partialPath -Force -ErrorAction SilentlyContinue
        throw "The downloaded EnterpriseDB PostgreSQL archive failed its exact size or SHA-256 integrity check."
    }

    try {
        Assert-PathHasNoReparsePoints `
            -PathValue $partialPath `
            -Description "Verified native PostgreSQL partial download"
        Assert-PathHasNoReparsePoints `
            -PathValue $archivePath `
            -Description "Native PostgreSQL archive destination"
        Move-Item -LiteralPath $partialPath -Destination $archivePath
        return $archivePath
    }
    finally {
        Assert-PathHasNoReparsePoints `
            -PathValue $partialPath `
            -Description "Native PostgreSQL partial download cleanup"
        Remove-Item -LiteralPath $partialPath -Force -ErrorAction SilentlyContinue
    }
}

function Test-NativeBinaryLayout {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BinPath
    )

    foreach ($executable in @("initdb.exe", "pg_ctl.exe", "pg_isready.exe", "psql.exe", "postgres.exe")) {
        if (-not (Test-Path -LiteralPath (Join-Path $BinPath $executable) -PathType Leaf)) {
            return $false
        }
    }

    $pgsqlRoot = Split-Path -Parent $BinPath
    foreach ($relativePath in @("lib", "share")) {
        if (-not (Test-Path -LiteralPath (Join-Path $pgsqlRoot $relativePath) -PathType Container)) {
            return $false
        }
    }

    foreach ($relativePath in @("server_license.txt", "commandlinetools_3rd_party_licenses.txt")) {
        if (-not (Test-Path -LiteralPath (Join-Path $pgsqlRoot $relativePath) -PathType Leaf)) {
            return $false
        }
    }

    return $true
}

function Assert-NativePostgreSqlLoads {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BinPath
    )

    foreach ($executable in @("postgres.exe", "initdb.exe", "pg_ctl.exe", "pg_isready.exe", "psql.exe")) {
        $executablePath = Join-Path $BinPath $executable
        try {
            $result = Invoke-ExternalProbe -FilePath $executablePath -Arguments @("--version")
        }
        catch {
            throw "Native PostgreSQL executable '$executable' could not start. Install the supported Microsoft Visual C++ x64 runtime and retry: https://learn.microsoft.com/cpp/windows/latest-supported-vc-redist"
        }

        $versionText = ($result.Output | ForEach-Object { [string]$_ }) -join " "
        if ($result.ExitCode -ne 0 -or $versionText -notmatch '(?i)PostgreSQL\)\s+16\.14(?:\s|$)') {
            throw "Native PostgreSQL executable '$executable' failed its exact version check. Expected PostgreSQL 16.14, but it returned exit code $($result.ExitCode). Install the supported Microsoft Visual C++ x64 runtime if Windows reported a missing runtime DLL."
        }
    }
}

function Test-NativePostgreSqlDistribution {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BinPath
    )

    if (-not (Test-NativeBinaryLayout -BinPath $BinPath)) {
        return $false
    }

    try {
        Assert-NativePostgreSqlLoads -BinPath $BinPath
        return $true
    }
    catch {
        return $false
    }
}

function Assert-SupportedNativeDataVersion {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DataPath
    )

    Assert-PathHasNoReparsePoints `
        -PathValue $DataPath `
        -Description "Native PostgreSQL data path"
    $pgVersionPath = Join-Path $DataPath "PG_VERSION"
    Assert-PathHasNoReparsePoints `
        -PathValue $pgVersionPath `
        -Description "Native PostgreSQL data version marker"
    if (-not (Test-Path -LiteralPath $pgVersionPath)) {
        return
    }
    if (-not (Test-Path -LiteralPath $pgVersionPath -PathType Leaf)) {
        throw "Native PostgreSQL data version marker '$pgVersionPath' is not a file."
    }

    $majorVersion = (Get-Content -LiteralPath $pgVersionPath -Raw).Trim()
    if ($majorVersion -ne "16") {
        throw "Native PostgreSQL data uses major version '$majorVersion'; this installer requires major version 16."
    }
}

function Remove-SafeNativePgsqlDirectory {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PgsqlPath,
        [Parameter(Mandatory = $true)]
        [string]$NativeRoot
    )

    if (-not (Test-Path -LiteralPath $PgsqlPath)) {
        return
    }

    $expected = [System.IO.Path]::GetFullPath((Join-Path $NativeRoot "pgsql"))
    $actual = [System.IO.Path]::GetFullPath($PgsqlPath)
    if (-not [string]::Equals($actual, $expected, [System.StringComparison]::OrdinalIgnoreCase)) {
        throw "Refusing to remove unexpected native PostgreSQL path '$actual'."
    }

    Assert-DirectoryTreeHasNoReparsePoints `
        -PathValue $actual `
        -Description "Native PostgreSQL replacement target"
    Remove-Item -LiteralPath $actual -Recurse -Force
}

function Stop-NativePostgreSqlForBinaryReplacement {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BinPath,
        [Parameter(Mandatory = $true)]
        [string]$DataPath
    )

    Assert-PathHasNoReparsePoints `
        -PathValue $DataPath `
        -Description "Native PostgreSQL data path before binary replacement"
    if (-not (Test-Path -LiteralPath (Join-Path $DataPath "PG_VERSION") -PathType Leaf)) {
        return $false
    }

    $pgCtlPath = Join-Path $BinPath "pg_ctl.exe"
    $postmasterPidPath = Join-Path $DataPath "postmaster.pid"
    Assert-PathHasNoReparsePoints `
        -PathValue $pgCtlPath `
        -Description "Native PostgreSQL control executable"
    Assert-PathHasNoReparsePoints `
        -PathValue $postmasterPidPath `
        -Description "Native PostgreSQL process marker"
    if (-not (Test-Path -LiteralPath $pgCtlPath -PathType Leaf)) {
        if (Test-Path -LiteralPath $postmasterPidPath -PathType Leaf) {
            throw "Native PostgreSQL may be running, but pg_ctl.exe is unavailable. Stop the managed server before repairing its binaries."
        }
        return $false
    }

    try {
        $status = Invoke-ExternalProbe -FilePath $pgCtlPath -Arguments @("status", "-D", $DataPath)
    }
    catch {
        if (Test-Path -LiteralPath $postmasterPidPath -PathType Leaf) {
            throw "The existing pg_ctl.exe could not determine whether native PostgreSQL is running. Stop the managed server before repairing its binaries."
        }
        return $false
    }

    switch ($status.ExitCode) {
        0 {
            Write-Status "Stopping native PostgreSQL before replacing its binary distribution"
            Invoke-CheckedExternal -FilePath $pgCtlPath -Arguments @(
                "stop",
                "-D",
                $DataPath,
                "-m",
                "fast",
                "-w",
                "-t",
                "120"
            ) | Out-Null
            return $true
        }
        3 { return $false }
        default {
            if (Test-Path -LiteralPath $postmasterPidPath -PathType Leaf) {
                throw "pg_ctl could not safely determine native PostgreSQL status before binary replacement (exit code $($status.ExitCode))."
            }
            return $false
        }
    }
}

function Start-NativePostgreSqlAfterBinaryRollback {
    param(
        [Parameter(Mandatory = $true)]
        [string]$BinPath,
        [Parameter(Mandatory = $true)]
        [string]$DataPath,
        [Parameter(Mandatory = $true)]
        [string]$LogPath
    )

    $pgCtlPath = Join-Path $BinPath "pg_ctl.exe"
    $logRoot = Split-Path -Parent $LogPath
    foreach ($managedPath in @($BinPath, $pgCtlPath, $DataPath, $logRoot, $LogPath)) {
        Assert-PathHasNoReparsePoints `
            -PathValue $managedPath `
            -Description "Native PostgreSQL rollback restart path"
    }

    New-Item -ItemType Directory -Path $logRoot -Force | Out-Null
    Rotate-NativePostgreSqlLog -LogPath $LogPath
    Write-Status "Restarting native PostgreSQL after restoring its previous binary distribution"
    Invoke-CheckedExternal -FilePath $pgCtlPath -Arguments @(
        "start",
        "-D",
        $DataPath,
        "-l",
        $LogPath,
        "-w",
        "-t",
        "120"
    ) | Out-Null
}

function Ensure-NativePostgreSqlBinaries {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DatabaseRoot,
        [Parameter(Mandatory = $true)]
        [string]$NativeRoot,
        [Parameter(Mandatory = $true)]
        [string]$BinPath,
        [Parameter(Mandatory = $true)]
        [string]$DataPath,
        [Parameter(Mandatory = $true)]
        [string]$LogPath
    )

    foreach ($managedPath in @($DatabaseRoot, $NativeRoot, $BinPath, $DataPath, $LogPath)) {
        Assert-PathHasNoReparsePoints `
            -PathValue $managedPath `
            -Description "Managed native PostgreSQL path"
    }
    Assert-SupportedNativeDataVersion -DataPath $DataPath

    $pgsqlPath = Join-Path $NativeRoot "pgsql"
    Assert-PathHasNoReparsePoints `
        -PathValue $pgsqlPath `
        -Description "Native PostgreSQL distribution path"
    if (Test-Path -LiteralPath $pgsqlPath) {
        Assert-DirectoryTreeHasNoReparsePoints `
            -PathValue $pgsqlPath `
            -Description "Existing native PostgreSQL distribution"
    }
    $previousDistributions = @(Get-ChildItem `
        -LiteralPath $NativeRoot `
        -Directory `
        -Force `
        -ErrorAction SilentlyContinue |
        Where-Object { $_.Name -match '^\.previous-pgsql-[a-f0-9]{32}$' })
    if ($previousDistributions.Count -gt 1) {
        throw "Multiple native PostgreSQL replacement backups were found. Refusing automatic recovery."
    }

    $currentDistributionValid = Test-NativePostgreSqlDistribution -BinPath $BinPath
    if (-not $currentDistributionValid -and
        -not (Test-Path -LiteralPath $pgsqlPath) -and
        $previousDistributions.Count -eq 1) {
        Assert-DirectoryTreeHasNoReparsePoints `
            -PathValue $previousDistributions[0].FullName `
            -Description "Interrupted native PostgreSQL replacement backup"
        Assert-PathHasNoReparsePoints `
            -PathValue $pgsqlPath `
            -Description "Recovered native PostgreSQL distribution destination"
        Move-Item -LiteralPath $previousDistributions[0].FullName -Destination $pgsqlPath
        $previousDistributions = @()
        $currentDistributionValid = Test-NativePostgreSqlDistribution -BinPath $BinPath
    }

    if (-not $currentDistributionValid -and
        (Test-Path -LiteralPath $pgsqlPath) -and
        $previousDistributions.Count -eq 1) {
        throw "Both an invalid native PostgreSQL distribution and a replacement backup were found. Refusing to discard either automatically."
    }

    if ($currentDistributionValid) {
        foreach ($previousDistribution in $previousDistributions) {
            try {
                Assert-DirectoryTreeHasNoReparsePoints `
                    -PathValue $previousDistribution.FullName `
                    -Description "Completed native PostgreSQL replacement backup"
                Remove-Item -LiteralPath $previousDistribution.FullName -Recurse -Force
            }
            catch {
                Write-Warning "The validated native PostgreSQL distribution is ready, but its completed replacement backup could not be removed: $($_.Exception.Message)"
            }
        }
        return
    }

    if (-not [Environment]::Is64BitOperatingSystem) {
        throw "The native PostgreSQL fallback requires 64-bit Windows."
    }

    if (Test-Path -LiteralPath $pgsqlPath) {
        Write-Status "The existing native PostgreSQL distribution failed exact validation and will be repaired"
    }

    $downloadRoot = Join-Path $DatabaseRoot "downloads"
    Assert-PathHasNoReparsePoints `
        -PathValue $downloadRoot `
        -Description "Native PostgreSQL download directory"
    $archivePath = Get-VerifiedEdBArchive -DownloadRoot $downloadRoot
    Assert-PathHasNoReparsePoints `
        -PathValue $NativeRoot `
        -Description "Native PostgreSQL installation directory"
    New-Item -ItemType Directory -Path $NativeRoot -Force | Out-Null
    $stagingRoot = Join-Path $NativeRoot (".extract-" + [Guid]::NewGuid().ToString("N"))
    Assert-PathHasNoReparsePoints `
        -PathValue $stagingRoot `
        -Description "Native PostgreSQL extraction staging path"
    try {
        Write-Status "Extracting the full EnterpriseDB pgsql distribution, including notices and documentation"
        Assert-PathHasNoReparsePoints `
            -PathValue $archivePath `
            -Description "Verified native PostgreSQL archive"
        Assert-PathHasNoReparsePoints `
            -PathValue $stagingRoot `
            -Description "Native PostgreSQL extraction staging path"
        Expand-Archive -LiteralPath $archivePath -DestinationPath $stagingRoot
        $stagedPgsql = Join-Path $stagingRoot "pgsql"
        $stagedBin = Join-Path $stagedPgsql "bin"
        Assert-DirectoryTreeHasNoReparsePoints `
            -PathValue $stagedPgsql `
            -Description "Extracted native PostgreSQL distribution"
        if (-not (Test-NativeBinaryLayout -BinPath $stagedBin)) {
            throw "The verified EnterpriseDB archive did not contain the expected full pgsql binary layout."
        }
        Assert-NativePostgreSqlLoads -BinPath $stagedBin

        $previousPgsqlPath = Join-Path $NativeRoot (".previous-pgsql-" + [Guid]::NewGuid().ToString("N"))
        Assert-PathHasNoReparsePoints `
            -PathValue $previousPgsqlPath `
            -Description "Native PostgreSQL replacement backup path"
        $serverWasRunning = [bool](Stop-NativePostgreSqlForBinaryReplacement `
            -BinPath $BinPath `
            -DataPath $DataPath)
        $previousPgsqlMoved = $false
        $newPgsqlMoved = $false
        try {
            if (Test-Path -LiteralPath $pgsqlPath) {
                Assert-DirectoryTreeHasNoReparsePoints `
                    -PathValue $pgsqlPath `
                    -Description "Existing native PostgreSQL distribution"
                Assert-PathHasNoReparsePoints `
                    -PathValue $previousPgsqlPath `
                    -Description "Native PostgreSQL replacement backup destination"
                Move-Item -LiteralPath $pgsqlPath -Destination $previousPgsqlPath
                $previousPgsqlMoved = $true
            }

            Assert-DirectoryTreeHasNoReparsePoints `
                -PathValue $stagedPgsql `
                -Description "Validated native PostgreSQL staged distribution"
            Assert-PathHasNoReparsePoints `
                -PathValue $pgsqlPath `
                -Description "Native PostgreSQL replacement destination"
            Move-Item -LiteralPath $stagedPgsql -Destination $pgsqlPath
            $newPgsqlMoved = $true
            if (-not (Test-NativeBinaryLayout -BinPath $BinPath)) {
                throw "Native PostgreSQL replacement did not produce the expected binary layout."
            }
            Assert-NativePostgreSqlLoads -BinPath $BinPath
        }
        catch {
            $replacementFailure = $_.Exception
            $rollbackFailure = $null
            $restartFailure = $null
            $failedPgsqlPath = $null
            try {
                if ($newPgsqlMoved -and (Test-Path -LiteralPath $pgsqlPath)) {
                    $failedPgsqlPath = Join-Path $NativeRoot (".failed-pgsql-" + [Guid]::NewGuid().ToString("N"))
                    Assert-DirectoryTreeHasNoReparsePoints `
                        -PathValue $pgsqlPath `
                        -Description "Failed native PostgreSQL replacement"
                    Assert-PathHasNoReparsePoints `
                        -PathValue $failedPgsqlPath `
                        -Description "Failed native PostgreSQL quarantine destination"
                    Move-Item -LiteralPath $pgsqlPath -Destination $failedPgsqlPath
                }

                if ($previousPgsqlMoved) {
                    if (Test-Path -LiteralPath $pgsqlPath) {
                        throw "The failed native PostgreSQL replacement could not be quarantined before rollback."
                    }

                    Assert-DirectoryTreeHasNoReparsePoints `
                        -PathValue $previousPgsqlPath `
                        -Description "Native PostgreSQL rollback source"
                    Assert-PathHasNoReparsePoints `
                        -PathValue $pgsqlPath `
                        -Description "Native PostgreSQL rollback destination"
                    Move-Item -LiteralPath $previousPgsqlPath -Destination $pgsqlPath
                }
            }
            catch {
                $rollbackFailure = $_.Exception
            }

            if ($null -eq $rollbackFailure -and $serverWasRunning) {
                try {
                    Start-NativePostgreSqlAfterBinaryRollback `
                        -BinPath $BinPath `
                        -DataPath $DataPath `
                        -LogPath $LogPath
                }
                catch {
                    $restartFailure = $_.Exception
                }
            }

            if ($null -ne $failedPgsqlPath -and (Test-Path -LiteralPath $failedPgsqlPath)) {
                try {
                    $failedLeafName = Split-Path -Leaf $failedPgsqlPath
                    $expectedFailedParent = [System.IO.Path]::GetFullPath($NativeRoot)
                    $actualFailedParent = [System.IO.Path]::GetFullPath((Split-Path -Parent $failedPgsqlPath))
                    if ($failedLeafName -notmatch '^\.failed-pgsql-[a-f0-9]{32}$' -or
                        -not [string]::Equals(
                            $actualFailedParent,
                            $expectedFailedParent,
                            [System.StringComparison]::OrdinalIgnoreCase)) {
                        throw "Refusing to remove unexpected failed native PostgreSQL quarantine '$failedPgsqlPath'."
                    }

                    Assert-DirectoryTreeHasNoReparsePoints `
                        -PathValue $failedPgsqlPath `
                        -Description "Failed native PostgreSQL quarantine cleanup"
                    Remove-Item -LiteralPath $failedPgsqlPath -Recurse -Force
                }
                catch {
                    Write-Warning "Native PostgreSQL rollback state was preserved, but failed replacement quarantine cleanup did not complete: $($_.Exception.Message)"
                }
            }

            if ($null -ne $rollbackFailure) {
                throw "Native PostgreSQL binary replacement failed and its previous distribution could not be restored. Replacement error: $($replacementFailure.Message) Rollback error: $($rollbackFailure.Message)"
            }
            if ($null -ne $restartFailure) {
                throw "Native PostgreSQL binary replacement failed. Its previous distribution was restored, but the previously running server could not be restarted. Replacement error: $($replacementFailure.Message) Restart error: $($restartFailure.Message)"
            }
            throw $replacementFailure
        }

        if ($previousPgsqlMoved) {
            try {
                Assert-DirectoryTreeHasNoReparsePoints `
                    -PathValue $previousPgsqlPath `
                    -Description "Previous native PostgreSQL distribution"
                Remove-Item -LiteralPath $previousPgsqlPath -Recurse -Force
            }
            catch {
                Write-Warning "The native PostgreSQL replacement is valid, but its previous distribution could not be removed: $($_.Exception.Message)"
            }
        }
    }
    finally {
        if (Test-Path -LiteralPath $stagingRoot) {
            try {
                Assert-DirectoryTreeHasNoReparsePoints `
                    -PathValue $stagingRoot `
                    -Description "Native PostgreSQL extraction staging target"
                Remove-Item -LiteralPath $stagingRoot -Recurse -Force
            }
            catch {
                Write-Warning "Native PostgreSQL extraction staging cleanup did not complete: $($_.Exception.Message)"
            }
        }
    }

    if (-not (Test-NativeBinaryLayout -BinPath $BinPath)) {
        throw "Native PostgreSQL extraction completed without the required executables."
    }

    Assert-NativePostgreSqlLoads -BinPath $BinPath
}

function Set-NativePostgreSqlConfiguration {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DataPath,
        [Parameter(Mandatory = $true)]
        [int]$ConfiguredPort
    )

    Assert-PathHasNoReparsePoints `
        -PathValue $DataPath `
        -Description "Native PostgreSQL data configuration directory"
    $configurationPath = Join-Path $DataPath "postgresql.conf"
    $hbaPath = Join-Path $DataPath "pg_hba.conf"
    foreach ($configurationFile in @($configurationPath, $hbaPath)) {
        Assert-PathHasNoReparsePoints `
            -PathValue $configurationFile `
            -Description "Native PostgreSQL configuration file"
    }
    if (-not (Test-Path -LiteralPath $configurationPath -PathType Leaf) -or
        -not (Test-Path -LiteralPath $hbaPath -PathType Leaf)) {
        throw "The native PostgreSQL data directory is missing its configuration files."
    }

    $beginMarker = "# BEGIN CanDoItAll installed web app"
    $endMarker = "# END CanDoItAll installed web app"
    $existing = Get-Content -LiteralPath $configurationPath -Raw
    $pattern = '(?ms)^# BEGIN CanDoItAll installed web app\r?\n.*?^# END CanDoItAll installed web app\r?\n?'
    $withoutManagedBlock = [regex]::Replace($existing, $pattern, "").TrimEnd()
    $managedBlock = @"
$beginMarker
listen_addresses = '$($script:DatabaseHost)'
port = $ConfiguredPort
password_encryption = 'scram-sha-256'
$endMarker
"@
    $updatedConfiguration = $withoutManagedBlock +
        [Environment]::NewLine +
        [Environment]::NewLine +
        $managedBlock.Trim() +
        [Environment]::NewLine

    $hba = @"
# CanDoItAll installed web app: authenticated loopback access only.
local all all scram-sha-256
host all all 127.0.0.1/32 scram-sha-256
host all all ::1/128 scram-sha-256
"@
    $updatedHba = $hba.Trim() + [Environment]::NewLine
    $existingHba = Get-Content -LiteralPath $hbaPath -Raw
    $configurationChanged = -not [string]::Equals(
        $existing,
        $updatedConfiguration,
        [System.StringComparison]::Ordinal)
    $hbaChanged = -not [string]::Equals(
        $existingHba,
        $updatedHba,
        [System.StringComparison]::Ordinal)

    if ($configurationChanged) {
        Assert-PathHasNoReparsePoints `
            -PathValue $configurationPath `
            -Description "Native PostgreSQL configuration file"
        Write-Utf8NoBomFile -Path $configurationPath -Content $updatedConfiguration
    }

    if ($hbaChanged) {
        Assert-PathHasNoReparsePoints `
            -PathValue $hbaPath `
            -Description "Native PostgreSQL client authentication file"
        Write-Utf8NoBomFile -Path $hbaPath -Content $updatedHba
    }

    return ($configurationChanged -or $hbaChanged)
}

function Rotate-NativePostgreSqlLog {
    param(
        [Parameter(Mandatory = $true)]
        [string]$LogPath
    )

    Assert-PathHasNoReparsePoints `
        -PathValue $LogPath `
        -Description "Native PostgreSQL log path"
    if (-not (Test-Path -LiteralPath $LogPath -PathType Leaf)) {
        return
    }

    $logItem = Get-Item -LiteralPath $LogPath
    if ($logItem.Length -lt 10MB) {
        return
    }

    $archivePath = "$LogPath.1"
    Assert-PathHasNoReparsePoints `
        -PathValue $archivePath `
        -Description "Native PostgreSQL rotated log path"
    Remove-Item -LiteralPath $archivePath -Force -ErrorAction SilentlyContinue
    Assert-PathHasNoReparsePoints `
        -PathValue $LogPath `
        -Description "Native PostgreSQL active log path"
    Move-Item -LiteralPath $LogPath -Destination $archivePath
}

function Get-NativePostgreSqlStatus {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PgCtlPath,
        [Parameter(Mandatory = $true)]
        [string]$DataPath
    )

    $result = Invoke-ExternalProbe -FilePath $PgCtlPath -Arguments @("status", "-D", $DataPath)
    switch ($result.ExitCode) {
        0 { return "Running" }
        3 { return "Stopped" }
        default { throw "pg_ctl could not determine the native PostgreSQL status (exit code $($result.ExitCode))." }
    }
}

function Wait-NativePostgreSqlReady {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PgIsReadyPath,
        [Parameter(Mandatory = $true)]
        [int]$ConfiguredPort,
        [int]$TimeoutSeconds = 120
    )

    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($TimeoutSeconds)
    while ([DateTimeOffset]::UtcNow -lt $deadline) {
        $result = Invoke-ExternalProbe -FilePath $PgIsReadyPath -Arguments @(
            "-h",
            $script:DatabaseHost,
            "-p",
            [string]$ConfiguredPort,
            "-d",
            "postgres"
        )

        if ($result.ExitCode -eq 0) {
            return
        }

        if ($result.ExitCode -eq 3) {
            throw "pg_isready rejected the native PostgreSQL probe parameters."
        }

        Start-Sleep -Seconds 1
    }

    throw "Native PostgreSQL did not become ready within $TimeoutSeconds seconds."
}

function Invoke-NativePsql {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PsqlPath,
        [Parameter(Mandatory = $true)]
        [string]$PgPassPath,
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,
        [switch]$SensitiveOutput
    )

    $previousPgPassFile = $env:PGPASSFILE
    $previousPgPassword = $env:PGPASSWORD
    $pgPasswordWasDefined = Test-Path Env:\PGPASSWORD
    try {
        Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
        $env:PGPASSFILE = $PgPassPath
        return Invoke-CheckedExternal `
            -FilePath $PsqlPath `
            -Arguments $Arguments `
            -SensitiveOutput:$SensitiveOutput
    }
    finally {
        $env:PGPASSFILE = $previousPgPassFile
        if ($pgPasswordWasDefined) {
            $env:PGPASSWORD = $previousPgPassword
        }
        else {
            Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
        }
    }
}

function Invoke-NativeDatabaseSetup {
    param(
        [Parameter(Mandatory = $true)]
        [string]$DatabaseRoot,
        [Parameter(Mandatory = $true)]
        [string]$BinPath,
        [Parameter(Mandatory = $true)]
        [string]$DataPath,
        [Parameter(Mandatory = $true)]
        [string]$LogPath,
        [Parameter(Mandatory = $true)]
        [string]$TemporaryRoot,
        [Parameter(Mandatory = $true)]
        [string]$AdminPasswordPath,
        [Parameter(Mandatory = $true)]
        [string]$AdminPgPassPath,
        [Parameter(Mandatory = $true)]
        [string]$AppPgPassPath,
        [Parameter(Mandatory = $true)]
        [string]$ProvisioningSqlPath,
        [Parameter(Mandatory = $true)]
        [int]$ConfiguredPort,
        [Parameter(Mandatory = $true)]
        [string]$ConfiguredDatabaseName,
        [Parameter(Mandatory = $true)]
        [string]$ConfiguredAppUsername
    )

    foreach ($managedPath in @(
            $DatabaseRoot,
            $BinPath,
            $DataPath,
            $LogPath,
            $TemporaryRoot,
            $AdminPasswordPath,
            $AdminPgPassPath,
            $AppPgPassPath,
            $ProvisioningSqlPath)) {
        Assert-PathHasNoReparsePoints `
            -PathValue $managedPath `
            -Description "Managed native PostgreSQL setup path"
    }

    $nativeRoot = Split-Path -Parent (Split-Path -Parent $BinPath)
    Ensure-NativePostgreSqlBinaries `
        -DatabaseRoot $DatabaseRoot `
        -NativeRoot $nativeRoot `
        -BinPath $BinPath `
        -DataPath $DataPath `
        -LogPath $LogPath

    $initDbPath = Join-Path $BinPath "initdb.exe"
    $pgCtlPath = Join-Path $BinPath "pg_ctl.exe"
    $pgIsReadyPath = Join-Path $BinPath "pg_isready.exe"
    $psqlPath = Join-Path $BinPath "psql.exe"
    $pgVersionPath = Join-Path $DataPath "PG_VERSION"

    if (-not (Test-Path -LiteralPath $pgVersionPath -PathType Leaf)) {
        Assert-PathHasNoReparsePoints `
            -PathValue $DataPath `
            -Description "Native PostgreSQL data initialization path"
        if (Test-Path -LiteralPath $DataPath) {
            $existingEntries = @(Get-ChildItem -LiteralPath $DataPath -Force)
            if ($existingEntries.Count -gt 0) {
                throw "Native PostgreSQL data directory '$DataPath' is non-empty but has no PG_VERSION marker. Refusing to overwrite it."
            }
        }
        else {
            Assert-PathHasNoReparsePoints `
                -PathValue $DataPath `
                -Description "Native PostgreSQL data initialization path"
            New-Item -ItemType Directory -Path $DataPath | Out-Null
        }

        Set-RestrictedAcl -Path $DataPath -IsDirectory $true
        Write-Status "Initializing native PostgreSQL data"
        Invoke-CheckedExternal -FilePath $initDbPath -Arguments @(
            "-D",
            $DataPath,
            "-U",
            $script:AdminUsername,
            "--pwfile=$AdminPasswordPath",
            "--auth-host=scram-sha-256",
            "--auth-local=scram-sha-256",
            "--encoding=UTF8"
        ) | Out-Null
    }

    Assert-SupportedNativeDataVersion -DataPath $DataPath

    $configurationChanged = Set-NativePostgreSqlConfiguration `
        -DataPath $DataPath `
        -ConfiguredPort $ConfiguredPort
    $logRoot = Split-Path -Parent $LogPath
    foreach ($managedLogPath in @($logRoot, $LogPath)) {
        Assert-PathHasNoReparsePoints `
            -PathValue $managedLogPath `
            -Description "Native PostgreSQL log path"
    }
    New-Item -ItemType Directory -Path $logRoot -Force | Out-Null

    $status = Get-NativePostgreSqlStatus -PgCtlPath $pgCtlPath -DataPath $DataPath
    if ($status -eq "Running" -and $configurationChanged) {
        Write-Status "Restarting native PostgreSQL to apply managed connection settings"
        Invoke-CheckedExternal -FilePath $pgCtlPath -Arguments @(
            "stop",
            "-D",
            $DataPath,
            "-m",
            "fast",
            "-w",
            "-t",
            "120"
        ) | Out-Null
        $status = "Stopped"
    }

    if ($status -eq "Stopped") {
        Rotate-NativePostgreSqlLog -LogPath $LogPath
        Write-Status "Starting native PostgreSQL"
        Invoke-CheckedExternal -FilePath $pgCtlPath -Arguments @(
            "start",
            "-D",
            $DataPath,
            "-l",
            $LogPath,
            "-w",
            "-t",
            "120"
        ) | Out-Null
    }

    Wait-NativePostgreSqlReady -PgIsReadyPath $pgIsReadyPath -ConfiguredPort $ConfiguredPort

    Write-Status "Provisioning PostgreSQL application role and database"
    Invoke-NativePsql `
        -PsqlPath $psqlPath `
        -PgPassPath $AdminPgPassPath `
        -Arguments @(
            "-X",
            "-w",
            "-h",
            $script:DatabaseHost,
            "-p",
            [string]$ConfiguredPort,
            "-U",
            $script:AdminUsername,
            "-d",
            "postgres",
            "-f",
            $ProvisioningSqlPath
        ) `
        -SensitiveOutput | Out-Null

    $testOutput = @(Invoke-NativePsql `
        -PsqlPath $psqlPath `
        -PgPassPath $AppPgPassPath `
        -Arguments @(
            "-X",
            "-w",
            "-tA",
            "-h",
            $script:DatabaseHost,
            "-p",
            [string]$ConfiguredPort,
            "-U",
            $ConfiguredAppUsername,
            "-d",
            $ConfiguredDatabaseName,
            "-c",
            "select current_user || '|' || current_database();"
        ) `
        -SensitiveOutput)

    $expected = "$ConfiguredAppUsername|$ConfiguredDatabaseName"
    if (-not ($testOutput | ForEach-Object { ([string]$_).Trim() } | Where-Object { $_ -eq $expected })) {
        throw "The native PostgreSQL application-role authentication test returned an unexpected result."
    }
}

function Write-DatabaseManifest {
    param(
        [Parameter(Mandatory = $true)]
        [string]$ManifestPath,
        [Parameter(Mandatory = $true)]
        [string]$Engine,
        [Parameter(Mandatory = $true)]
        [int]$ConfiguredPort,
        [Parameter(Mandatory = $true)]
        [string]$ConfiguredDatabaseName,
        [Parameter(Mandatory = $true)]
        [string]$ConfiguredAppUsername,
        [Parameter(Mandatory = $true)]
        [string]$AppPasswordFile,
        [Parameter(Mandatory = $true)]
        [string]$NativeBinRelative,
        [Parameter(Mandatory = $true)]
        [string]$NativeDataRelative,
        [Parameter(Mandatory = $true)]
        [string]$NativeLogRelative
    )

    $dockerMetadata = $null
    $nativeMetadata = $null
    if ($Engine -eq "docker") {
        $dockerMetadata = [ordered]@{
            containerName = $script:DockerContainerName
            volumeName = $script:DockerVolumeName
            image = $script:DockerImage
        }
    }
    else {
        $nativeMetadata = [ordered]@{
            binPath = $NativeBinRelative
            dataPath = $NativeDataRelative
            logPath = $NativeLogRelative
        }
    }

    $manifest = [ordered]@{
        schemaVersion = $script:SchemaVersion
        engine = $Engine
        host = $script:DatabaseHost
        port = $ConfiguredPort
        databaseName = $ConfiguredDatabaseName
        appUsername = $ConfiguredAppUsername
        appPasswordFile = $AppPasswordFile
        docker = $dockerMetadata
        native = $nativeMetadata
    }

    Assert-PathHasNoReparsePoints `
        -PathValue $ManifestPath `
        -Description "Database manifest path"
    $temporaryManifestPath = "$ManifestPath.new-$([Guid]::NewGuid().ToString('N'))"
    try {
        Write-Utf8NoBomFile -Path $temporaryManifestPath -Content (($manifest | ConvertTo-Json -Depth 5) + [Environment]::NewLine)
        Assert-PathHasNoReparsePoints `
            -PathValue $ManifestPath `
            -Description "Database manifest path"
        Move-Item -LiteralPath $temporaryManifestPath -Destination $ManifestPath -Force
    }
    finally {
        Assert-PathHasNoReparsePoints `
            -PathValue $temporaryManifestPath `
            -Description "Database manifest temporary path"
        Remove-Item -LiteralPath $temporaryManifestPath -Force -ErrorAction SilentlyContinue
    }
}

function New-DatabaseSetupResult {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Status,
        [Parameter(Mandatory = $true)]
        [string]$Engine,
        [Parameter(Mandatory = $true)]
        [string]$ManifestPath,
        [Parameter(Mandatory = $true)]
        [string]$ProtectedAppPasswordPath,
        [Parameter(Mandatory = $true)]
        [int]$ConfiguredPort,
        [Parameter(Mandatory = $true)]
        [string]$ConfiguredDatabaseName,
        [Parameter(Mandatory = $true)]
        [string]$ConfiguredAppUsername
    )

    return [pscustomobject]@{
        Status = $Status
        Engine = $Engine
        Host = $script:DatabaseHost
        Port = $ConfiguredPort
        DatabaseName = $ConfiguredDatabaseName
        AppUsername = $ConfiguredAppUsername
        ManifestPath = $ManifestPath
        ProtectedAppPasswordPath = $ProtectedAppPasswordPath
    }
}

if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) {
    throw "This installer supports Windows only."
}

if ([string]::IsNullOrWhiteSpace($InstallRoot)) {
    if ([string]::IsNullOrWhiteSpace($env:LOCALAPPDATA)) {
        throw "LOCALAPPDATA is not available; specify -InstallRoot."
    }

    $InstallRoot = Join-Path $env:LOCALAPPDATA "CanDoItAll\WebApp"
}

$portWasBound = $PSBoundParameters.ContainsKey("Port")
$databaseNameWasBound = $PSBoundParameters.ContainsKey("DatabaseName")
$appUsernameWasBound = $PSBoundParameters.ContainsKey("AppUsername")

$InstallRoot = Resolve-ValidatedInstallRoot -PathValue $InstallRoot
$runtimeRoot = Join-Path $InstallRoot "runtime"
$databaseRoot = Join-Path $runtimeRoot "database"
$manifestPath = Join-Path $databaseRoot "database-manifest.json"
$pendingEnginePath = Join-Path $databaseRoot "database-engine.pending"
$defaultAppPasswordRelative = $script:AppPasswordRelativePath
$adminPasswordRelative = "secrets\admin-password.dpapi"
$nativeBinRelative = $script:NativeBinRelativePath
$nativeDataRelative = $script:NativeDataRelativePath
$nativeLogRelative = $script:NativeLogRelativePath
$appInstallMutex = New-Object System.Threading.Mutex(
    $false,
    "Global\CanDoItAll.WebApp.Install.v1")
$appInstallMutexAcquired = $false
$databaseInstallMutex = New-Object System.Threading.Mutex(
    $false,
    "Global\CanDoItAll.WebApp.DatabaseInstall.v1")
$databaseInstallMutexAcquired = $false
try {
    try {
        $appInstallMutexAcquired = $appInstallMutex.WaitOne(0)
    }
    catch [System.Threading.AbandonedMutexException] {
        $appInstallMutexAcquired = $true
    }

    if (-not $appInstallMutexAcquired) {
        throw "A CanDoItAll web app installation or startup is in progress. Wait for it to finish and retry database setup."
    }

    try {
        $databaseInstallMutexAcquired = $databaseInstallMutex.WaitOne(0)
    }
    catch [System.Threading.AbandonedMutexException] {
        $databaseInstallMutexAcquired = $true
    }

    if (-not $databaseInstallMutexAcquired) {
        throw "Another CanDoItAll installed-web-app database setup is already running. Wait for it to finish and retry."
    }

foreach ($managedStatePath in @($runtimeRoot, $databaseRoot, $manifestPath, $pendingEnginePath)) {
    Assert-PathHasNoReparsePoints `
        -PathValue $managedStatePath `
        -Description "Managed installed-web-app database state path"
}

$manifest = Read-DatabaseManifest -ManifestPath $manifestPath -DatabaseRoot $databaseRoot
$pendingState = if ($null -eq $manifest) {
    Read-PendingDatabaseState -Path $pendingEnginePath
}
else {
    $null
}
$pendingEngine = if ($null -eq $pendingState) { $null } else { [string]$pendingState.engine }
if ($null -ne $pendingState) {
    $pendingPort = [int]$pendingState.port
    $pendingDatabaseName = [string]$pendingState.databaseName
    $pendingAppUsername = [string]$pendingState.appUsername
    if ($portWasBound -and $Port -ne $pendingPort) {
        throw "Port '$Port' conflicts with interrupted database setup value '$pendingPort'."
    }
    if ($databaseNameWasBound -and $DatabaseName -ne $pendingDatabaseName) {
        throw "DatabaseName '$DatabaseName' conflicts with interrupted database setup value '$pendingDatabaseName'."
    }
    if ($appUsernameWasBound -and $AppUsername -ne $pendingAppUsername) {
        throw "AppUsername '$AppUsername' conflicts with interrupted database setup value '$pendingAppUsername'."
    }

    $Port = $pendingPort
    $DatabaseName = $pendingDatabaseName
    $AppUsername = $pendingAppUsername
}

if ($null -ne $manifest) {
    $manifestPort = [int]$manifest.port
    $manifestDatabaseName = [string]$manifest.databaseName
    $manifestAppUsername = [string]$manifest.appUsername

    if ($portWasBound -and $Port -ne $manifestPort) {
        throw "Port '$Port' conflicts with the existing database manifest value '$manifestPort'. Explicit database migration is required to change it."
    }

    if ($databaseNameWasBound -and $DatabaseName -ne $manifestDatabaseName) {
        throw "DatabaseName '$DatabaseName' conflicts with the existing database manifest value '$manifestDatabaseName'."
    }

    if ($appUsernameWasBound -and $AppUsername -ne $manifestAppUsername) {
        throw "AppUsername '$AppUsername' conflicts with the existing database manifest value '$manifestAppUsername'."
    }

    $Port = $manifestPort
    $DatabaseName = $manifestDatabaseName
    $AppUsername = $manifestAppUsername
    $engine = [string]$manifest.engine
    $appPasswordRelative = [string]$manifest.appPasswordFile

    if ($engine -eq "native") {
        $nativeBinRelative = [string]$manifest.native.binPath
        $nativeDataRelative = [string]$manifest.native.dataPath
        $nativeLogRelative = [string]$manifest.native.logPath
    }
}
else {
    Assert-ConfigurationValues `
        -ConfiguredPort $Port `
        -ConfiguredDatabaseName $DatabaseName `
        -ConfiguredAppUsername $AppUsername
    $appPasswordRelative = $defaultAppPasswordRelative
    $dockerPath = Find-DockerExecutable
    $dockerAvailable = Test-LinuxDockerEngine -DockerPath $dockerPath
    if ($dockerAvailable) {
        $script:DockerExe = $dockerPath
        $dockerStateExists = (Test-DockerVolumeExists -Name $script:DockerVolumeName) -or
            (Test-DockerContainerExists -Name $script:DockerContainerName)
    }
    else {
        $dockerStateExists = $false
    }

    $defaultNativeMarker = Resolve-ContainedRelativePath `
        -Root $databaseRoot `
        -RelativePath (Join-Path $nativeDataRelative "PG_VERSION") `
        -Description "Default native data marker"
    Assert-PathHasNoReparsePoints `
        -PathValue $defaultNativeMarker `
        -Description "Default native PostgreSQL data marker"
    $nativeStateExists = Test-Path -LiteralPath $defaultNativeMarker -PathType Leaf

    if ($nativeStateExists -and $dockerStateExists) {
        throw "Both incomplete native and Docker installed-web-app database state were found without a manifest. Refusing to choose one automatically; recover or remove one state explicitly."
    }

    if ($null -ne $pendingEngine) {
        if ($pendingEngine -eq "docker" -and $nativeStateExists) {
            throw "The interrupted setup selected Docker, but native database state was also found. Refusing to switch or overwrite either backend."
        }

        if ($pendingEngine -eq "native" -and $dockerStateExists) {
            throw "The interrupted setup selected native PostgreSQL, but Docker database state was also found. Refusing to switch or overwrite either backend."
        }

        $engine = $pendingEngine
    }
    elseif ($nativeStateExists) {
        $engine = "native"
    }
    elseif ($dockerAvailable) {
        $engine = "docker"
    }
    else {
        $engine = "native"
    }
}

$appPasswordPath = Resolve-ContainedRelativePath `
    -Root $databaseRoot `
    -RelativePath $appPasswordRelative `
    -Description "App password file"
$adminPasswordPath = Resolve-ContainedRelativePath `
    -Root $databaseRoot `
    -RelativePath $adminPasswordRelative `
    -Description "Admin password file"
$nativeBinPath = Resolve-ContainedRelativePath `
    -Root $databaseRoot `
    -RelativePath $nativeBinRelative `
    -Description "Native bin path"
$nativeDataPath = Resolve-ContainedRelativePath `
    -Root $databaseRoot `
    -RelativePath $nativeDataRelative `
    -Description "Native data path"
$nativeLogPath = Resolve-ContainedRelativePath `
    -Root $databaseRoot `
    -RelativePath $nativeLogRelative `
    -Description "Native log path"

if ($engine -eq "native") {
    Assert-NativeInstallPathSupported -DatabaseRoot $databaseRoot
}

$action = "Install or verify the '$engine' PostgreSQL database for the installed CanDoItAll web app"
if (-not $PSCmdlet.ShouldProcess($databaseRoot, $action)) {
    New-DatabaseSetupResult `
        -Status "Preview" `
        -Engine $engine `
        -ManifestPath $manifestPath `
        -ProtectedAppPasswordPath $appPasswordPath `
        -ConfiguredPort $Port `
        -ConfiguredDatabaseName $DatabaseName `
        -ConfiguredAppUsername $AppUsername
    return
}

if ($engine -eq "docker") {
    if ($null -eq $script:DockerExe) {
        $script:DockerExe = Find-DockerExecutable
    }

    if (-not (Test-LinuxDockerEngine -DockerPath $script:DockerExe)) {
        throw "The existing database engine is Docker, but a working Linux Docker engine is unavailable. Refusing to fall back to native PostgreSQL because that would silently migrate database state."
    }
}
elseif (-not [Environment]::Is64BitOperatingSystem) {
    throw "The selected native PostgreSQL fallback requires 64-bit Windows."
}

foreach ($managedDirectoryPath in @($runtimeRoot, $databaseRoot)) {
    Assert-PathHasNoReparsePoints `
        -PathValue $managedDirectoryPath `
        -Description "Managed installed-web-app database directory"
}
New-Item -ItemType Directory -Path $databaseRoot -Force | Out-Null
Remove-StalePlaintextCredentialDirectories -DatabaseRoot $databaseRoot
if ($null -eq $manifest -and $null -eq $pendingState) {
    Write-PendingDatabaseState `
        -Path $pendingEnginePath `
        -Engine $engine `
        -ConfiguredPort $Port `
        -ConfiguredDatabaseName $DatabaseName `
        -ConfiguredAppUsername $AppUsername
}
$secretRoot = Split-Path -Parent $appPasswordPath

$databaseAlreadyExists = $null -ne $manifest
if (-not $databaseAlreadyExists) {
    if ($engine -eq "docker") {
        $databaseAlreadyExists = (Test-DockerVolumeExists -Name $script:DockerVolumeName) -or
            (Test-DockerContainerExists -Name $script:DockerContainerName)
    }
    else {
        $databaseAlreadyExists = Test-Path -LiteralPath (Join-Path $nativeDataPath "PG_VERSION") -PathType Leaf
    }
}

$secrets = Get-OrCreateProtectedSecrets `
    -SecretRoot $secretRoot `
    -AppPasswordPath $appPasswordPath `
    -AdminPasswordPath $adminPasswordPath `
    -DatabaseAlreadyExists $databaseAlreadyExists

$temporaryFiles = $null
try {
    $temporaryFiles = New-PlaintextCredentialFiles `
        -DatabaseRoot $databaseRoot `
        -AppSecret $secrets.App `
        -AdminSecret $secrets.Admin `
        -ConfiguredDatabaseName $DatabaseName `
        -ConfiguredAppUsername $AppUsername `
        -ServerPort $(if ($engine -eq "docker") { 5432 } else { $Port })

    if ($engine -eq "docker") {
        Invoke-DockerDatabaseSetup `
            -TemporaryRoot $temporaryFiles.Root `
            -ConfiguredPort $Port `
            -ConfiguredDatabaseName $DatabaseName `
            -ConfiguredAppUsername $AppUsername `
            -ManifestExists ($null -ne $manifest)
    }
    else {
        Invoke-NativeDatabaseSetup `
            -DatabaseRoot $databaseRoot `
            -BinPath $nativeBinPath `
            -DataPath $nativeDataPath `
            -LogPath $nativeLogPath `
            -TemporaryRoot $temporaryFiles.Root `
            -AdminPasswordPath $temporaryFiles.AdminPassword `
            -AdminPgPassPath $temporaryFiles.AdminPgPass `
            -AppPgPassPath $temporaryFiles.AppPgPass `
            -ProvisioningSqlPath $temporaryFiles.ProvisioningSql `
            -ConfiguredPort $Port `
            -ConfiguredDatabaseName $DatabaseName `
            -ConfiguredAppUsername $AppUsername
    }

    Write-DatabaseManifest `
        -ManifestPath $manifestPath `
        -Engine $engine `
        -ConfiguredPort $Port `
        -ConfiguredDatabaseName $DatabaseName `
        -ConfiguredAppUsername $AppUsername `
        -AppPasswordFile $appPasswordRelative `
        -NativeBinRelative $nativeBinRelative `
        -NativeDataRelative $nativeDataRelative `
        -NativeLogRelative $nativeLogRelative
    Assert-PathHasNoReparsePoints `
        -PathValue $pendingEnginePath `
        -Description "Pending database state removal path"
    Remove-Item -LiteralPath $pendingEnginePath -Force -ErrorAction SilentlyContinue
}
finally {
    if ($null -ne $temporaryFiles) {
        Remove-SafeTemporaryDirectory -Path $temporaryFiles.Root -DatabaseRoot $databaseRoot
    }
}

Write-Status "Database setup completed with engine '$engine'."
Write-Status "Connection target: $($script:DatabaseHost):$Port/$DatabaseName"

New-DatabaseSetupResult `
    -Status "Ready" `
    -Engine $engine `
    -ManifestPath $manifestPath `
    -ProtectedAppPasswordPath $appPasswordPath `
    -ConfiguredPort $Port `
    -ConfiguredDatabaseName $DatabaseName `
    -ConfiguredAppUsername $AppUsername
}
finally {
    if ($databaseInstallMutexAcquired) {
        $databaseInstallMutex.ReleaseMutex()
    }
    $databaseInstallMutex.Dispose()
    if ($appInstallMutexAcquired) {
        $appInstallMutex.ReleaseMutex()
    }
    $appInstallMutex.Dispose()
}
