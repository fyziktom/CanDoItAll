[CmdletBinding(SupportsShouldProcess)]
param([switch]$Execute)

$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
$repositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..')).Path
$validationRoot = Join-Path $repositoryRoot '.artifacts\agent-startup-performance\frozen-validation'
$proofRoot = Join-Path $repositoryRoot 'codex\bundles\agent-startup-performance\proof\frozen-integration'
$expectedOldId = 'aa1bac9c700103cc7738d33c1c5b14fbaa076fb128f387747a057951b81db9f9'
$expectedImageId = 'sha256:54451ecb8ab38c24c3ec123f2fd501303a3a1856a5c66e98cecf2460d5e1e9d7'
$newContainerName = 'candoitall-agent-startup-tests-20260831-capacity2'
$privateConfigurationPath = Join-Path $PSScriptRoot 'private\connection.json'
$privateEnvironmentPath = Join-Path $PSScriptRoot 'private\postgres.env'

function Get-LiveHostIdentity {
    $identifiers = @(& docker ps -q)
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not inspect running container identities.'
    }
    $containers = @(& docker inspect @identifiers | ConvertFrom-Json)
    if ($LASTEXITCODE -ne 0) {
        throw 'Could not read running container identities.'
    }
    $records = foreach ($container in $containers) {
        $bindings = @($container.HostConfig.PortBindings.PSObject.Properties.Value | ForEach-Object { $_ } | Where-Object { $_.HostPort -in @('5432', '5210', '5214') })
        if ($bindings.Count -gt 0) {
            [pscustomobject]@{ Name = $container.Name; Id = $container.Id; Image = $container.Image; StartedAt = $container.State.StartedAt; Bindings = @($bindings | Select-Object HostIp, HostPort) }
        }
    }
    return @($records | Sort-Object Name)
}

