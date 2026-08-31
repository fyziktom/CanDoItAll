[CmdletBinding(SupportsShouldProcess)]
param(
    [Parameter(Mandatory)][int]$ExpectedProcessId,
    [Parameter(Mandatory)][string]$WebAssemblyPath,
    [Parameter(Mandatory)][ValidatePattern('^[A-Fa-f0-9]{64}$')][string]$ExpectedAssemblySha256,
    [Parameter(Mandatory)][string]$SharedInfoRoot,
    [switch]$AllowPrecommit
)
$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..\..\..')).Path
$sharedRoot = (Resolve-Path -LiteralPath $SharedInfoRoot).Path
$assemblyPath = (Resolve-Path -LiteralPath $WebAssemblyPath).Path
$process = Get-Process -Id $ExpectedProcessId
$expectedExecutable = [IO.Path]::ChangeExtension($assemblyPath, '.exe')
if (-not $process.Path.Equals($expectedExecutable, [StringComparison]::OrdinalIgnoreCase)) {
    throw 'The supplied process is not running the identified Web output.'
}
if ($process.StartTime.ToUniversalTime() -lt (Get-Item -LiteralPath $assemblyPath).LastWriteTimeUtc) {
    throw 'The Web assembly changed after host startup. Restart the identified host.'
}
$assemblyHash = (Get-FileHash -LiteralPath $assemblyPath -Algorithm SHA256).Hash
if ($assemblyHash -ne $ExpectedAssemblySha256) { throw 'Web assembly hash does not match the reviewed build.' }
$listeners = @(Get-NetTCPConnection -State Listen -LocalPort 5032 -ErrorAction Stop)
if (!$listeners -or @($listeners | Where-Object OwningProcess -ne $ExpectedProcessId).Count) {
    throw 'The identified process does not exclusively own canonical port 5032.'
}
$status = @(& git -C $repoRoot status --porcelain=v1)
if ($LASTEXITCODE -ne 0) { throw 'Cannot read product source status.' }
if ($status.Count -and !$AllowPrecommit) { throw 'A non-clean capture requires explicit -AllowPrecommit authority.' }
$baseline = (& git -C $repoRoot rev-parse HEAD).Trim()
$branch = (& git -C $repoRoot branch --show-current).Trim()
$statusHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData(
    [Text.Encoding]::UTF8.GetBytes($status -join [char]10)))
