param(
    [Parameter(Mandatory = $true)]
    [string] $PublishDirectory,

    [Parameter(Mandatory = $true)]
    [string] $OutputDirectory
)

$ErrorActionPreference = 'Stop'
$runtimeImage = 'mcr.microsoft.com/dotnet/aspnet:10.0-noble'
$databaseImage = 'postgres:16'
$runId = [DateTimeOffset]::UtcNow.ToString('yyyyMMddHHmmss')
$resourcePrefix = "candoitall-a06-$runId"
$networkName = "$resourcePrefix-network"
$installVolume = "$resourcePrefix-install"
$dataVolume = "$resourcePrefix-data"
$certificateVolume = "$resourcePrefix-certificate"
$databaseVolume = "$resourcePrefix-postgres"
$databaseContainer = "$resourcePrefix-postgres"
$databaseUser = 'candoitall_a06'
$databaseName = 'candoitall_a06'
$databasePassword = [Guid]::NewGuid().ToString('N')
$certificatePassword = [Guid]::NewGuid().ToString('N')
$hostBindingId = "a06-linux-host-$runId"
$certificateDirectory = Join-Path ([IO.Path]::GetTempPath()) "CanDoItAll-A06-cert-$runId"
$certificatePath = Join-Path $certificateDirectory 'dataprotection.pfx'
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$toolDirectory = Join-Path $repositoryRoot 'tools\install\unix'
$publishRoot = (Resolve-Path $PublishDirectory).Path
$outputRoot = [IO.Path]::GetFullPath($OutputDirectory)
$appContainers = [System.Collections.Generic.List[string]]::new()
$createdVolumes = [System.Collections.Generic.List[string]]::new()
$createdNetwork = $false
$commands = [System.Collections.Generic.List[object]]::new()
$launches = [System.Collections.Generic.List[object]]::new()

function Invoke-Docker {
    param(
        [Parameter(Mandatory = $true)]
        [string[]] $Arguments,

        [Parameter(Mandatory = $true)]
        [string] $Evidence,

        [switch] $Sensitive
    )

    $output = @(& docker @Arguments 2>&1)
    $exitCode = $LASTEXITCODE
    $commands.Add([ordered]@{
        evidence = $Evidence
        exitCode = $exitCode
        sensitiveArgumentsRedacted = [bool]$Sensitive
    })

    if ($exitCode -ne 0) {
        throw "Docker command failed ($Evidence): $($output -join [Environment]::NewLine)"
    }

    return $output
}

function Get-FreeTcpPort {
    $listener = [Net.Sockets.TcpListener]::new([Net.IPAddress]::Loopback, 0)
    $listener.Start()
    try {
        return ([Net.IPEndPoint]$listener.LocalEndpoint).Port
    }
    finally {
        $listener.Stop()
    }
}

function Wait-DatabaseReady {
    for ($attempt = 1; $attempt -le 60; $attempt++) {
        & docker exec $databaseContainer pg_isready -U $databaseUser -d $databaseName *> $null
        if ($LASTEXITCODE -eq 0) {
            $commands.Add([ordered]@{
                evidence = "postgres readiness probe succeeded on attempt $attempt"
                exitCode = 0
                sensitiveArgumentsRedacted = $false
            })
            return
        }

        Start-Sleep -Milliseconds 500
    }

    throw 'PostgreSQL did not become ready.'
}

function Wait-ApplicationReady {
    param(
        [Parameter(Mandatory = $true)]
        [int] $Port,

        [Parameter(Mandatory = $true)]
        [string] $ContainerName
    )

    $healthUri = "http://127.0.0.1:$Port/health"
    for ($attempt = 1; $attempt -le 120; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri $healthUri -UseBasicParsing -TimeoutSec 2
            if ($response.StatusCode -eq 200) {
                $commands.Add([ordered]@{
                    evidence = "GET /health returned 200 on attempt $attempt"
                    exitCode = 0
                    sensitiveArgumentsRedacted = $false
                })
                return
            }
        }
        catch {
            $state = (& docker inspect $ContainerName --format '{{.State.Running}}' 2>$null)
            if ($state -eq 'false') {
                $containerLog = @(& docker logs $ContainerName 2>&1) -join [Environment]::NewLine
                throw "Application container exited before readiness: $containerLog"
            }
        }

        Start-Sleep -Milliseconds 500
    }

    throw "Application did not become ready at $healthUri."
}

