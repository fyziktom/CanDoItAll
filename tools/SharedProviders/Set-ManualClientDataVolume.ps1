#requires -Version 7.5
[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9][a-z0-9_.-]{1,100}$')][string]$ContainerName,
    [Parameter(Mandatory)][ValidatePattern('^[a-f0-9]{64}$')][string]$ExpectedContainerId,
    [Parameter(Mandatory)][ValidatePattern('^[a-z0-9][a-z0-9_.-]{1,100}$')][string]$VolumeName,
    [Parameter(Mandatory)][string]$EvidenceDirectory,
    [switch]$Recreate,
    [ValidateRange(15, 300)][int]$HealthTimeoutSeconds = 120
)

enum ManualClientRunState {
    Idle
    Preparing
    Running
    WaitingOnTool
    Persisting
    Completed
    Failed
}

$ErrorActionPreference = 'Stop'
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$artifactRoot = Join-Path $repositoryRoot '.artifacts'
$expectedProject = 'candoitall-shared-providers-manual'
$expectedService = 'client-a'
$ownerLabel = 'io.candoitall.manual-client-data-owner'
$schemaLabel = 'io.candoitall.manual-client-data-schema'
$helperLabel = 'io.candoitall.manual-client-data-operation'
$volumeOwner = "$expectedProject/$expectedService"
$operationId = [Guid]::NewGuid().ToString('N')
$backupName = "$ContainerName-data-backup-$($operationId.Substring(0, 12))"
$maximumArchiveBytes = 64MB
$maximumEntryBytes = 16MB
$maximumEntries = 10000
$helpers = [Collections.Generic.List[string]]::new()
$phase = 'ReadOnlyPreflight'
$report = $null
$candidateId = $null
$evidenceCreated = $false

function Stop-DataOperation {
    param([string]$Message)
    $failure = [InvalidOperationException]::new($Message)
    $failure.Data['ManualClientDataSafeMessage'] = $true
    throw $failure
}

function Copy-Value {
    param($Value)
    return ConvertFrom-Json -InputObject (ConvertTo-Json -InputObject $Value -Depth 100 -Compress) -AsHashtable -DateKind String
}

function Get-CanonicalValue {
    param($Value)
    if ($null -eq $Value) {
        return $null
    }
    if ($Value -is [Collections.IDictionary]) {
        $result = [ordered]@{}
        foreach ($key in @($Value.Keys | Sort-Object -CaseSensitive)) {
            $result[$key] = Get-CanonicalValue $Value[$key]
        }
        return $result
    }
    if ($Value -is [Collections.IEnumerable] -and $Value -isnot [string]) {
        return ,@($Value | ForEach-Object { Get-CanonicalValue $_ })
    }
    return $Value
}

function Get-BytesHash {
    param([byte[]]$Bytes)
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($Bytes))
}

function Get-ValueHash {
    param($Value)
    $json = ConvertTo-Json -InputObject (Get-CanonicalValue $Value) -Depth 100 -Compress
    return Get-BytesHash ([Text.Encoding]::UTF8.GetBytes($json))
}

