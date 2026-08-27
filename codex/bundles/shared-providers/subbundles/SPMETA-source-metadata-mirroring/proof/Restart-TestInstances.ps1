param([Parameter(Mandatory)] [string] $Image, [Parameter(Mandatory)] [string] $BackupSuffix)
$ErrorActionPreference = 'Stop'
$names = @('candoitall-spui-shared', 'candoitall-spui-client')
$replaced = [System.Collections.Generic.List[string]]::new()
$renamed = [System.Collections.Generic.List[string]]::new()
docker image inspect $Image --format '{{.Id}}'
if ($LASTEXITCODE -ne 0) {
    throw 'The requested test image is unavailable.'
}
try {
    foreach ($name in $names) {
        $configuration = (docker inspect $name | ConvertFrom-Json)[0]
        if ($LASTEXITCODE -ne 0 -or $configuration.Config.User -ne '1654:1654') {
            throw "Unexpected test-container configuration: $name"
        }
        $networks = @($configuration.NetworkSettings.Networks.PSObject.Properties.Name)
        if ($networks -notcontains 'candoitall-spui-app') {
            throw "Expected isolated application network is missing: $name"
        }
        docker stop $name | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "Cannot stop test container: $name"
        }
        docker rename $name "$name-$BackupSuffix"
        if ($LASTEXITCODE -ne 0) {
            docker start $name | Out-Host
            throw "Cannot retain rollback container: $name"
        }
        $renamed.Add($name)
        $arguments = @('create', '--name', $name, '--network', 'candoitall-spui-app', '--user', $configuration.Config.User)
        foreach ($binding in $configuration.HostConfig.PortBindings.PSObject.Properties) {
            foreach ($port in $binding.Value) {
                $arguments += @('--publish', "$($port.HostIp):$($port.HostPort):$($binding.Name)")
            }
        }
        foreach ($mount in $configuration.Mounts) {
            if ($mount.Type -ne 'volume' -or -not $mount.Name.StartsWith('candoitall-spui-')) {
                throw 'Only the existing isolated test volumes may be mounted.'
            }
            $volume = "$($mount.Name):$($mount.Destination)"
            if (-not $mount.RW) {
                $volume += ':ro'
            }
            $arguments += @('--volume', $volume)
        }
        foreach ($setting in $configuration.Config.Env) {
            $arguments += @('--env', $setting)
        }
        $arguments += $Image
        & docker @arguments | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "Cannot create replacement test container: $name"
        }
        $replaced.Add($name)
        foreach ($network in $networks | Where-Object { $_ -ne 'candoitall-spui-app' }) {
            docker network connect $network $name
            if ($LASTEXITCODE -ne 0) {
                throw "Cannot reconnect test network: $network"
            }
        }
        docker start $name | Out-Host
        if ($LASTEXITCODE -ne 0) {
            throw "Cannot start replacement test container: $name"
        }
        Write-Output "$name started; rollback retained as $name-$BackupSuffix; named data volumes preserved."
    }
} catch {
    foreach ($name in $replaced) {
        docker rm --force $name | Out-Host
    }
    foreach ($name in $renamed) {
        docker rename "$name-$BackupSuffix" $name
        docker start $name | Out-Host
    }
    throw
}
