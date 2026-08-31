$ErrorActionPreference = 'Stop'
$repositoryRoot = 'C:/repositories/CanDoItAll'
$deploymentRoot = Join-Path $repositoryRoot '.artifacts/agent-startup-performance/deployment'
$artifactRoot = Join-Path $repositoryRoot '.artifacts/asn-20260831'
$publishRoot = Join-Path $deploymentRoot 'native-publish'
$log = Join-Path $deploymentRoot 'native-publish.log'
$arguments = @('publish','src/App/CanDoItAll.Web/CanDoItAll.Web.csproj','--configuration','Release','--artifacts-path',$artifactRoot,'--output',$publishRoot,'-p:UseLocalCanDoItAllLibraries=true','--verbosity','minimal')
$startedUtc = [DateTimeOffset]::UtcNow
& dotnet @arguments *> $log
$exitCode = $LASTEXITCODE
[ordered]@{Executable='dotnet';Arguments=$arguments;WorkingDirectory=$repositoryRoot;StartedUtc=$startedUtc;CompletedUtc=[DateTimeOffset]::UtcNow;ExitCode=$exitCode} |
    ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $deploymentRoot 'native-publish.command.json') -Encoding utf8NoBOM
Get-Content -LiteralPath $log -Tail 12
if ($exitCode -ne 0) {
    exit $exitCode
}
$hashes = foreach ($path in @(
    (Join-Path $artifactRoot 'bin/CanDoItAll.Web/release/CanDoItAll.Web.dll'),
    (Join-Path $publishRoot 'CanDoItAll.Web.dll'),
    (Join-Path $repositoryRoot 'src/App/CanDoItAll.Web/bin/Release/net10.0/CanDoItAll.Web.dll'))) {
    Get-FileHash -LiteralPath $path -Algorithm SHA256 | Select-Object Path,Hash
}
$hashes | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $deploymentRoot 'native-build-hashes.json') -Encoding utf8NoBOM
$hashes | ConvertTo-Json -Compress
