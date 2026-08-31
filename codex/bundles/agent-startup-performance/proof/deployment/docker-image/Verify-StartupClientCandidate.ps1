[CmdletBinding()]
param([string]$RepositoryRoot)
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $directory = [IO.DirectoryInfo]::new($PSScriptRoot)
    while ($null -ne $directory -and !(Test-Path -LiteralPath (Join-Path $directory.FullName 'CanDoItAll.slnx'))) {
        $directory = $directory.Parent
    }
    if ($null -eq $directory) {
        throw 'Cannot locate the owning repository.'
    }
    $RepositoryRoot = $directory.FullName
}
$proofRoot = Join-Path $RepositoryRoot 'codex/bundles/agent-startup-performance/proof'
$build = Get-Content -LiteralPath (Join-Path $proofRoot 'deployment/docker-image/image-result.json') -Raw | ConvertFrom-Json -AsHashtable -DateKind String
$freeze = Get-Content -LiteralPath (Join-Path $proofRoot 'frozen-integration/source-freeze.json') -Raw | ConvertFrom-Json -AsHashtable -DateKind String
$baseline = Get-Content -LiteralPath (Join-Path $proofRoot 'phase-0/host-preflight.json') -Raw | ConvertFrom-Json -AsHashtable -DateKind String
$contract = Get-Content -LiteralPath (Join-Path $proofRoot 'deployment/docker-image/verification-contract.json') -Raw | ConvertFrom-Json -AsHashtable
$expectedIds = @{
    'candoitall-shared-providers-manual-client-a-1' = 'fb12806ab50b7bdadb68175ce79d6efb8596b3f4f62329f07f445ae49074226e'
    'candoitall-shared-providers-manual-central-1' = '000fadde7e6757f7afd413e3102fa58568e18da4d9a7361d8057bda40c9b966d'
}
function Get-EnvironmentHash {
    param($Configuration)
    $text = [string]::Join("`n", @($Configuration.Config.Env | Sort-Object))
    return [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData([Text.Encoding]::UTF8.GetBytes($text)))
}
function Read-DockerIdentity {
    param([ValidateSet('image', 'container')][string]$Kind, [string]$Identity)
    $result = @(& docker inspect --type $Kind $Identity 2>$null | ConvertFrom-Json -AsHashtable -DateKind String)
    if ($LASTEXITCODE -ne 0 -or $result.Count -ne 1) {
        throw 'The exact Docker identity could not be read.'
    }
    return $result[0]
}
$endpoint = (& docker context inspect --format '{{.Endpoints.docker.Host}}' 2>$null).Trim()
if ($LASTEXITCODE -ne 0 -or $endpoint -ne 'npipe:////./pipe/dockerDesktopLinuxEngine') {
    throw 'Unexpected Docker endpoint.'
}
$head = (& git -C $RepositoryRoot rev-parse HEAD 2>$null).Trim()
if ($LASTEXITCODE -ne 0 -or $head -ne $freeze.head -or $build.SourceRevision -ne $freeze.head) {
    throw 'Frozen repository/image revision mismatch.'
}
foreach ($row in $freeze.sourceFiles) {
    if ((Get-FileHash -LiteralPath (Join-Path $RepositoryRoot $row.path) -Algorithm SHA256).Hash -ne $row.sha256) {
        throw 'A frozen source/test input changed.'
    }
}
$restartPath = Join-Path $RepositoryRoot '.artifacts/agent-startup-performance/deployment/Restart-StartupClient.ps1'
if ((Get-FileHash -LiteralPath $restartPath -Algorithm SHA256).Hash -ne $contract.restartScriptSha256) {
    throw 'The reviewed restart draft changed; obtain a new review.'
}
$stylePath = Join-Path ([IO.Path]::GetDirectoryName($RepositoryRoot)) $contract.sourceCssPath
if ((Get-FileHash -LiteralPath $stylePath -Algorithm SHA256).Hash -ne $contract.sourceCssSha256) {
    throw 'The file-browser source stylesheet changed after the image build.'
}
$image = Read-DockerIdentity image $build.ImageId
if ($image.Id -ne $build.ImageId -or $image.Config.Labels['io.candoitall.source-fingerprint'] -ne $build.SourceFingerprint -or
    $image.Config.Labels['org.opencontainers.image.revision'] -ne $freeze.head -or $image.Config.User -ne '1654') {
    throw 'Immutable candidate image identity/provenance mismatch.'
}
$containers = @()
foreach ($expected in $baseline.Containers) {
    if (!$expectedIds.ContainsKey($expected.Name) -or $expected.Id -ne $expectedIds[$expected.Name]) {
        throw 'The baseline contains an unreviewed target identity.'
    }
    $current = Read-DockerIdentity container $expected.Id
    $hostConfig = $current.HostConfig
    if ($current.Id -ne $expected.Id -or $current.Image -ne $expected.Image -or $current.Name -ne '/' + $expected.Name -or
        $current.State.StartedAt -ne $expected.StartedAtUtc -or !$current.State.Running -or $current.State.Health.Status -ne 'healthy' -or
        $current.Config.User -ne '1654:1654' -or !$hostConfig.ReadonlyRootfs -or $hostConfig.Privileged -or $hostConfig.AutoRemove -or
        $hostConfig.Memory -ne $expected.Memory -or $hostConfig.NanoCpus -ne $expected.NanoCpus -or $hostConfig.PidsLimit -ne $expected.PidsLimit -or
        @($hostConfig.CapAdd | Where-Object { $null -ne $_ }).Count -ne 0 -or @($hostConfig.CapDrop) -notcontains 'ALL' -or
        @($hostConfig.SecurityOpt) -notcontains 'no-new-privileges:true' -or (Get-EnvironmentHash $current) -ne $expected.EnvironmentSha256) {
        throw 'A baseline container identity/environment/security invariant changed.'
    }
    $expectedPort = if ($expected.Id -eq $expectedIds['candoitall-shared-providers-manual-client-a-1']) { '5214' } else { '5210' }
    $ports = @($hostConfig.PortBindings['8080/tcp'])
    if ($hostConfig.PortBindings.Count -ne 1 -or $ports.Count -ne 1 -or $ports[0].HostIp -ne '127.0.0.1' -or $ports[0].HostPort -ne $expectedPort) {
        throw 'A baseline loopback publication changed.'
    }
    if (@($current.Mounts).Count -ne @($expected.Mounts).Count -or $current.NetworkSettings.Networks.Count -ne @($expected.Networks).Count) {
        throw 'Baseline mount/network inventory changed.'
    }
    foreach ($mount in $expected.Mounts) {
        $actual = @($current.Mounts | Where-Object Destination -EQ $mount.Destination)
        if ($actual.Count -ne 1 -or $actual[0].Type -ne 'bind' -or $actual[0].Source -ne $mount.Source -or $actual[0].RW -ne $mount.RW) {
            throw 'A baseline persistent mount changed.'
        }
    }
    foreach ($network in $expected.Networks) {
        if ($current.NetworkSettings.Networks[$network.Name].NetworkID -ne $network.NetworkId -or
            (ConvertTo-Json -InputObject $current.NetworkSettings.Networks[$network.Name].Aliases -Compress) -ne
            (ConvertTo-Json -InputObject $network.Aliases -Compress)) {
            throw 'A baseline network identity changed.'
        }
    }
    $containers += [ordered]@{
        Name = $expected.Name
        Id = $current.Id
        ImageId = $current.Image
        StartedAtUtc = $current.State.StartedAt
        EnvironmentSha256 = Get-EnvironmentHash $current
        BaselineMetadataMatches = $true
    }
}
[ordered]@{
    VerifiedUtc = [DateTimeOffset]::UtcNow.ToString('O')
    ImageId = $image.Id
    SourceFingerprint = $build.SourceFingerprint
    FrozenSourceCount = @($freeze.sourceFiles).Count
    SourceFreezeMatches = $true
    SourceCssMatches = $true
    PublishedImageCssVerified = $false
    PublishedImageCssGate = 'Pending actual5214 static-file and UI checks after authorized replacement.'
    RestartScriptExecuted = $false
    HostMutation = $false
    VerificationScope = 'Pre-replacement metadata and frozen inputs; restart script separately rechecks idleness, pending journals and complete configuration immediately before any stop.'
    Containers = $containers
} | ConvertTo-Json -Depth 7