function Assert-LocalPath {
    param([string]$Path, [string]$Root)
    $full = [IO.Path]::GetFullPath($Path)
    $base = [IO.Path]::GetFullPath($Root).TrimEnd([IO.Path]::DirectorySeparatorChar)
    if (!$full.StartsWith($base + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        Stop-DataOperation 'A local path is outside its authorized repository artifact root.'
    }
    $ancestor = $full
    while ($null -ne $ancestor) {
        if ((Test-Path -LiteralPath $ancestor) -and ([IO.File]::GetAttributes($ancestor) -band [IO.FileAttributes]::ReparsePoint)) {
            Stop-DataOperation 'A local path contains a symlink or reparse point.'
        }
        $ancestor = [IO.Path]::GetDirectoryName($ancestor)
    }
    return $full
}

function New-PrivateEvidenceDirectory {
    $directory = [IO.Directory]::CreateDirectory($EvidenceDirectory)
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent().User
    $security = [Security.AccessControl.DirectorySecurity]::new()
    $security.SetOwner($identity)
    $security.SetAccessRuleProtection($true, $false)
    $rule = [Security.AccessControl.FileSystemAccessRule]::new($identity, 'FullControl', 'ContainerInherit,ObjectInherit', 'None', 'Allow')
    $security.AddAccessRule($rule)
    Set-Acl -LiteralPath $directory.FullName -AclObject $security
    $actual = Get-Acl -LiteralPath $directory.FullName
    $rules = @($actual.GetAccessRules($true, $true, [Security.Principal.SecurityIdentifier]))
    if (!$actual.AreAccessRulesProtected -or $rules.Count -ne 1 -or $rules[0].IdentityReference -ne $identity -or $rules[0].AccessControlType -ne 'Allow') {
        Stop-DataOperation 'Private evidence directory permissions could not be verified.'
    }
}

function Invoke-Engine {
    param([ValidateSet('GET', 'POST', 'PUT', 'DELETE')][string]$Method, [string]$Path, $Body = $null, [switch]$AllowMissing, [int]$TimeoutSeconds = 55, [switch]$Archive, [byte[]]$ArchiveBody)
    $contentType = 'application/json'
    $requestBytes = $null
    if ($null -ne $ArchiveBody) {
        $requestBytes = $ArchiveBody
        $contentType = 'application/x-tar'
    } elseif ($null -ne $Body) {
        $requestBytes = [Text.Encoding]::UTF8.GetBytes((ConvertTo-Json -InputObject $Body -Depth 100 -Compress))
    }
    $limit = if ($Archive) { $maximumArchiveBytes } else { 16MB }
    $bytes = [ManualClientDataDockerEngine]::Request($Method, $Path, $requestBytes, $contentType, [bool]$AllowMissing, $TimeoutSeconds, $limit)
    if ($null -eq $bytes) {
        return $null
    }
    if ($Archive) {
        return ,$bytes
    }
    if ($bytes.Length -eq 0) {
        return $null
    }
    return [Text.Encoding]::UTF8.GetString($bytes) | ConvertFrom-Json -AsHashtable -DateKind String
}

function Get-Container {
    param([string]$Identity, [switch]$AllowMissing, [int]$TimeoutSeconds = 20)
    return Invoke-Engine GET "/containers/$([Uri]::EscapeDataString($Identity))/json" -AllowMissing:$AllowMissing -TimeoutSeconds $TimeoutSeconds
}

function Get-NetworkShape {
    param($Container, [switch]$DeclaredOnly)
    $networks = [ordered]@{}
    foreach ($name in @($Container.NetworkSettings.Networks.Keys | Sort-Object -CaseSensitive)) {
        $endpoint = $Container.NetworkSettings.Networks[$name]
        $networks[$name] = [ordered]@{
            Aliases = $endpoint.Aliases
            IPAMConfig = $endpoint.IPAMConfig
            Links = $endpoint.Links
            DriverOpts = $endpoint.DriverOpts
            GwPriority = $endpoint.GwPriority
        }
        if (!$DeclaredOnly) {
            $networks[$name].NetworkID = $endpoint.NetworkID
        }
    }
    return $networks
}

function Assert-ReferencedNetworks {
    param($Container)
    foreach ($name in $Container.NetworkSettings.Networks.Keys) {
        $network = Invoke-Engine GET "/networks/$([Uri]::EscapeDataString($name))"
        if ($network.Id -ne $Container.NetworkSettings.Networks[$name].NetworkID) {
            Stop-DataOperation 'A referenced network name no longer resolves to its original network identity.'
        }
    }
}

function Get-ContainerHash {
    param($Container)
    return Get-ValueHash ([ordered]@{
        Id = $Container.Id
        Image = $Container.Image
        StartedAt = $Container.State.StartedAt
        Running = $Container.State.Running
        Config = $Container.Config
        HostConfig = $Container.HostConfig
        Mounts = @($Container.Mounts | Sort-Object Destination)
        Networks = Get-NetworkShape $Container
    })
}

function Assert-Client {
    param($Container, [string]$ExpectedId, [switch]$RequireHealthy)
    if ($Container.Id -ne $ExpectedId -or $Container.Name -ne '/' + $ContainerName -or
        $Container.Config.Labels['com.docker.compose.project'] -ne $expectedProject -or
        $Container.Config.Labels['com.docker.compose.service'] -ne $expectedService) {
        Stop-DataOperation 'The exact manual client container identity or Compose ownership differs.'
    }
    $settings = $Container.HostConfig
    if ($Container.Config.User -ne '1654:1654' -or !$settings.ReadonlyRootfs -or $settings.Privileged -or
        $settings.AutoRemove -or $settings.PublishAllPorts -or $settings.NetworkMode -eq 'host' -or
        $settings.PidMode -eq 'host' -or $settings.IpcMode -eq 'host' -or
        @($settings.CapAdd | Where-Object { $null -ne $_ }).Count -ne 0 -or
        @($settings.CapDrop) -notcontains 'ALL' -or @($settings.SecurityOpt) -notcontains 'no-new-privileges:true' -or
        !$settings.Tmpfs.Contains('/tmp')) {
        Stop-DataOperation 'The manual client security boundary is not the supported configuration.'
    }
    $binding = @($settings.PortBindings['8080/tcp'])
    if ($settings.PortBindings.Count -ne 1 -or $binding.Count -ne 1 -or
        $binding[0].HostIp -ne '127.0.0.1' -or $binding[0].HostPort -ne '5214') {
        Stop-DataOperation 'The target does not exclusively publish loopback5214.'
    }
    if ($RequireHealthy -and (!$Container.State.Running -or $Container.State.Health.Status -ne 'healthy')) {
        Stop-DataOperation 'The exact manual client must be running and healthy before replacement.'
    }
    $mounts = @($Container.Mounts | Where-Object Destination -EQ '/data')
    if ($mounts.Count -ne 1 -or !$mounts[0].RW -or $mounts[0].Type -notin @('bind', 'volume')) {
        Stop-DataOperation 'The client must have exactly one writable bind or named volume at /data.'
    }
    foreach ($mount in $Container.Mounts) {
        if ($mount.Destination -ne '/data' -and $mount.Destination.StartsWith('/data/', [StringComparison]::Ordinal)) {
            Stop-DataOperation 'Nested data mounts cannot be migrated as one archive.'
        }
        if ($mount.Destination -match '(?i)docker[.]sock|docker_engine' -or $mount.Source -match '(?i)docker[.]sock|docker_engine') {
            Stop-DataOperation 'Docker control mounts are not supported.'
        }
    }
    return $mounts[0]
}

function Assert-Volume {
    param($Volume)
    if ($null -eq $Volume -or $Volume.Name -ne $VolumeName -or $Volume.Driver -ne 'local' -or $Volume.Scope -ne 'local' -or
        ($null -ne $Volume.Options -and $Volume.Options.Count -ne 0) -or
        $Volume.Labels[$ownerLabel] -ne $volumeOwner -or $Volume.Labels[$schemaLabel] -ne '1') {
        Stop-DataOperation 'The named volume has foreign ownership, driver options or an unsupported identity.'
    }
}

function Assert-NoOtherVolumeWriter {
    param([string]$AllowedId)
    $filters = [Uri]::EscapeDataString((ConvertTo-Json -Compress -InputObject @{ volume = @($VolumeName) }))
    $attached = @(Invoke-Engine GET "/containers/json?all=true&filters=$filters")
    foreach ($container in $attached) {
        if ($container.Id -ne $AllowedId -and $container.State -in @('running', 'restarting', 'paused')) {
            Stop-DataOperation 'Another running, restarting or paused container is attached to the target volume.'
        }
    }
}

function Assert-NoOtherBindWriter {
    param([string]$Source, [string]$AllowedId)
    $containers = @(Invoke-Engine GET '/containers/json?all=true')
    foreach ($container in $containers) {
        if ($container.Id -eq $AllowedId -or $container.State -notin @('running', 'restarting', 'paused')) {
            continue
        }
        foreach ($mount in @($container.Mounts | Where-Object { $_.Type -eq 'bind' -and $_.RW })) {
            $path = [string]$mount.Source
            if ($path -match '^/(?:run/desktop/mnt/host|host_mnt)/(?<drive>[a-z])/(?<relative>.*)$') {
                $path = $Matches.drive + ':/' + $Matches.relative
            }
            if ($path -notmatch '^[a-zA-Z]:[\\/]') {
                continue
            }
            $other = [IO.Path]::GetFullPath($path).TrimEnd([IO.Path]::DirectorySeparatorChar)
            if ($other.Equals($Source, [StringComparison]::OrdinalIgnoreCase) -or
                $other.StartsWith($Source + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
                $Source.StartsWith($other + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
                Stop-DataOperation 'Another active container has a writable bind overlapping the original data source.'
            }
        }
    }
}

function Read-DataArchive {
    param([string]$Identity)
    [byte[]]$bytes = Invoke-Engine GET "/containers/$Identity/archive?path=%2Fdata" -Archive
    $stream = [IO.MemoryStream]::new($bytes, $false)
    $reader = [System.Formats.Tar.TarReader]::new($stream, $false)
    $entries = [Collections.Generic.List[object]]::new()
    $names = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $runs = [Collections.Generic.List[object]]::new()
    $runDirectories = [Collections.Generic.HashSet[string]]::new([StringComparer]::Ordinal)
    $totalBytes = [long]0
    try {
        while ($null -ne ($entry = $reader.GetNextEntry())) {
            $name = $entry.Name.TrimEnd('/')
            if ($name.Length -gt 2048 -or $name -match '[\x00-\x1f\x7f\\]' -or $name.StartsWith('/') -or
                @($name.Split('/') | Where-Object { $_ -in @('', '.', '..') }).Count -gt 0 -or
                ($name -ne 'data' -and !$name.StartsWith('data/', [StringComparison]::Ordinal)) -or !$names.Add($name)) {
                Stop-DataOperation 'The archive contains an unsafe, duplicate or unexpected path.'
            }
            if ($entry.EntryType -notin @([System.Formats.Tar.TarEntryType]::Directory, [System.Formats.Tar.TarEntryType]::RegularFile, [System.Formats.Tar.TarEntryType]::V7RegularFile)) {
                Stop-DataOperation 'The archive contains a link or unsupported special entry.'
            }
            if ($entries.Count -ge $maximumEntries -or $entry.Length -gt $maximumEntryBytes) {
                Stop-DataOperation 'The data archive exceeds its bounded entry limits.'
            }
            $relative = if ($name -eq 'data') { '' } else { $name.Substring(5) }
            $isDirectory = $entry.EntryType -eq [System.Formats.Tar.TarEntryType]::Directory
            $record = [ordered]@{
                Path = $relative
                Kind = if ($isDirectory) { 'Directory' } else { 'File' }
                Mode = [int]$entry.Mode
                Uid = $entry.Uid
                Gid = $entry.Gid
                ModifiedUtc = $entry.ModificationTime.ToUniversalTime().ToString('O')
                Bytes = $entry.Length
                Sha256 = $null
            }
            if (!$isDirectory) {
                $content = [IO.MemoryStream]::new()
                try {
                    if ($null -ne $entry.DataStream) {
                        $entry.DataStream.CopyTo($content)
                    }
                    [byte[]]$fileBytes = $content.ToArray()
                } finally {
                    $content.Dispose()
                }
                $totalBytes += $fileBytes.Length
                if ($fileBytes.Length -ne $entry.Length -or $totalBytes -gt $maximumArchiveBytes) {
                    Stop-DataOperation 'The archive contains an incomplete or oversized file.'
                }
                $record.Sha256 = Get-BytesHash $fileBytes
                if ($relative -match '(^|/)(pending[^/]*[.]json|[^/]+[.]pending[.]json)$') {
                    Stop-DataOperation 'A pending execution journal blocks this operation; no recovery was invoked.'
                }
                if ($relative -match '^workspace/data/scopes/.+/execution/runs/(?<run>[a-f0-9]{32})/run[.]json$') {
                    $runId = $Matches.run
                    $run = [Text.Encoding]::UTF8.GetString($fileBytes) | ConvertFrom-Json -AsHashtable -DateKind String
                    $state = [ManualClientRunState]::Idle
                    $completed = [DateTimeOffset]::MinValue
                    if (![Enum]::TryParse[ManualClientRunState]([string]$run.state, $false, [ref]$state) -or
                        ![Enum]::IsDefined([ManualClientRunState], $state) -or $state -notin @([ManualClientRunState]::Completed, [ManualClientRunState]::Failed) -or
                        ![DateTimeOffset]::TryParse([string]$run.completedAtUtc, [ref]$completed) -or
                        ([Guid]$run.id).ToString('N') -ne $runId -or
                        @($run.pendingApprovals | Where-Object { $null -ne $_ }).Count -gt 0) {
                        Stop-DataOperation 'An execution run is not terminal, has pending approvals or has invalid identity.'
                    }
                    $runs.Add([ordered]@{ Path = $relative; Sha256 = $record.Sha256 })
                }
            } elseif ($relative -match '^workspace/data/scopes/.+/execution/runs/[a-f0-9]{32}$') {
                [void]$runDirectories.Add($relative)
            }
            $entries.Add($record)
        }
    } finally {
        $reader.Dispose()
        $stream.Dispose()
    }
    $root = @($entries | Where-Object Path -EQ '')
    if ($root.Count -ne 1 -or $root[0].Kind -ne 'Directory') {
        Stop-DataOperation 'The archive does not contain exactly one data root directory.'
    }
    if ($runDirectories.Count -ne $runs.Count) {
        Stop-DataOperation 'An execution run directory lacks its required terminal run record.'
    }
    $orderedEntries = @($entries | Sort-Object Path -CaseSensitive)
    return [ordered]@{
        Bytes = $bytes
        Entries = $orderedEntries
        ArchiveSha256 = Get-BytesHash $bytes
        ManifestSha256 = Get-ValueHash $orderedEntries
        FileCount = @($entries | Where-Object Kind -EQ 'File').Count
        DirectoryCount = @($entries | Where-Object Kind -EQ 'Directory').Count
        DataBytes = $totalBytes
        RunCount = $runs.Count
        RunsSha256 = Get-ValueHash @($runs | Sort-Object Path -CaseSensitive)
        Root = $root[0]
    }
}

function Assert-DataPresent {
    param($Snapshot)
    if ($Snapshot.FileCount -eq 0 -or @($Snapshot.Entries | Where-Object { $_.Kind -eq 'File' -and $_.Path -match '^workspace/data/scopes/.+/workspace[.]json$' }).Count -eq 0) {
        Stop-DataOperation 'The archive lacks the expected scoped client workspace catalog.'
    }
}

function Assert-LinuxDataPermissions {
    param($Snapshot)
    foreach ($entry in $Snapshot.Entries) {
        $permissions = if ($entry.Path -eq '' -or $entry.Uid -eq 1654) {
            ($entry.Mode -shr 6) -band 7
        } elseif ($entry.Gid -eq 1654) {
            ($entry.Mode -shr 3) -band 7
        } else {
            $entry.Mode -band 7
        }
        $required = if ($entry.Kind -eq 'Directory') { 7 } else { 6 }
        if (($permissions -band $required) -ne $required) {
            Stop-DataOperation 'Preserved Linux permissions would deny app1654 required data access; descendant ownership was not normalized.'
        }
    }
}

function Save-DataSnapshot {
    param($Snapshot, [string]$Prefix)
    [IO.File]::WriteAllBytes((Join-Path $EvidenceDirectory "$Prefix.tar"), $Snapshot.Bytes)
    $Snapshot.Entries | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $EvidenceDirectory "$Prefix.manifest.json") -Encoding utf8NoBOM
}

function New-RestoreArchive {
    param([byte[]]$Bytes)
    $input = [IO.MemoryStream]::new($Bytes, $false)
    $output = [IO.MemoryStream]::new()
    $reader = [System.Formats.Tar.TarReader]::new($input, $false)
    $writer = [System.Formats.Tar.TarWriter]::new($output, [System.Formats.Tar.TarEntryFormat]::Pax, $true)
    try {
        while ($null -ne ($entry = $reader.GetNextEntry())) {
            $copy = [System.Formats.Tar.PaxTarEntry]::new($entry)
            $name = $entry.Name.TrimEnd('/')
            $copy.Name = if ($name -eq 'data') { '.' } else { $name.Substring(5) }
            $writer.WriteEntry($copy)
        }
        $writer.Dispose()
        if ($output.Length -gt $maximumArchiveBytes) {
            Stop-DataOperation 'The relative restore archive exceeds the capture limit.'
        }
        return ,$output.ToArray()
    } finally {
        $writer.Dispose()
        $reader.Dispose()
        $input.Dispose()
        $output.Dispose()
    }
}

function New-VolumeHelper {
    param([string]$ImageId, [switch]$Restore, [byte[]]$RestoreArchive)
    $mounts = @(@{ Type = 'volume'; Source = $VolumeName; Target = '/data'; ReadOnly = !$Restore; VolumeOptions = @{ NoCopy = $true } })
    $arguments = @('true')
    $capabilities = @()
    if ($Restore) {
        $input = [IO.MemoryStream]::new($RestoreArchive, $false)
        $reader = [System.Formats.Tar.TarReader]::new($input, $false)
        try {
            $root = $reader.GetNextEntry()
            if ($root.Name -ne '.' -or $root.EntryType -ne [System.Formats.Tar.TarEntryType]::Directory) {
                Stop-DataOperation 'The restore payload lacks its validated root metadata.'
            }
            $mode = [Convert]::ToString([int]$root.Mode, 8)
            $timestamp = $root.ModificationTime.ToUniversalTime().ToString('yyyy-MM-dd HH:mm:ss', [Globalization.CultureInfo]::InvariantCulture)
        } finally {
            $reader.Dispose()
            $input.Dispose()
        }
        $arguments = @('sh', '-ec', @'
/usr/local/bin/busybox chmod "$1" /data
/usr/local/bin/busybox touch -m -d "$2" /data
/usr/local/bin/busybox chown 1654:1654 /data
'@, 'restore-root', $mode, $timestamp)
        $capabilities = @('CHOWN', 'FOWNER')
    }
    $configuration = @{
        Image = $ImageId
        User = '0:0'
        Entrypoint = @('/usr/local/bin/busybox')
        Cmd = $arguments
        Env = @('TZ=UTC0')
        WorkingDir = '/'
        Labels = @{ $helperLabel = $operationId }
        Healthcheck = @{ Test = @('NONE') }
        HostConfig = @{
            NetworkMode = 'none'
            ReadonlyRootfs = $true
            CapDrop = @('ALL')
            CapAdd = $capabilities
            SecurityOpt = @('no-new-privileges:true')
            Memory = 256MB
            NanoCpus = 1000000000
            PidsLimit = 64
            RestartPolicy = @{ Name = 'no'; MaximumRetryCount = 0 }
            AutoRemove = $false
            Mounts = $mounts
            LogConfig = @{ Type = 'none'; Config = @{} }
        }
    }
    $name = "$ContainerName-data-helper-$($operationId.Substring(0, 12))-$($helpers.Count)"
    $created = Invoke-Engine POST "/containers/create?name=$name" $configuration
    if ($created.Id -notmatch '^[a-f0-9]{64}$' -or $created.Id -eq $ExpectedContainerId) {
        Stop-DataOperation 'The volume helper did not return a valid owned identity.'
    }
    $identity = [string]$created.Id
    $helpers.Add($identity)
    if ($Restore) {
        $empty = Read-DataArchive $identity
        if ($empty.FileCount -ne 0 -or $empty.DirectoryCount -ne 1 -or $null -eq $RestoreArchive) {
            Stop-DataOperation 'The restore target is not empty or lacks its bounded archive payload.'
        }
        [void](Invoke-Engine PUT "/containers/$identity/archive?path=%2Fdata&copyUIDGID=false&noOverwriteDirNonDir=true" -ArchiveBody $RestoreArchive)
    }
    [void](Invoke-Engine POST "/containers/$identity/start")
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds(135)
    do {
        $current = Get-Container $identity
        if (!$current.State.Running) {
            if ($current.State.ExitCode -ne 0 -or $current.State.OOMKilled) {
                Stop-DataOperation 'The bounded volume helper failed; its payload and filesystem names were not logged.'
            }
            return $identity
        }
        Start-Sleep -Milliseconds 250
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    Stop-DataOperation 'The owned volume helper exceeded its deadline; it was retained without force removal.'
}

function Get-CandidateCreateBody {
    param($Client, [switch]$AlreadyVolume)
    $body = Copy-Value $Client.Config
    $body.HostConfig = Copy-Value $Client.HostConfig
    if (!$AlreadyVolume) {
        $changed = 0
        $binds = @()
        foreach ($bind in @($body.HostConfig.Binds)) {
            if ($bind -match '^.+:/data(?::[^:]*)?$') {
                $binds += "${VolumeName}:/data:rw,nocopy"
                $changed++
            } else {
                $binds += $bind
            }
        }
        if ($null -ne $body.HostConfig.Binds) {
            $body.HostConfig.Binds = $binds
        }
        foreach ($mount in @($body.HostConfig.Mounts)) {
            if ($mount.Target -eq '/data') {
                if ($mount.Type -ne 'bind' -or $mount.ReadOnly) {
                    Stop-DataOperation 'The source HostConfig data mount is not the expected writable bind.'
                }
                $mount.Type = 'volume'
                $mount.Source = $VolumeName
                [void]$mount.Remove('BindOptions')
                $mount.VolumeOptions = @{ NoCopy = $true }
                $changed++
            }
        }
        if ($changed -ne 1) {
            Stop-DataOperation 'Exactly one existing HostConfig data bind must be replaced.'
        }
    }
    $endpoints = [ordered]@{}
    foreach ($name in $Client.NetworkSettings.Networks.Keys) {
        $endpoint = $Client.NetworkSettings.Networks[$name]
        $endpoints[$name] = @{
            Aliases = $endpoint.Aliases
            IPAMConfig = $endpoint.IPAMConfig
            Links = $endpoint.Links
            DriverOpts = $endpoint.DriverOpts
            GwPriority = $endpoint.GwPriority
        }
    }
    $body.NetworkingConfig = @{ EndpointsConfig = $endpoints }
    return $body
}

function Get-BindSourceComparisonValue {
    param([string]$Source)
    $path = $Source
    if ($path -cmatch '^/run/desktop/mnt/host/(?<drive>[a-z])(?:/(?<relative>.*))?$') {
        $drive = $Matches.drive
        $relative = [string]$Matches.relative
        if ($relative.Contains('\') -or @($relative.Split('/') | Where-Object { $_ -in @('.', '..') }).Count -ne 0) {
            return $Source
        }
        $path = $drive + ':/' + $relative
    }
    if ($path -notmatch '^[a-z]:[\\/]') {
        return $Source
    }
    return [IO.Path]::GetFullPath($path).Replace('\', '/').TrimEnd('/').ToUpperInvariant()
}

function Get-HostConfigComparisonValue {
    param($HostConfig)
    $value = Copy-Value $HostConfig
    if ($value.Contains('OomKillDisable') -and $null -eq $value.OomKillDisable) {
        $value.OomKillDisable = $false
    }
    foreach ($mount in @($value.Mounts)) {
        if ($mount.Type -eq 'bind') {
            $mount.Source = Get-BindSourceComparisonValue $mount.Source
        }
    }
    if ($null -ne $value.Binds) {
        $value.Binds = @($value.Binds | ForEach-Object {
            if ($_ -match '^(?<source>.+):(?<target>/[^:]*)(?<options>:[^:]*)?$') {
                (Get-BindSourceComparisonValue $Matches.source) + ':' + $Matches.target + $Matches.options
            } else {
                $_
            }
        })
    }
    return $value
}

function Get-MountComparisonValues {
    param($Mounts)
    foreach ($mount in @($Mounts)) {
        $value = Copy-Value $mount
        if ($value.Type -eq 'bind') {
            $value.Source = Get-BindSourceComparisonValue $value.Source
        }
        $value
    }
}

function Assert-CandidatePreserved {
    param($Candidate, $Original, $CreateBody, [switch]$BeforeStart)
    $mount = Assert-Client $Candidate $Candidate.Id
    Assert-Volume (Invoke-Engine GET "/volumes/$VolumeName")
    $expectedConfig = Copy-Value $CreateBody
    [void]$expectedConfig.Remove('HostConfig')
    [void]$expectedConfig.Remove('NetworkingConfig')
    if ($Candidate.Image -ne $Original.Image -or $mount.Type -ne 'volume' -or $mount.Name -ne $VolumeName -or
        (Get-ValueHash $Candidate.Config) -ne (Get-ValueHash $expectedConfig) -or
        (Get-ValueHash (Get-HostConfigComparisonValue $Candidate.HostConfig)) -ne (Get-ValueHash (Get-HostConfigComparisonValue $CreateBody.HostConfig)) -or
        (Get-ValueHash @(Get-MountComparisonValues @($Candidate.Mounts | Where-Object Destination -NE '/data' | Sort-Object Destination))) -ne
            (Get-ValueHash @(Get-MountComparisonValues @($Original.Mounts | Where-Object Destination -NE '/data' | Sort-Object Destination))) -or
        (Get-ValueHash (Get-NetworkShape $Candidate -DeclaredOnly:$BeforeStart)) -ne (Get-ValueHash (Get-NetworkShape $Original -DeclaredOnly:$BeforeStart))) {
        Stop-DataOperation 'Candidate configuration differs beyond the explicitly replaced data mount.'
    }
    if ($BeforeStart) {
        foreach ($name in $Candidate.NetworkSettings.Networks.Keys) {
            $identity = $Candidate.NetworkSettings.Networks[$name].NetworkID
            if (![string]::IsNullOrEmpty($identity) -and $identity -ne $Original.NetworkSettings.Networks[$name].NetworkID) {
                Stop-DataOperation 'An already-resolved candidate endpoint refers to a different network.'
            }
        }
    }
}

function Wait-Healthy {
    param([string]$Identity)
    $deadline = [DateTimeOffset]::UtcNow.AddSeconds($HealthTimeoutSeconds)
    do {
        $remaining = [int][Math]::Ceiling(($deadline - [DateTimeOffset]::UtcNow).TotalSeconds)
        if ($remaining -le 0) {
            break
        }
        $container = Get-Container $Identity -TimeoutSeconds ([Math]::Min(10, $remaining))
        if (!$container.State.Running -or $container.State.Health.Status -eq 'unhealthy') {
            Stop-DataOperation 'The candidate stopped or became unhealthy.'
        }
        if ($container.State.Health.Status -eq 'healthy') {
            return $container
        }
        Start-Sleep -Seconds 1
    } while ([DateTimeOffset]::UtcNow -lt $deadline)
    Stop-DataOperation 'The candidate did not become healthy within the bounded deadline.'
}

if (!$IsWindows -or $ContainerName -ne "$expectedProject-$expectedService-1") {
    Stop-DataOperation 'This operator supports the exact manual client on Windows Docker Desktop Linux only.'
}
$EvidenceDirectory = Assert-LocalPath $EvidenceDirectory $artifactRoot
if (Test-Path -LiteralPath $EvidenceDirectory) {
    Stop-DataOperation 'EvidenceDirectory must be a fresh ignored directory under this repository .artifacts.'
}
$docker = (Get-Command docker.exe -CommandType Application -ErrorAction Stop).Source
$endpoint = (& $docker context inspect --format '{{.Endpoints.docker.Host}}' 2>$null).Trim()
if ($LASTEXITCODE -ne 0 -or $endpoint -ne 'npipe:////./pipe/dockerDesktopLinuxEngine') {
    Stop-DataOperation 'The active Docker context does not target Docker Desktop Linux.'
}
Add-Type -AssemblyName System.Formats.Tar
if (!('ManualClientDataDockerEngine' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.IO.Pipes;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
public static class ManualClientDataDockerEngine {
    public static byte[] Request(string method, string path, byte[] body, string contentType, bool allowMissing, int timeoutSeconds, int maximumBytes) {
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var handler = new SocketsHttpHandler {
            ConnectCallback = async (_, token) => {
                var pipe = new NamedPipeClientStream(".", "dockerDesktopLinuxEngine", PipeDirection.InOut, PipeOptions.Asynchronous);
                try {
                    await pipe.ConnectAsync(token);
                    return pipe;
                } catch {
                    pipe.Dispose();
                    throw;
                }
            }
        };
        using var client = new HttpClient(handler);
        using var request = new HttpRequestMessage(new HttpMethod(method), "http://localhost" + path);
        if (body is not null) {
            request.Content = new ByteArrayContent(body);
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue(contentType);
        }
        using var response = client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation.Token).GetAwaiter().GetResult();
        if (allowMissing && response.StatusCode == HttpStatusCode.NotFound) {
            return null;
        }
        if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotModified) {
            throw new InvalidOperationException("Docker request failed with HTTP " + (int)response.StatusCode + "; response payload omitted.");
        }
        if (response.Content.Headers.ContentLength > maximumBytes) {
            throw new InvalidOperationException("Docker response exceeds the bounded capture limit.");
        }
        using var input = response.Content.ReadAsStreamAsync(cancellation.Token).GetAwaiter().GetResult();
        using var output = new MemoryStream();
        byte[] buffer = new byte[65536];
        int count;
        while ((count = input.ReadAsync(buffer.AsMemory(), cancellation.Token).AsTask().GetAwaiter().GetResult()) != 0) {
            if (output.Length + count > maximumBytes) {
                throw new InvalidOperationException("Docker response exceeds the bounded capture limit.");
            }
            output.Write(buffer, 0, count);
        }
        return output.ToArray();
    }
}
'@
}

try {
    $client = Get-Container $ExpectedContainerId
    $dataMount = Assert-Client $client $ExpectedContainerId -RequireHealthy
    $alreadyVolume = $dataMount.Type -eq 'volume'
    if ($alreadyVolume -and $dataMount.Name -ne $VolumeName) {
        Stop-DataOperation 'The existing data volume is not the explicitly requested volume.'
    }
    if (!$alreadyVolume) {
        $source = Assert-LocalPath $dataMount.Source (Join-Path $artifactRoot 'shared-providers-e2e/client-a')
        if ($source -ne [IO.Path]::GetFullPath((Join-Path $artifactRoot 'shared-providers-e2e/client-a/data')) -or
            $EvidenceDirectory.StartsWith($source + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            Stop-DataOperation 'The original data bind is not the supported manual client data directory.'
        }
        Assert-NoOtherBindWriter $source $ExpectedContainerId
    }
    $volume = Invoke-Engine GET "/volumes/$VolumeName" -AllowMissing
    if ($null -ne $volume -or $alreadyVolume) {
        Assert-Volume $volume
        Assert-NoOtherVolumeWriter $ExpectedContainerId
    }
    $publisher = Get-Container "$expectedProject-central-1"
    if (!$publisher.State.Running -or $publisher.Config.Labels['com.docker.compose.project'] -ne $expectedProject -or
        $publisher.Config.Labels['com.docker.compose.service'] -ne 'central') {
        Stop-DataOperation 'The publisher identity could not be established for preservation evidence.'
    }
    $publisherHash = Get-ContainerHash $publisher
    $referencedImage = Invoke-Engine GET "/images/$([Uri]::EscapeDataString($client.Config.Image))/json"
    if ($referencedImage.Id -ne $client.Image) {
        Stop-DataOperation 'The configured image reference no longer resolves to the running immutable image.'
    }
    Assert-ReferencedNetworks $client
    $clientHash = Get-ContainerHash $client
    $preflight = Read-DataArchive $ExpectedContainerId
    Assert-DataPresent $preflight
    Assert-LinuxDataPermissions $preflight
    if ($alreadyVolume -and !$Recreate) {
        [pscustomobject]@{ Outcome = 'AlreadyOnOwnedVolume'; ContainerId = $ExpectedContainerId; VolumeName = $VolumeName; RunCount = $preflight.RunCount; ManifestSha256 = $preflight.ManifestSha256; Mutation = $false }
        return
    }
    $createBody = Get-CandidateCreateBody $client -AlreadyVolume:$alreadyVolume
    if ($null -ne (Get-Container $backupName -AllowMissing)) {
        Stop-DataOperation 'The generated retained-container backup name already exists.'
    }
    $action = if ($alreadyVolume) { 'Recreate only client5214 against its existing owned volume, retaining the stopped original and a private archive' } else { 'Migrate only client5214 data into the owned volume, retaining the original bind, stopped container and private archive' }
    if (!$PSCmdlet.ShouldProcess("$ExpectedContainerId -> $VolumeName", $action)) {
        [pscustomobject]@{
            Outcome = 'ReadOnlyPreflight'
            ContainerId = $ExpectedContainerId
            ImageId = $client.Image
            VolumeName = $VolumeName
            VolumeExists = $null -ne $volume
            EmptyVolumeCheckDeferred = !$alreadyVolume -and $null -ne $volume
            FileCount = $preflight.FileCount
            DataBytes = $preflight.DataBytes
            RunCount = $preflight.RunCount
            ManifestSha256 = $preflight.ManifestSha256
            Mutation = $false
        }
        return
    }
    New-PrivateEvidenceDirectory
    $evidenceCreated = $true
    $report = [ordered]@{
        SchemaVersion = 1
        OperationId = $operationId
        StartedUtc = [DateTimeOffset]::UtcNow.ToString('O')
        ContainerName = $ContainerName
        OriginalContainerId = $ExpectedContainerId
        ImageId = $client.Image
        VolumeName = $VolumeName
        VolumeOwner = $volumeOwner
        ComposeVariable = 'MANUAL_CLIENT_A_DATA_VOLUME'
        Recreate = [bool]$alreadyVolume
        BackupName = $backupName
        ConfigSha256 = Get-ValueHash $client.Config
        OriginalHostConfigSha256 = Get-ValueHash $client.HostConfig
        ExpectedHostConfigSha256 = Get-ValueHash $createBody.HostConfig
        ExpectedHostConfigComparisonSha256 = Get-ValueHash (Get-HostConfigComparisonValue $createBody.HostConfig)
        ComparisonNormalizations = @('Known Docker Desktop /run/desktop/mnt/host/drive aliases and absolute Windows bind paths compare as the same case-insensitive Windows path.', 'OomKillDisable null compares as false; OOM killing remains enabled.')
        NetworkSha256 = Get-ValueHash (Get-NetworkShape $client)
        PublisherContainerId = $publisher.Id
        OriginalStopped = $false
        OriginalRenamed = $false
        CandidateStartAttempted = $false
        RootOwnershipNormalization = 'Only /data uid and gid become1654; original root mode and archived whole-second mtime are explicitly restored.'
        TimestampComparison = 'Docker archive whole-second UTC mtime precision; fractions below one second are not represented. Compare exactly at that precision; file contents and application JSON timestamps remain unchanged.'
        ArchiveMtimePrecisionSeconds = 1
        FractionalMtimeLossMaximumExclusiveSeconds = 1
        Outcome = 'InProgress'
    }
    $phase = 'PrepareOwnedVolume'
    if (!$alreadyVolume) {
        if ($null -eq $volume) {
            $volume = Invoke-Engine POST '/volumes/create' @{ Name = $VolumeName; Driver = 'local'; Labels = @{ $ownerLabel = $volumeOwner; $schemaLabel = '1' } }
            Assert-Volume $volume
        }
        Assert-NoOtherVolumeWriter $ExpectedContainerId
        $emptyHelper = New-VolumeHelper $client.Image
        $empty = Read-DataArchive $emptyHelper
        if ($empty.FileCount -ne 0 -or $empty.DirectoryCount -ne 1) {
            Stop-DataOperation 'Migration refuses an existing nonempty volume.'
        }
    }
    $phase = 'RecheckBeforeStop'
    $freshClient = Get-Container $ExpectedContainerId
    [void](Assert-Client $freshClient $ExpectedContainerId -RequireHealthy)
    $fresh = Read-DataArchive $ExpectedContainerId
    if ((Get-ContainerHash $freshClient) -ne $clientHash -or $fresh.RunsSha256 -ne $preflight.RunsSha256 -or
        (Get-ContainerHash (Get-Container $publisher.Id)) -ne $publisherHash) {
        Stop-DataOperation 'A container, configuration or execution run changed since preflight.'
    }
    Assert-NoOtherVolumeWriter $ExpectedContainerId
    if (!$alreadyVolume) {
        Assert-NoOtherBindWriter $source $ExpectedContainerId
    }
    $phase = 'StopOriginal'
    [void](Invoke-Engine POST "/containers/$ExpectedContainerId/stop?t=40" -TimeoutSeconds 50)
    $stopped = Get-Container $ExpectedContainerId
    $report.OriginalStopped = !$stopped.State.Running
    $report.OriginalExitCode = $stopped.State.ExitCode
    $report.OriginalOomKilled = $stopped.State.OOMKilled
    if ($stopped.State.Running -or $stopped.State.ExitCode -ne 0 -or $stopped.State.OOMKilled) {
        Stop-DataOperation 'The original did not stop cleanly; forced or OOM termination prevents cutover.'
    }
    $phase = 'ArchiveQuiescedData'
    $sourceSnapshot = Read-DataArchive $ExpectedContainerId
    Assert-DataPresent $sourceSnapshot
    Assert-LinuxDataPermissions $sourceSnapshot
    if ($sourceSnapshot.RunsSha256 -ne $preflight.RunsSha256) {
        Stop-DataOperation 'Execution state changed during the stop admission window.'
    }
    Save-DataSnapshot $sourceSnapshot 'source'
    $report.SourceArchiveSha256 = $sourceSnapshot.ArchiveSha256
    $report.SourceManifestSha256 = $sourceSnapshot.ManifestSha256
    $report.FileCount = $sourceSnapshot.FileCount
    $report.DirectoryCount = $sourceSnapshot.DirectoryCount
    $report.DataBytes = $sourceSnapshot.DataBytes
    $report.RunCount = $sourceSnapshot.RunCount
    $report.SourceRootMode = [Convert]::ToString([int]$sourceSnapshot.Root.Mode, 8)
    $report.SourceRootArchiveMtimeUtc = $sourceSnapshot.Root.ModifiedUtc
    if (($sourceSnapshot.Root.Mode -band 448) -ne 448) {
        Stop-DataOperation 'The preserved data root mode does not grant its owner read/write/traverse access.'
    }
    $expectedEntries = Copy-Value $sourceSnapshot.Entries
    if (!$alreadyVolume) {
        $phase = 'RestoreAndVerifyVolume'
        [byte[]]$restoreArchive = New-RestoreArchive $sourceSnapshot.Bytes
        [IO.File]::WriteAllBytes((Join-Path $EvidenceDirectory 'restore-payload.tar'), $restoreArchive)
        $report.RestorePayloadSha256 = Get-BytesHash $restoreArchive
        $restoreHelper = New-VolumeHelper $client.Image -Restore -RestoreArchive $restoreArchive
        $restored = Read-DataArchive $restoreHelper
        $expectedRoot = @($expectedEntries | Where-Object Path -EQ '')[0]
        $expectedRoot.Uid = 1654
        $expectedRoot.Gid = 1654
        Save-DataSnapshot $restored 'restored'
        $report.RestoredArchiveSha256 = $restored.ArchiveSha256
        $report.RestoredManifestSha256 = $restored.ManifestSha256
        if ($restored.ManifestSha256 -ne (Get-ValueHash $expectedEntries)) {
            Stop-DataOperation 'Restored file/directory metadata, timestamps, ownership or content differs from the archive.'
        }
    }
    $phase = 'VerifyBeforeCreate'
    $unchanged = Read-DataArchive $ExpectedContainerId
    if ($unchanged.ManifestSha256 -ne $sourceSnapshot.ManifestSha256 -or (Get-Container $ExpectedContainerId).State.Running -or
        (Get-ContainerHash (Get-Container $publisher.Id)) -ne $publisherHash) {
        Stop-DataOperation 'Source data, original running state or publisher changed before cutover.'
    }
    Assert-NoOtherVolumeWriter $ExpectedContainerId
    $phase = 'RetainOriginal'
    [void](Invoke-Engine POST "/containers/$ExpectedContainerId/rename?name=$backupName")
    $report.OriginalRenamed = $true
    $phase = 'CreateCandidate'
    $referencedImage = Invoke-Engine GET "/images/$([Uri]::EscapeDataString($client.Config.Image))/json"
    if ($referencedImage.Id -ne $client.Image) {
        Stop-DataOperation 'The configured image reference changed before candidate creation.'
    }
    Assert-ReferencedNetworks $client
    $created = Invoke-Engine POST "/containers/create?name=$ContainerName" $createBody
    if ($created.Id -notmatch '^[a-f0-9]{64}$' -or $created.Id -in @($ExpectedContainerId, $publisher.Id)) {
        Stop-DataOperation 'The replacement did not return an exact new container identity.'
    }
    $candidateId = [string]$created.Id
    $report.CandidateContainerId = $candidateId
    $candidate = Get-Container $candidateId
    Assert-CandidatePreserved $candidate $client $createBody -BeforeStart
    $beforeStart = Read-DataArchive $candidateId
    Save-DataSnapshot $beforeStart 'candidate-before-start'
    if ($beforeStart.ManifestSha256 -ne (Get-ValueHash $expectedEntries)) {
        Stop-DataOperation 'The candidate data does not exactly match the verified snapshot before start.'
    }
    $report.BeforeStartManifestSha256 = $beforeStart.ManifestSha256
    $report.ArchiveRestorationVerified = !$alreadyVolume
    $report.ConfigurationPreserved = $true
    $report.CandidateHostConfigComparisonSha256 = Get-ValueHash (Get-HostConfigComparisonValue $candidate.HostConfig)
    $phase = 'StartAndHealth'
    $report.CandidateStartAttempted = $true
    [void](Invoke-Engine POST "/containers/$candidateId/start")
    $candidate = Wait-Healthy $candidateId
    Assert-CandidatePreserved $candidate $client $createBody
    if ((Get-Container $ExpectedContainerId).State.Running -or (Get-ContainerHash (Get-Container $publisher.Id)) -ne $publisherHash) {
        Stop-DataOperation 'The original resumed or publisher changed during cutover.'
    }
    $report.PublisherUnchanged = $true
    $report.CandidateStartedUtc = $candidate.State.StartedAt
    $report.Outcome = 'HealthyCandidateOriginalAndDataRetained'
    $report.RequiredFollowup = 'Verify real UI history/tools and exact HTTP dispatch timings; use Recreate for a separate persistence replacement test.'
} catch {
    $safeCause = 'An input, archive parser, filesystem or Docker transport check failed; private exception details omitted.'
    $failure = $_.Exception
    for ($depth = 0; $null -ne $failure -and $depth -lt 8; $depth++) {
        if ($failure.Data.Contains('ManualClientDataSafeMessage')) {
            $safeCause = $failure.Message
            break
        }
        $failure = $failure.InnerException
    }
    if ($null -ne $report) {
        $report.SafeFailure = $safeCause
        $report.Outcome = 'FailedNeedsExplicitRecovery'
        $report.FailedPhase = $phase
        $report.AutomaticRollback = $false
        $report.Recovery = if ($report.CandidateStartAttempted) {
            'The candidate may have written data. Preserve both containers, volume and archives. Verify exact identities, stop any running candidate, and review post-cutover writes before choosing a data source. Never start both containers against this volume or silently restore the old bind.'
        } else {
            'No candidate start was requested. Verify exact container identities and that all candidates are stopped. Retain the volume and archives; explicitly rename any stopped candidate aside, restore the retained original name if needed, and start only the original after reviewing the failed phase.'
        }
    }
    throw "Manual client data operation failed at phase ${phase}: $safeCause No automatic rollback or data deletion was attempted."
} finally {
    $retainedHelpers = @()
    foreach ($identity in $helpers) {
        try {
            $helper = Get-Container $identity
            if ($helper.Config.Labels[$helperLabel] -ne $operationId -or $helper.Id -eq $ExpectedContainerId -or $helper.Id -eq $candidateId -or $helper.State.Running) {
                $retainedHelpers += $identity
                continue
            }
            [void](Invoke-Engine DELETE "/containers/${identity}?force=false&v=false")
        } catch {
            $retainedHelpers += $identity
        }
    }
    if ($null -ne $report -and $evidenceCreated) {
        $report.RetainedHelperIds = $retainedHelpers
        $report.CompletedUtc = [DateTimeOffset]::UtcNow.ToString('O')
        $report | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $EvidenceDirectory 'operation-report.json') -Encoding utf8NoBOM
        [pscustomobject]$report
    }
}
