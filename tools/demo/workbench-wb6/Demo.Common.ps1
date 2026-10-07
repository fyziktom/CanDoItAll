#requires -Version 7.4
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false

function Invoke-DemoDocker {
    param([Parameter(Mandatory)][string[]]$Arguments)
    $result = & docker @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "Docker operation '$($Arguments[0])' failed with exit code $LASTEXITCODE. Resources have been preserved."
    }
    return $result
}

function Read-DemoCandidate {
    param([Parameter(Mandatory)][string]$Path)
    $candidate = Get-Content -LiteralPath ([IO.Path]::GetFullPath($Path)) -Raw | ConvertFrom-Json
    if ($candidate.task -cne 'workbench-completion-wb6' -or $candidate.project -notmatch '^candoitall-wb6-(candidate|recovery)-[0-9a-f]{8}$') {
        throw 'This is not an owned WB6 candidate manifest.'
    }
    if ($candidate.imageId -notmatch '^sha256:[0-9a-f]{64}$' -or $candidate.databaseImageId -notmatch '^sha256:[0-9a-f]{64}$' -or $candidate.port -lt 1024 -or $candidate.port -gt 65535) {
        throw 'Invalid immutable image or loopback port in manifest.'
    }
    foreach ($file in $candidate.composeFiles) {
        if ((Get-FileHash -LiteralPath $file.path -Algorithm SHA256).Hash -cne $file.sha256) {
            throw "Frozen Compose contract changed: $($file.path)"
        }
    }
    if (-not (Test-Path -LiteralPath $candidate.environmentPath -PathType Leaf)) {
        throw 'The private environment file is missing. Restore its original private configuration; do not create replacement credentials.'
    }
    foreach ($imageId in @($candidate.imageId, $candidate.databaseImageId)) {
        $image = (Invoke-DemoDocker @('image', 'inspect', $imageId) | ConvertFrom-Json)[0]
        if ($image.Id -cne $imageId) {
            throw 'An exact published application or database image is not installed.'
        }
    }
    return $candidate
}

function Get-DemoContainers {
    param([Parameter(Mandatory)]$Candidate)
    $ids = @(Invoke-DemoDocker @('ps', '--all', '--filter', "label=com.docker.compose.project=$($Candidate.project)", '--format', '{{.ID}}'))
    foreach ($id in $ids) {
        $container = (Invoke-DemoDocker @('inspect', $id) | ConvertFrom-Json)[0]
        $service = $container.Config.Labels.'com.docker.compose.service'
        if ($container.Config.Labels.'io.candoitall.task' -cne $Candidate.task -or $service -notin @('app', 'db')) {
            throw 'A resource in this Compose project does not have the expected owner and service. No resources were changed.'
        }
        foreach ($mount in @($container.Mounts | Where-Object Type -eq 'volume')) {
            if ($mount.Name -cnotin @($Candidate.volumes.app, $Candidate.volumes.keys, $Candidate.volumes.database)) {
                throw 'A container uses a volume outside the candidate manifest. No resources were changed.'
            }
        }
        $container
    }
}

