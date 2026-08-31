[CmdletBinding(SupportsShouldProcess)]
param([Parameter(Mandatory)][string]$Image, [string]$BackupSuffix = 'premerge-20260831')
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../../..')).Path
$expectedRoot = Join-Path $repoRoot '.artifacts/shared-providers-e2e'
$names = @('candoitall-shared-providers-manual-central-1', 'candoitall-shared-providers-manual-client-a-1')
$imageConfig = @(docker image inspect $Image | ConvertFrom-Json -AsHashtable)[0]
if ($LASTEXITCODE -ne 0 -or !$imageConfig.Id) { throw 'Requested image is unavailable.' }
$dockerEndpoint = (docker context inspect --format '{{.Endpoints.docker.Host}}').Trim()
if ($dockerEndpoint -ne 'npipe:////./pipe/dockerDesktopLinuxEngine') { throw 'Unexpected Docker endpoint.' }
Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.IO.Pipes;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

public static class FinishingDockerApi {
    public static string Create(string name, string json) {
        using var handler = new SocketsHttpHandler {
            ConnectCallback = async (_, cancellationToken) => {
                var pipe = new NamedPipeClientStream(".", "dockerDesktopLinuxEngine", PipeDirection.InOut, PipeOptions.Asynchronous);
                try {
                    await pipe.ConnectAsync(cancellationToken);
                    return pipe;
                } catch {
                    pipe.Dispose();
                    throw;
                }
            }
        };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = client.PostAsync("http://localhost/containers/create?name=" + Uri.EscapeDataString(name), content).GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode) {
            throw new InvalidOperationException("Docker create failed with HTTP " + (int)response.StatusCode + ". No configuration values were logged.");
        }
        return response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
    }
}
'@
$configurations = @{}
foreach ($name in $names) {
    $configuration = @(docker inspect $name | ConvertFrom-Json -AsHashtable)[0]
    if ($LASTEXITCODE -ne 0 -or $configuration.Config.User -ne '1654:1654' -or !$configuration.HostConfig.ReadonlyRootfs) {
        throw "Unexpected container security configuration: $name"
    }
    $expectedPort = if ($name -like '*central*') { '5210' } else { '5214' }
    $binding = $configuration.HostConfig.PortBindings['8080/tcp']
    if ($binding.Count -ne 1 -or $binding[0].HostIp -ne '127.0.0.1' -or $binding[0].HostPort -ne $expectedPort) {
        throw "Unexpected published port: $name"
    }
    foreach ($mount in $configuration.HostConfig.Mounts) {
        $resolved = [IO.Path]::GetFullPath($mount.Source)
        if ($mount.Type -ne 'bind' -or !$resolved.StartsWith($expectedRoot + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or !(Test-Path -LiteralPath $resolved)) {
            throw "Unexpected or missing persistent mount: $name"
        }
    }
    $existingNames = @(docker ps --all --format '{{.Names}}')
    if ($existingNames -contains "$name-$BackupSuffix") { throw "Rollback name already exists: $name-$BackupSuffix" }
    $configurations[$name] = $configuration
}
if (!$PSCmdlet.ShouldProcess(($names -join ', '), "Replace with $Image; preserve all persistent mounts, settings, ports and networks")) { return }
$renamed = [Collections.Generic.List[string]]::new()
$created = [Collections.Generic.List[string]]::new()
try {
    foreach ($name in $names) {
        $configuration = $configurations[$name]
        docker stop --time 40 $name | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Cannot stop $name" }
        docker rename $name "$name-$BackupSuffix"
        if ($LASTEXITCODE -ne 0) {
            docker start $name | Out-Host
            throw "Cannot retain rollback container for $name"
        }
        $renamed.Add($name)
        $create = $configuration.Config
        $create.Image = $imageConfig.Id
        $create.Hostname = ''
        foreach ($label in $imageConfig.Config.Labels.Keys) { $create.Labels[$label] = $imageConfig.Config.Labels[$label] }
        $create.Labels['com.docker.compose.image'] = $imageConfig.Id
        $create.Labels['io.candoitall.finishing-source'] = 'preserved-live-configuration-20260831'
        $create.HostConfig = $configuration.HostConfig
        $endpoints = @{}
        foreach ($network in $configuration.NetworkSettings.Networks.Keys) {
            $oldEndpoint = $configuration.NetworkSettings.Networks[$network]
            $endpoints[$network] = @{Aliases = $oldEndpoint.Aliases; DriverOpts = $oldEndpoint.DriverOpts; GwPriority = $oldEndpoint.GwPriority}
        }
        $create.NetworkingConfig = @{EndpointsConfig = $endpoints}
        $result = [FinishingDockerApi]::Create($name, ($create | ConvertTo-Json -Depth 100 -Compress)) | ConvertFrom-Json
        $created.Add($name)
        docker start $name | Out-Host
        if ($LASTEXITCODE -ne 0) { throw "Cannot start replacement $name" }
        Write-Output "$name replaced with $($result.Id); rollback container retained; data unchanged."
    }
} catch {
    foreach ($name in $created) {
        docker stop --time 20 $name | Out-Host
        docker rm $name | Out-Host
    }
    foreach ($name in $renamed) {
        docker rename "$name-$BackupSuffix" $name
        docker start $name | Out-Host
    }
    throw
}