$captureRoot = Join-Path $repoRoot 'artifacts/providers-shared-premerge/openapi'
New-Item -ItemType Directory -Force -Path $captureRoot | Out-Null
$openApiPath = Join-Path $captureRoot 'openapi.json'
$swaggerPath = Join-Path $captureRoot 'swagger.json'
Invoke-WebRequest -Uri 'http://localhost:5032/openapi/v1.json' -OutFile $openApiPath
Invoke-WebRequest -Uri 'http://localhost:5032/swagger/v1/swagger.json' -OutFile $swaggerPath
$openApiBytes = [IO.File]::ReadAllBytes($openApiPath)
$swaggerBytes = [IO.File]::ReadAllBytes($swaggerPath)
if (![Linq.Enumerable]::SequenceEqual[byte]($openApiBytes, $swaggerBytes)) {
    throw 'Runtime document endpoints returned different bytes.'
}
$hash = (Get-FileHash -LiteralPath $openApiPath -Algorithm SHA256).Hash
$document = [Text.Encoding]::UTF8.GetString($openApiBytes) | ConvertFrom-Json -AsHashtable
if ($document.servers.Count -ne 1 -or $document.servers[0].url -ne 'http://localhost:5032/') {
    throw 'Generated server URL is not canonical. Do not edit the generated JSON.'
}
$methods = @('get', 'put', 'post', 'delete', 'options', 'head', 'patch', 'trace')
$operations = @(foreach ($path in ($document.paths.Keys | Sort-Object)) {
    foreach ($method in ($document.paths[$path].Keys | Sort-Object)) {
        if ($method -in $methods) {
            [ordered]@{ method = $method.ToUpperInvariant(); path = $path; operationId = $document.paths[$path][$method].operationId }
        }
    }
})
if (@($operations | Where-Object { $_.path.StartsWith('/api/shared-providers/') }).Count -ne 5) {
    throw 'Expected exactly five shared-provider operations.'
}
$support = Join-Path $sharedRoot 'codex/skills/_candoitall-api-shared'
$manifestPath = Join-Path $support 'manifest.json'
$manifest = Get-Content -Raw -LiteralPath $manifestPath | ConvertFrom-Json -AsHashtable
$manifest.source.branch = $branch
$manifest.source.commit = $baseline
$manifest.source.workingTreeClean = $status.Count -eq 0
$manifest.source.workingTreeNote = if ($status.Count) {
    'Pre-merge capture from the committed repair baseline plus uncommitted finishing changes. Product SB09 finishing proof identifies changed-file hashes. No automatic commit was made.'
} else {
    'Capture from the recorded clean source commit. No automatic commit was made.'
}
$manifest.source.workingTreeStatusSha256 = $statusHash
$manifest.source.captureNote = "Identified Release host PID $ExpectedProcessId; Web assembly SHA-256 $assemblyHash. Both canonical endpoints returned identical $($openApiBytes.Length)-byte documents. Runtime details remain in product proof."
$manifest.source.generatedUtc = [DateTimeOffset]::UtcNow.UtcDateTime.ToString('O')
$manifest.artifact.sha256 = $hash
$manifest.artifact.openapiVersion = $document.openapi
$manifest.artifact.documentTitle = $document.info.title
$manifest.artifact.documentVersion = $document.info.version
$manifest.artifact.serverUrl = $document.servers[0].url
$manifest.artifact.pathCount = $document.paths.Count
$manifest.artifact.operationCount = $operations.Count
$manifest.artifact.schemaCount = $document.components.schemas.Count
$prefixes = @($document.paths.Keys | ForEach-Object {
    $parts = $_.Split('/', [StringSplitOptions]::RemoveEmptyEntries)
    if ($parts[0] -eq 'api') { "/api/$($parts[1])" } else { "/$($parts[0])" }
} | Sort-Object -Unique)
$manifest.routeFamilies = @($prefixes | ForEach-Object {
    $prefix = $_
    $paths = @($document.paths.Keys | Where-Object { $_ -eq $prefix -or $_.StartsWith("$prefix/") })
    [ordered]@{
        prefix = $prefix
        pathCount = $paths.Count
        operationCount = @($operations | Where-Object { $_.path -in $paths }).Count
    }
})
$sets = @($manifest.documentedOperationSets | Where-Object prefix -ne '/api/shared-providers')
$sets += [ordered]@{
    name = 'Shared Providers API'
    prefix = '/api/shared-providers'
    skillFile = '../candoitall-api-shared-providers/SKILL.md'
    routeAppendix = 'api-docs-skills-parity:routes'
    operations = @()
}
foreach ($set in $sets) {
    $set.operations = @($operations | Where-Object { $_.path -eq $set.prefix -or $_.path.StartsWith("$($set.prefix)/") })
}
$manifest.documentedOperationSets = $sets
if ($PSCmdlet.ShouldProcess($support, 'Replace generated OpenAPI and complete manifest')) {
    Copy-Item -LiteralPath $openApiPath -Destination (Join-Path $support $manifest.artifact.file)
    [IO.File]::WriteAllText($manifestPath, ($manifest | ConvertTo-Json -Depth 100) + [char]10)
}
[ordered]@{
    processId = $ExpectedProcessId
    assemblySha256 = $assemblyHash
    artifactSha256 = $hash
    bytes = $openApiBytes.Length
    paths = $manifest.artifact.pathCount
    operations = $operations.Count
    schemas = $manifest.artifact.schemaCount
    sourceCommit = $baseline
    workingTreeClean = $manifest.source.workingTreeClean
    statusSha256 = $statusHash
} | ConvertTo-Json