function Assert-OperationsSnapshot {
    param(
        [Parameter(Mandatory = $true)]
        [object] $Snapshot
    )

    if ($Snapshot.state -ne 'Ready' -or !$Snapshot.databaseAndMigrationsReady) {
        throw 'Operations snapshot is not ready.'
    }

    if ($Snapshot.hostCapabilities.profile -ne 'LinuxHeadless' -or !$Snapshot.hostCapabilities.isReady) {
        throw 'Linux headless capability snapshot is not ready.'
    }

    $purposeRoots = @($Snapshot.hostCapabilities.purposeRoots)
    if ($purposeRoots.Count -ne 7) {
        throw "Expected seven purpose-root facts, found $($purposeRoots.Count)."
    }

    if (@($purposeRoots | Where-Object state -ne 'Ready').Count -ne 0) {
        throw 'One or more purpose-root facts are not ready.'
    }

    $expectedPurposes = @('Workspace', 'ControlPlane', 'DatabaseProfiles', 'DataProtectionKeys', 'State', 'Logs', 'RuntimeTemporary')
    $actualPurposes = @($purposeRoots.purpose | Sort-Object)
    if ((Compare-Object ($expectedPurposes | Sort-Object) $actualPurposes).Count -ne 0) {
        throw 'Purpose-root inventory is incomplete or contains duplicates.'
    }

    $serialized = $Snapshot | ConvertTo-Json -Depth 30 -Compress
    foreach ($forbidden in @('/srv/', '/install/', 'Password=', $databasePassword, $certificatePassword, $hostBindingId)) {
        if ($serialized.Contains($forbidden, [StringComparison]::Ordinal)) {
            throw "Operations snapshot disclosed forbidden deployment detail '$forbidden'."
        }
    }
}

function Install-Release {
    param(
        [Parameter(Mandatory = $true)]
        [string] $ReleaseId
    )

    Invoke-Docker -Arguments @(
        'run', '--rm',
        '--user', '10001:10001',
        '--mount', "type=volume,source=$installVolume,target=/install",
        '--mount', "type=bind,source=$publishRoot,target=/artifact,readonly",
        '--mount', "type=bind,source=$toolDirectory,target=/tools,readonly",
        '--entrypoint', '/bin/sh',
        $runtimeImage,
        '/tools/install-candoitall-web.sh',
        '--artifact', '/artifact',
        '--install-root', '/install',
        '--release-id', $ReleaseId
    ) -Evidence "install immutable release $ReleaseId as uid 10001 gid 10001" | Out-Null
}

