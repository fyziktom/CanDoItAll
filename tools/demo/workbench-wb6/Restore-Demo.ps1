#requires -Version 7.4
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$CandidatePath,
    [Parameter(Mandatory)][string]$SnapshotPath,
    [Parameter(Mandatory)][string]$TargetDirectory,
    [Parameter(Mandatory)][ValidateRange(1024, 65535)][int]$Port,
    [Parameter(Mandatory)][ValidatePattern('^10\.(\d{1,3}\.){2}\d{1,3}/28$')][string]$FrontendSubnet,
    [Parameter(Mandatory)][ValidatePattern('^10\.(\d{1,3}\.){2}\d{1,3}/28$')][string]$BackendSubnet
)
. (Join-Path $PSScriptRoot 'Demo.Common.ps1')
$candidate = Read-DemoCandidate $CandidatePath
$snapshotRoot = [IO.Path]::GetFullPath($SnapshotPath)
$targetRoot = [IO.Path]::GetFullPath($TargetDirectory)
$deliveryRoot = [IO.Path]::GetDirectoryName([IO.Path]::GetFullPath($CandidatePath)) + [IO.Path]::DirectorySeparatorChar
if (-not $targetRoot.StartsWith($deliveryRoot, [StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $targetRoot)) {
    throw 'Restore requires a new child directory of this delivery directory. Existing targets are never overwritten.'
}
if (@(Get-NetTCPConnection -State Listen -LocalPort $Port -ErrorAction SilentlyContinue).Count -gt 0 -or $Port -eq $candidate.port) {
    throw 'Restore requires a separate unused loopback port.'
}
$snapshot = Get-Content -LiteralPath (Join-Path $snapshotRoot 'snapshot.json') -Raw | ConvertFrom-Json
if ($snapshot.task -cne $candidate.task -or $snapshot.sourceProject -cne $candidate.project -or $snapshot.imageId -cne $candidate.imageId -or $snapshot.databaseImageId -cne $candidate.databaseImageId) {
    throw 'Snapshot identity does not match this candidate.'
}
foreach ($file in $snapshot.files) {
    if ($file.file -cnotin @('database.dump', 'app-data.tar.gz', 'key-data.tar.gz') -or (Get-FileHash -LiteralPath (Join-Path $snapshotRoot $file.file)).Hash -cne $file.sha256) {
        throw 'Snapshot content or hash is invalid.'
    }
}
if (@($snapshot.files).Count -ne 3 -or @($snapshot.files.file | Sort-Object -Unique).Count -ne 3) {
    throw 'The snapshot must contain all three database, app and key archives.'
}
function Get-SubnetRange([string]$Subnet) {
    $parts = $Subnet.Split('/')
    $bytes = [Net.IPAddress]::Parse($parts[0]).GetAddressBytes()
    if ($bytes.Length -ne 4) {
        throw 'Expected an IPv4 subnet.'
    }
    $value = [uint64]0
    foreach ($part in $bytes) {
        $value = $value * 256 + $part
    }
    $size = [uint64][Math]::Pow(2, 32 - [int]$parts[1])
    $start = [uint64]([Math]::Floor($value / $size) * $size)
    return @{ start = $start; end = $start + $size - 1 }
}
$requested = @((Get-SubnetRange $FrontendSubnet), (Get-SubnetRange $BackendSubnet))
foreach ($subnet in @($FrontendSubnet, $BackendSubnet)) {
    if ([Net.IPAddress]::Parse($subnet.Split('/')[0]).GetAddressBytes()[3] % 16 -ne 0) {
        throw 'Each restore subnet must use its canonical /28 network address.'
    }
}
if ($requested[0].start -le $requested[1].end -and $requested[1].start -le $requested[0].end) {
    throw 'The two restore networks overlap.'
}
foreach ($networkId in @(Invoke-DemoDocker @('network', 'ls', '--format', '{{.ID}}'))) {
    $network = (Invoke-DemoDocker @('network', 'inspect', $networkId) | ConvertFrom-Json)[0]
    foreach ($allocation in @($network.IPAM.Config)) {
        if ($null -ne $allocation -and $allocation.PSObject.Properties.Name -contains 'Subnet' -and $allocation.Subnet -match '^\d+\.') {
            $used = Get-SubnetRange $allocation.Subnet
            foreach ($range in $requested) {
                if ($range.start -le $used.end -and $used.start -le $range.end) {
                    throw 'A restore subnet overlaps an existing Docker network. No existing network was changed.'
                }
            }
        }
    }
}
$project = 'candoitall-wb6-recovery-' + [Guid]::NewGuid().ToString('N').Substring(0, 8)
New-Item -ItemType Directory -Path $targetRoot | Out-Null
$recovery = $candidate | ConvertTo-Json -Depth 12 | ConvertFrom-Json
$recovery.project = $project
$recovery.port = $Port
$frontendAddress = [Net.IPAddress]::Parse($FrontendSubnet.Split('/')[0]).GetAddressBytes()
$frontendAddress[3]++
$recovery.trustedIngressIp = ([Net.IPAddress]::new($frontendAddress)).ToString()
$recovery.environmentPath = Join-Path $targetRoot '.env'
$recovery.volumes.app = $project + '_app-data'
$recovery.volumes.keys = $project + '_key-data'
$recovery.volumes.database = $project + '_db-data'
$settings = @(Get-Content -LiteralPath $candidate.environmentPath | Where-Object { $_ -notmatch '^(COMPOSE_PROJECT_NAME|APP_PORT|APP_IMAGE)=' })
$settings += "COMPOSE_PROJECT_NAME=$project"
$settings += "APP_PORT=$Port"
$settings += "APP_IMAGE=$($candidate.imageId)"
[IO.File]::WriteAllLines($recovery.environmentPath, $settings)
Copy-Item -LiteralPath $candidate.composeFiles[0].path -Destination (Join-Path $targetRoot 'compose.frozen.yaml')
$override = Get-Content -LiteralPath $candidate.composeFiles[1].path -Raw
$subnets = [regex]::Matches($override, 'subnet: (10\.\d+\.\d+\.\d+/28)')
if ($subnets.Count -ne 2) {
    throw 'Expected the frozen candidate override with exactly two explicit networks.'
}
$override = $override.Replace($subnets[0].Groups[1].Value, $FrontendSubnet).Replace($subnets[1].Groups[1].Value, $BackendSubnet).Replace($candidate.volumes.keys, $recovery.volumes.keys)
$override = $override.Replace("WebHost__LocalOperatorUi__TrustedAddresses__0: $($candidate.trustedIngressIp)", "WebHost__LocalOperatorUi__TrustedAddresses__0: $($recovery.trustedIngressIp)")
[IO.File]::WriteAllText((Join-Path $targetRoot 'compose.override.yaml'), $override)
$recovery.composeFiles = @(foreach ($path in @((Join-Path $targetRoot 'compose.frozen.yaml'), (Join-Path $targetRoot 'compose.override.yaml'))) {
    [ordered]@{ path = $path; sha256 = (Get-FileHash -LiteralPath $path).Hash }
})
$recoveryPath = Join-Path $targetRoot 'candidate.json'
$recovery | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $recoveryPath
foreach ($entry in @(@{ volume = $recovery.volumes.app; kind = 'app-data' }, @{ volume = $recovery.volumes.keys; kind = 'key-data' }, @{ volume = $recovery.volumes.database; kind = 'db-data' })) {
    $existing = @(Invoke-DemoDocker @('volume', 'ls', '--format', '{{.Name}}') | Where-Object { $_ -ceq $entry.volume })
    if ($existing.Count -gt 0) {
        throw 'Restore volume already exists; no existing data was overwritten.'
    }
    Invoke-DemoDocker @('volume', 'create', '--label', "io.candoitall.task=$($candidate.task)", '--label', "com.docker.compose.project=$project", '--label', "com.docker.compose.volume=$($entry.kind)", $entry.volume) | Out-Null
    if ($entry.kind -cne 'db-data') {
        Invoke-DemoDocker @('run', '--rm', '--label', "io.candoitall.task=$($candidate.task)", '--network', 'none', '--read-only', '--mount', "type=volume,source=$($entry.volume),target=/output", '--mount', "type=bind,source=$snapshotRoot,target=/backup,readonly", '--entrypoint', 'tar', $snapshot.databaseImageId, '-xzf', "/backup/$($entry.kind).tar.gz", '-C', '/output', '.')
    }
}
Invoke-DemoCompose $recovery @('up', '--detach', '--no-build', '--pull', 'never', '--wait', '--wait-timeout', '120', 'db')
$db = @((Get-DemoContainers $recovery) | Where-Object { $_.Config.Labels.'com.docker.compose.service' -ceq 'db' })[0]
Invoke-DemoDocker @('cp', (Join-Path $snapshotRoot 'database.dump'), "$($db.Id):/tmp/wb6-restore.dump")
Invoke-DemoDocker @('exec', $db.Id, 'pg_restore', '--exit-on-error', '--no-owner', '--no-privileges', '--username', $recovery.databaseUser, '--dbname', $recovery.database, '/tmp/wb6-restore.dump')
& (Join-Path $PSScriptRoot 'Start-Demo.ps1') -CandidatePath $recoveryPath
Write-Output "Separate recovery target started: $recoveryPath. The original candidate volumes and port were preserved."