if (-not $Execute) {
    Write-Output 'Prepared only: requires -Execute and the completed first Integration evidence; replaces only the identity-checked disposable test server.'
    return
}
$commandPath = Join-Path $validationRoot 'frozen-integration-integration-execution.log.command.json'
$summaryPath = Join-Path $validationRoot 'frozen-integration-integration-execution.log.summary.json'
$integrationCommand = Get-Content -LiteralPath $commandPath -Raw | ConvertFrom-Json
$integrationSummary = Get-Content -LiteralPath $summaryPath -Raw | ConvertFrom-Json
if (-not $integrationCommand.CompletedUtc -or -not $integrationSummary.Executed) {
    throw 'First Integration execution has not completed with retained runtime results.'
}
$activeTestProcesses = @(Get-CimInstance Win32_Process | Where-Object { $_.Name -in @('dotnet.exe', 'testhost.exe') -and $_.CommandLine -match 'agent-startup-performance[\\/]sb01-tests' })
if ($activeTestProcesses.Count -ne 0) {
    throw 'An owned test process is still using the frozen artifact tree.'
}
$configuration = Get-Content -LiteralPath $privateConfigurationPath -Raw | ConvertFrom-Json
$oldContainer = (& docker inspect $configuration.ContainerName | ConvertFrom-Json)[0]
if ($LASTEXITCODE -ne 0 -or $configuration.ContainerId -ne $expectedOldId -or $oldContainer.Id -ne $expectedOldId -or $oldContainer.Image -ne $expectedImageId -or -not $oldContainer.State.Running) {
    throw 'Old disposable PostgreSQL identity or image does not match the approved replacement.'
}
if ($oldContainer.Config.Labels.'candoitall.owner' -ne 'agent-startup-performance-tests' -or $oldContainer.Config.Labels.'candoitall.scope' -ne 'disposable-test-postgres') {
    throw 'Old disposable PostgreSQL ownership mismatch.'
}
$binding = @($oldContainer.HostConfig.PortBindings.'5432/tcp')
if ($binding.Count -ne 1 -or $binding[0].HostIp -ne '127.0.0.1' -or $binding[0].HostPort -ne '52049') {
    throw 'Old disposable PostgreSQL endpoint mismatch.'
}
if (-not (Test-Path -LiteralPath $privateEnvironmentPath -PathType Leaf)) {
    throw 'Private PostgreSQL environment file is missing.'
}
$existingNew = @(& docker ps -a --filter "name=^/$newContainerName$" --format '{{.ID}}')
if ($existingNew.Count -ne 0) {
    throw 'The replacement container name already exists; refusing to overwrite it.'
}
$liveBefore = Get-LiveHostIdentity
if (@($liveBefore | Where-Object { $_.Id -eq '000fadde7e6757f7afd413e3102fa58568e18da4d9a7361d8057bda40c9b966d' }).Count -ne 1 -or @($liveBefore | Where-Object { $_.Id -eq 'fb12806ab50b7bdadb68175ce79d6efb8596b3f4f62329f07f445ae49074226e' }).Count -ne 1) {
    throw 'Publisher/client frozen live identities changed before test-server replacement.'
}
if (-not $PSCmdlet.ShouldProcess($expectedOldId, 'Stop and retain the approved disposable test server, then create the same image on port52049 with4GiB tmpfs and6GiB memory cap')) {
    return
}
Copy-Item -LiteralPath $commandPath -Destination (Join-Path $proofRoot 'integration-first-execution-command.json')
Copy-Item -LiteralPath $summaryPath -Destination (Join-Path $proofRoot 'integration-first-execution-summary.json')
Copy-Item -LiteralPath $privateConfigurationPath -Destination (Join-Path $PSScriptRoot 'private\connection-capacity1.json')
$startedUtc = [DateTimeOffset]::UtcNow.ToString('o')
& docker stop --time 30 $expectedOldId | Out-Null
if ($LASTEXITCODE -ne 0) {
    throw 'Could not stop the approved disposable test server.'
}
$createdId = & docker run --detach --pull never --name $newContainerName --publish '127.0.0.1:52049:5432' --memory 6g --cpus 2 --tmpfs '/var/lib/postgresql:rw,size=4294967296' --env-file $privateEnvironmentPath --label 'candoitall.owner=agent-startup-performance-tests' --label 'candoitall.scope=disposable-test-postgres' --health-cmd 'pg_isready -U cditall_startup_tests -d postgres' --health-interval 2s --health-timeout 2s --health-start-period 5s --health-retries 20 $expectedImageId
if ($LASTEXITCODE -ne 0 -or -not $createdId) {
    throw 'Replacement creation failed; the original disposable test container is retained stopped.'
}
$deadline = [DateTimeOffset]::UtcNow.AddSeconds(60)
do {
    $replacement = (& docker inspect $createdId | ConvertFrom-Json)[0]
    if ($LASTEXITCODE -ne 0 -or -not $replacement.State.Running) {
        throw 'Replacement disposable PostgreSQL is not running.'
    }
    if ($replacement.State.Health.Status -eq 'healthy') {
        break
    }
    Start-Sleep -Seconds 1
} while ([DateTimeOffset]::UtcNow -lt $deadline)
if ($replacement.State.Health.Status -ne 'healthy') {
    throw 'Replacement disposable PostgreSQL failed the bounded health gate.'
}
$stoppedOriginal = (& docker inspect $expectedOldId | ConvertFrom-Json)[0]
if ($stoppedOriginal.State.Running -or $replacement.Image -ne $expectedImageId -or $replacement.HostConfig.Memory -ne 6442450944 -or $replacement.HostConfig.NanoCpus -ne 2000000000 -or $replacement.HostConfig.Tmpfs.'/var/lib/postgresql' -ne 'rw,size=4294967296') {
    throw 'Replacement resource configuration or original stopped state does not match approval.'
}
$liveAfter = Get-LiveHostIdentity
if (($liveBefore | ConvertTo-Json -Depth 6 -Compress) -ne ($liveAfter | ConvertTo-Json -Depth 6 -Compress)) {
    throw 'A live5432/5210/5214 container identity changed; do not proceed with tests.'
}
$configuration.ContainerName = $newContainerName
$configuration.ContainerId = [string]$createdId
$configuration | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $privateConfigurationPath -Encoding utf8
. (Join-Path $PSScriptRoot 'Enter-IsolatedPostgresTestEnvironment.ps1')
$evidence = [pscustomobject]@{ StartedUtc = $startedUtc; CompletedUtc = [DateTimeOffset]::UtcNow.ToString('o'); Authorization = 'Root approved after first full Integration and its children exit; preserve original run and stop-retain exact owned server.'; RetainedStoppedContainerId = $expectedOldId; ContainerName = $newContainerName; ContainerId = [string]$createdId; ImageId = $replacement.Image; HostEndpoint = '127.0.0.1:52049'; DataTmpfsBytes = 4294967296; MemoryLimitBytes = $replacement.HostConfig.Memory; NanoCpus = $replacement.HostConfig.NanoCpus; Healthy = $replacement.State.Health.Status; LiveContainersUnchanged = $true; LiveBefore = $liveBefore; LiveAfter = $liveAfter; Credentials = 'Reused private existing environment; no values emitted.' }
$evidence | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $validationRoot 'test-postgres-capacity-replacement.json') -Encoding utf8
$evidence | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $proofRoot 'test-postgres-capacity-replacement.json') -Encoding utf8
Write-Output ('Owned disposable PostgreSQL capacity replacement healthy: ' + $createdId + '; live5432/5210/5214 unchanged.')