function Invoke-ApplicationLaunch {
    param(
        [Parameter(Mandatory = $true)]
        [string] $Phase,

        [Parameter(Mandatory = $true)]
        [string] $ExpectedRelease
    )

    $containerName = "$resourcePrefix-app-$Phase"
    $appContainers.Add($containerName)
    $port = Get-FreeTcpPort
    $connectionString = "Host=$databaseContainer;Port=5432;Database=$databaseName;Username=$databaseUser;Password=$databasePassword"

    Invoke-Docker -Arguments @(
        'run', '--detach',
        '--name', $containerName,
        '--network', $networkName,
        '--user', '10001:10001',
        '--publish', "127.0.0.1:$Port`:8080",
        '--mount', "type=volume,source=$installVolume,target=/install",
        '--mount', "type=volume,source=$dataVolume,target=/srv",
        '--mount', "type=volume,source=$certificateVolume,target=/cert,readonly",
        '--env', 'ASPNETCORE_ENVIRONMENT=Production',
        '--env', 'ASPNETCORE_URLS=http://0.0.0.0:8080',
        '--env', 'RuntimeHost__Profile=LinuxHeadless',
        '--env', 'SecretVault__UsageProfile=Headless',
        '--env', 'SecretVault__Provider=Auto',
        '--env', "CANDOITALL_HOST_BINDING_ID=$hostBindingId",
        '--env', 'XDG_DATA_HOME=/srv/data',
        '--env', 'XDG_CONFIG_HOME=/srv/config',
        '--env', 'XDG_STATE_HOME=/srv/state',
        '--env', 'XDG_RUNTIME_DIR=/srv/run',
        '--env', 'Storage__WorkspaceRoot=/srv/workspace',
        '--env', 'ControlPlane__RootPath=/srv/control-plane',
        '--env', 'ControlPlane__DataProtectionKeysPath=/srv/dataprotection-keys',
        '--env', 'ControlPlane__StateRootPath=/srv/state',
        '--env', 'ControlPlane__LogsRootPath=/srv/logs',
        '--env', 'ControlPlane__RuntimeTemporaryRootPath=/srv/run',
        '--env', 'DataProtection__KeyProtection__Provider=Certificate',
        '--env', 'DataProtection__KeyProtection__CertificatePath=/cert/dataprotection.pfx',
        '--env', 'DataProtection__KeyProtection__CertificatePasswordEnvironmentVariable=CANDOITALL_DP_CERTIFICATE_PASSWORD',
        '--env', "CANDOITALL_DP_CERTIFICATE_PASSWORD=$certificatePassword",
        '--env', 'Database__Provider=PostgreSql',
        '--env', "Database__ConnectionString=$connectionString",
        '--env', 'FileTools__DesktopLaunch__Enabled=false',
        '--entrypoint', '/bin/sh',
        $runtimeImage,
        '/install/bin/run-candoitall-web.sh', '/install'
    ) -Evidence "launch $Phase on loopback as uid 10001 gid 10001 (secret environment values redacted)" -Sensitive | Out-Null

    Wait-ApplicationReady -Port $port -ContainerName $containerName
    $health = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/health" -TimeoutSec 10
    $operations = Invoke-RestMethod -Uri "http://127.0.0.1:$Port/api/runtime/operations" -TimeoutSec 30
    Assert-OperationsSnapshot -Snapshot $operations

    $activeRelease = (Invoke-Docker -Arguments @('exec', $containerName, '/bin/sh', '-c', 'cat /install/active-release') -Evidence "read active release during $Phase") -join ''
    if ($activeRelease.Trim() -ne $ExpectedRelease) {
        throw "Expected active release '$ExpectedRelease' during '$Phase', found '$activeRelease'."
    }

    $osRelease = (Invoke-Docker -Arguments @('exec', $containerName, '/bin/sh', '-c', 'cat /etc/os-release') -Evidence "capture /etc/os-release during $Phase") -join "`n"
    if (!$osRelease.Contains('Ubuntu 24.04', [StringComparison]::Ordinal)) {
        throw "Actual Linux image is not Ubuntu 24.04: $osRelease"
    }

    $architecture = ((Invoke-Docker -Arguments @('exec', $containerName, 'uname', '-m') -Evidence "capture architecture during $Phase") -join '').Trim()
    $runtimes = (Invoke-Docker -Arguments @('exec', $containerName, 'dotnet', '--list-runtimes') -Evidence "capture .NET runtimes during $Phase") -join "`n"
    $uid = ((Invoke-Docker -Arguments @('exec', $containerName, 'id', '-u') -Evidence "capture effective uid during $Phase") -join '').Trim()
    $gid = ((Invoke-Docker -Arguments @('exec', $containerName, 'id', '-g') -Evidence "capture effective gid during $Phase") -join '').Trim()
    if ($uid -ne '10001' -or $gid -ne '10001') {
        throw "Application ran with unexpected identity $uid`:$gid."
    }

    $startupLog = (Invoke-Docker -Arguments @('logs', $containerName) -Evidence "capture bounded startup log during $Phase") -join "`n"
    [IO.File]::WriteAllText((Join-Path $outputRoot "A06-linux-startup-$Phase.log"), $startupLog)
    [IO.File]::WriteAllText((Join-Path $outputRoot "A06-linux-health-$Phase.txt"), [string]$health)
    [IO.File]::WriteAllText(
        (Join-Path $outputRoot "A06-linux-operations-$Phase.json"),
        ($operations | ConvertTo-Json -Depth 30))

    Invoke-Docker -Arguments @('kill', '--signal=INT', $containerName) -Evidence "send SIGINT during $Phase" | Out-Null
    $exitCode = ((Invoke-Docker -Arguments @('wait', $containerName) -Evidence "wait for graceful exit during $Phase") -join '').Trim()
    if ($exitCode -ne '0') {
        throw "Application exited with code '$exitCode' during '$Phase'."
    }

    Invoke-Docker -Arguments @('rm', $containerName) -Evidence "remove stopped application container during $Phase" | Out-Null
    $appContainers.Remove($containerName) | Out-Null
    $launches.Add([ordered]@{
        phase = $Phase
        expectedRelease = $ExpectedRelease
        healthStatus = [string]$health
        operationsState = $operations.state
        databaseAndMigrationsReady = $operations.databaseAndMigrationsReady
        hostProfile = $operations.hostCapabilities.profile
        purposeRootCount = @($operations.hostCapabilities.purposeRoots).Count
        purposeRoots = @($operations.hostCapabilities.purposeRoots | ForEach-Object {
            [ordered]@{
                purpose = $_.purpose
                configurationSource = $_.configurationSource
                state = $_.state
                reason = $_.reason
            }
        })
        architecture = $architecture
        dotnetRuntimes = @($runtimes -split "`n")
        effectiveUid = $uid
        effectiveGid = $gid
        exitCode = [int]$exitCode
    })
}