function Invoke-DemoCompose {
    param([Parameter(Mandatory)]$Candidate, [Parameter(Mandatory)][string[]]$Arguments)
    $environmentBefore = @{}
    try {
        foreach ($line in Get-Content -LiteralPath $Candidate.environmentPath) {
            if ($line -match '^([A-Z][A-Z0-9_]*)=(.*)$') {
                $name = $Matches[1]
                $environmentBefore[$name] = [Environment]::GetEnvironmentVariable($name, 'Process')
                [Environment]::SetEnvironmentVariable($name, $Matches[2], 'Process')
            }
        }
        if (-not $environmentBefore.ContainsKey('APP_IMAGE')) {
            $environmentBefore['APP_IMAGE'] = [Environment]::GetEnvironmentVariable('APP_IMAGE', 'Process')
        }
        [Environment]::SetEnvironmentVariable('APP_IMAGE', $Candidate.imageId, 'Process')
        $composeArguments = @('compose', '--ansi', 'never', '--env-file', $Candidate.environmentPath, '--project-name', $Candidate.project)
        foreach ($file in $Candidate.composeFiles) {
            $composeArguments += @('--file', $file.path)
        }
        $configuration = Invoke-DemoDocker ($composeArguments + @('config', '--format', 'json')) | ConvertFrom-Json
        if ($configuration.services.app.image -cne $Candidate.imageId -or $configuration.services.db.image -cne $Candidate.databaseImageId -or $configuration.services.app.environment.CANDOITALL_HOST_BINDING_ID -cne $Candidate.hostBindingId) {
            throw 'Resolved startup identity differs from the frozen candidate.'
        }
        $port = @($configuration.services.app.ports)
        if ($port.Count -ne 1 -or $port[0].host_ip -cne '127.0.0.1' -or [int]$port[0].published -ne $Candidate.port) {
            throw 'The resolved port must be the exact candidate loopback endpoint.'
        }
        if ($Candidate.PSObject.Properties.Name -contains 'trustedIngressIp' -and
            $configuration.services.app.environment.WebHost__LocalOperatorUi__TrustedAddresses__0 -cne $Candidate.trustedIngressIp) {
            throw 'The local operator ingress differs from the inspected candidate address.'
        }
        Invoke-DemoDocker ($composeArguments + $Arguments)
    } finally {
        foreach ($entry in $environmentBefore.GetEnumerator()) {
            [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value, 'Process')
        }
    }
}

function Write-DemoCheck {
    param([Parameter(Mandatory)]$Candidate)
    $containers = @(Get-DemoContainers $Candidate)
    foreach ($service in @('app', 'db')) {
        $matching = @($containers | Where-Object { $_.Config.Labels.'com.docker.compose.service' -ceq $service })
        if ($matching.Count -ne 1 -or $matching[0].State.Status -cne 'running' -or $matching[0].State.Health.Status -cne 'healthy') {
            throw "The owned $service container is not healthy. Inspect its exact container log; data has been preserved."
        }
        if ($service -ceq 'app' -and $matching[0].Image -cne $Candidate.imageId) {
            throw 'The running application is not the frozen candidate image.'
        }
        if ($service -ceq 'app' -and $Candidate.PSObject.Properties.Name -contains 'trustedIngressIp') {
            $expectedTrust = "WebHost__LocalOperatorUi__TrustedAddresses__0=$($Candidate.trustedIngressIp)"
            $trust = @($matching[0].Config.Env | Where-Object { $_ -like 'WebHost__LocalOperatorUi__TrustedAddresses__*' })
            if ($trust.Count -ne 1 -or $trust[0] -cne $expectedTrust) {
                throw 'The running application does not use the exact local operator ingress.'
            }
            $bindings = @($matching[0].NetworkSettings.Ports.'8080/tcp')
            if ($bindings.Count -ne 1 -or $bindings[0].HostIp -cne '127.0.0.1' -or [int]$bindings[0].HostPort -ne $Candidate.port) {
                throw 'The running local operator application must bind only to its manifest loopback port.'
            }
        }
        if ($service -ceq 'db' -and $matching[0].Image -cne $Candidate.databaseImageId) {
            throw 'The running database is not the frozen database image.'
        }
    }
    $response = Invoke-WebRequest -Uri "http://127.0.0.1:$($Candidate.port)/health" -TimeoutSec 20
    if ($response.StatusCode -ne 200) {
        throw 'Application HTTP health failed.'
    }
    [ordered]@{
        checkedUtc = [DateTimeOffset]::UtcNow.ToString('O')
        sourceCommit = $Candidate.sourceCommit
        imageId = $Candidate.imageId
        url = "http://127.0.0.1:$($Candidate.port)"
        httpStatus = $response.StatusCode
        containers = @($containers | ForEach-Object {
            [ordered]@{ id = $_.Id; service = $_.Config.Labels.'com.docker.compose.service'; image = $_.Image; health = $_.State.Health.Status }
        })
    } | ConvertTo-Json -Depth 5
}