New-Item -ItemType Directory -Path $outputRoot -Force | Out-Null
New-Item -ItemType Directory -Path $certificateDirectory -Force | Out-Null

try {
    & dotnet dev-certs https --export-path $certificatePath --password $certificatePassword
    if ($LASTEXITCODE -ne 0 -or !(Test-Path -LiteralPath $certificatePath)) {
        throw 'Failed to export a disposable Data Protection certificate.'
    }

    Invoke-Docker -Arguments @('network', 'create', $networkName) -Evidence 'create isolated validation network' | Out-Null
    $createdNetwork = $true
    foreach ($volume in @($installVolume, $dataVolume, $certificateVolume, $databaseVolume)) {
        Invoke-Docker -Arguments @('volume', 'create', $volume) -Evidence "create disposable volume $volume" | Out-Null
        $createdVolumes.Add($volume)
    }

    Invoke-Docker -Arguments @(
        'run', '--rm',
        '--user', '0:0',
        '--mount', "type=volume,source=$installVolume,target=/install",
        '--mount', "type=volume,source=$dataVolume,target=/srv",
        '--mount', "type=volume,source=$certificateVolume,target=/cert",
        '--mount', "type=bind,source=$certificatePath,target=/source/dataprotection.pfx,readonly",
        '--entrypoint', '/bin/sh',
        $runtimeImage,
        '-c', 'mkdir -p /install /srv/data /srv/config /srv/state /srv/run /srv/workspace /srv/control-plane /srv/dataprotection-keys /srv/logs && cp /source/dataprotection.pfx /cert/dataprotection.pfx && chown -R 10001:10001 /install /srv/data /srv/config /srv/state /srv/run /srv/workspace /srv/control-plane /srv/dataprotection-keys /srv/logs /cert/dataprotection.pfx && chmod 0750 /install /srv/data /srv/config /srv/state /srv/run /srv/workspace /srv/control-plane /srv/dataprotection-keys /srv/logs && chmod 0600 /cert/dataprotection.pfx'
    ) -Evidence 'initialize owned install and data volumes for uid 10001 gid 10001' | Out-Null

    Invoke-Docker -Arguments @(
        'run', '--detach',
        '--name', $databaseContainer,
        '--network', $networkName,
        '--mount', "type=volume,source=$databaseVolume,target=/var/lib/postgresql/data",
        '--env', "POSTGRES_USER=$databaseUser",
        '--env', "POSTGRES_DB=$databaseName",
        '--env', "POSTGRES_PASSWORD=$databasePassword",
        $databaseImage
    ) -Evidence 'launch disposable PostgreSQL 16 (password redacted)' -Sensitive | Out-Null
    Wait-DatabaseReady

    Install-Release -ReleaseId '2026.08.10-1'
    Invoke-ApplicationLaunch -Phase '1' -ExpectedRelease '2026.08.10-1'
    Install-Release -ReleaseId '2026.08.10-2'
    Invoke-ApplicationLaunch -Phase '2' -ExpectedRelease '2026.08.10-2'
    Invoke-Docker -Arguments @(
        'run', '--rm',
        '--user', '10001:10001',
        '--mount', "type=volume,source=$installVolume,target=/install",
        '--entrypoint', '/bin/sh',
        $runtimeImage,
        '/install/bin/rollback-candoitall-web.sh', '/install'
    ) -Evidence 'activate previous immutable release using rollback script' | Out-Null
    Invoke-ApplicationLaunch -Phase '3' -ExpectedRelease '2026.08.10-1'

    $runtimeImageMetadata = Invoke-Docker -Arguments @('image', 'inspect', $runtimeImage, '--format', '{{json .}}') -Evidence 'inspect exact Ubuntu ASP.NET runtime image'
    $runtimeImageObject = (($runtimeImageMetadata -join '') | ConvertFrom-Json)
    $databaseImageMetadata = Invoke-Docker -Arguments @('image', 'inspect', $databaseImage, '--format', '{{json .}}') -Evidence 'inspect exact PostgreSQL image'
    $databaseImageObject = (($databaseImageMetadata -join '') | ConvertFrom-Json)
    $osRelease = (Invoke-Docker -Arguments @('run', '--rm', '--entrypoint', '/bin/sh', $runtimeImage, '-c', 'cat /etc/os-release') -Evidence 'capture final Ubuntu image /etc/os-release') -join "`n"

    $provenance = [ordered]@{
        generatedAtUtc = [DateTimeOffset]::UtcNow.ToString('O')
        evidenceScope = 'A06 Linux actual-host install, upgrade, rollback, restart, readiness, identity, and provenance validation'
        hostBoundary = 'Docker Desktop Linux container using the official Ubuntu 24.04 .NET 10 ASP.NET runtime image'
        runtimeImage = [ordered]@{
            reference = $runtimeImage
            imageId = $runtimeImageObject.Id
            repositoryDigests = @($runtimeImageObject.RepoDigests)
            osRelease = @($osRelease -split "`n")
        }
        databaseImage = [ordered]@{
            reference = $databaseImage
            imageId = $databaseImageObject.Id
            repositoryDigests = @($databaseImageObject.RepoDigests)
        }
        artifact = [ordered]@{
            publishDirectoryOutsideRepository = !$publishRoot.StartsWith($repositoryRoot, [StringComparison]::OrdinalIgnoreCase)
            webDllSha256 = (Get-FileHash -LiteralPath (Join-Path $publishRoot 'CanDoItAll.Web.dll') -Algorithm SHA256).Hash
            runtimeSupportSha256 = (Get-FileHash -LiteralPath (Join-Path $publishRoot 'runtime-support.json') -Algorithm SHA256).Hash
        }
        deploymentIdentity = [ordered]@{
            requestedUid = 10001
            requestedGid = 10001
            rootExecutionRequired = $false
        }
        launches = @($launches)
        commands = @($commands)
        sensitiveValuesCaptured = $false
        physicalPurposeRootsCaptured = $false
    }

    [IO.File]::WriteAllText(
        (Join-Path $outputRoot 'A06-linux-host-provenance.json'),
        ($provenance | ConvertTo-Json -Depth 30))
}
finally {
    foreach ($container in @($appContainers)) {
        & docker rm --force $container *> $null
    }

    & docker rm --force $databaseContainer *> $null
    if ($createdNetwork) {
        & docker network rm $networkName *> $null
    }

    foreach ($volume in @($createdVolumes)) {
        & docker volume rm $volume *> $null
    }

    if (Test-Path -LiteralPath $certificateDirectory) {
        Remove-Item -LiteralPath $certificateDirectory -Recurse -Force
    }
}
