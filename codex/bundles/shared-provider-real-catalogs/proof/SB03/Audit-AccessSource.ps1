$ErrorActionPreference = 'Stop'
$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '../../../../..')).Path
$bundleRoot = Join-Path $repoRoot 'codex/bundles/shared-provider-real-catalogs'
Set-Location -LiteralPath $repoRoot
$transcript = Join-Path $PSScriptRoot 'transcripts/source-audit.txt'
Start-Transcript -Path $transcript -Force | Out-Null
try {
    Write-Output 'LOCAL-UI-ACCESS API-BOUNDARY production source and provenance audit'
    $production = @(
        'src/App/CanDoItAll.Web/Infrastructure/LocalOperatorAuthenticationStateProvider.cs',
        'src/App/CanDoItAll.Web/Infrastructure/InteractiveServerServiceCollectionExtensions.cs',
        'src/App/CanDoItAll.Web/Infrastructure/LocalOperatorUiOptions.cs'
    )
    $identity = Get-Content -LiteralPath $production[0] -Raw
    foreach ($assertion in @('IOptions<LocalOperatorUiOptions>', 'apiOptions.Value.Authorization.Enabled',
        'IsTrustedAddress(originalRemoteIp) &&', 'IsTrustedAddress(httpContext.Connection.RemoteIpAddress)',
        'authenticationState.User.Identity?.IsAuthenticated != true', 'ApiAccessScopeNames.ReadLlmChats',
        'ApiAccessScopeNames.ManageLlmChats', 'ApiAccessScopeNames.ExecuteLlmChats')) {
        if (-not $identity.Contains($assertion)) {
            throw "Missing production assertion: $assertion"
        }
        Write-Output "PASS identity assertion: $assertion"
    }
    if ($identity -match 'hostProfile|HttpContext\.User\s*=|ApiAccessScopeNames\.Api\b|172\.31\.') {
        throw 'Identity owner contains a forbidden gate, broad scope, mutation or hard-coded ingress.'
    }
    if (-not (Get-Content -LiteralPath $production[1] -Raw).Contains('.ValidateOnStart()')) {
        throw 'Trust configuration must fail predictably at startup.'
    }
    Write-Output 'Command: rg -n TODO|NotImplemented|e2e-|deterministic.fixture|AllowAnonymous against the three owned production files'
    & rg -n 'TODO|NotImplemented|e2e-|deterministic.fixture|AllowAnonymous' @production | Out-Host
    if ($LASTEXITCODE -ne 1) {
        throw 'Unexpected stub/bypass marker or search failure.'
    }
    Write-Output 'PASS no fixture, stub or anonymous HTTP bypass in owned production'
    Write-Output 'Command: git diff --exit-code -- API/dev/middleware/file-access owners and project files'
    & git diff --exit-code -- src/App/CanDoItAll.Web/Program.cs src/App/CanDoItAll.Web/DevelopmentEndpointAccess.cs src/App/CanDoItAll.Web/Api/ApiAuthorizationPolicies.cs src/App/CanDoItAll.Web/Infrastructure/HttpFileAccessContextProvider.cs src/App/CanDoItAll.Web/Composition/LlmChatsUiComposition.cs '*.csproj' | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw 'Protected boundary or project reference changed.'
    }
    Write-Output 'PASS HTTP, dev, application-policy and project boundaries unchanged'
    Write-Output 'Command: git diff --check'
    & git diff --check | Out-Host
    if ($LASTEXITCODE -ne 0) {
        throw 'Whitespace check failed.'
    }
    $before = @{}
    Import-Csv -LiteralPath (Join-Path $PSScriptRoot 'before-hashes.csv') | ForEach-Object { $before[$_.Path] = $_.Hash }
    $paths = @(& git diff --name-only) + @(& git ls-files --others --exclude-standard)
    $paths = @($paths | Where-Object {
        ($_ -eq '.gitignore' -or $_ -match '\.(cs|md|ps1)$') -and
        ($_ -notmatch '/proof/SB03/' -or $_ -match '\.ps1$' -or $_ -match '/(manifest|semantic-invariants)\.md$')
    } | Sort-Object -Unique)
    $hashes = foreach ($path in $paths) {
        $beforeHash = 'ABSENT-new-file'
        $beforeSource = 'New file in SB03'
        if ($before.ContainsKey($path)) {
            $beforeHash = $before[$path]
            $beforeSource = 'Exact pre-edit working-tree capture'
        } elseif (@(& git ls-files -- $path).Count -gt 0) {
            $start = [Diagnostics.ProcessStartInfo]::new('git')
            $start.UseShellExecute = $false
            $start.RedirectStandardOutput = $true
            $start.CreateNoWindow = $true
            $start.ArgumentList.Add('cat-file')
            $start.ArgumentList.Add('blob')
            $start.ArgumentList.Add("HEAD:$path")
            $process = [Diagnostics.Process]::Start($start)
            $bytes = [IO.MemoryStream]::new()
            $process.StandardOutput.BaseStream.CopyTo($bytes)
            $process.WaitForExit()
            if ($process.ExitCode -ne 0) {
                throw "Cannot hash baseline blob: $path"
            }
            $beforeHash = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($bytes.ToArray()))
            $beforeSource = 'Baseline HEAD 3b998b363 raw Git blob, not exact pre-edit working-tree bytes'
            $process.Dispose()
            $bytes.Dispose()
        }
        [pscustomobject]@{
            Path = "repo://$path"
            BeforeSHA256 = $beforeHash
            BeforeSource = $beforeSource
            AfterSHA256 = (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash
        }
    }
    $hashes | Export-Csv -LiteralPath (Join-Path $PSScriptRoot 'changed-files.csv') -NoTypeInformation
    Write-Output "PASS current SHA256 for $($hashes.Count) changed code/test/document files; seven initial captures include two absent new files."
    Write-Output 'Exit code: 0'
} finally {
    Stop-Transcript | Out-Null
}
$artifactHashes = Get-ChildItem -LiteralPath $PSScriptRoot -Recurse -File |
    Where-Object { $_.Name -ne 'proof-artifacts.csv' } | Sort-Object FullName | ForEach-Object {
        [pscustomobject]@{
            Path = 'bundle://' + [IO.Path]::GetRelativePath($bundleRoot, $_.FullName).Replace('\', '/')
            SHA256 = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash
        }
    }
$artifactHashes | Export-Csv -LiteralPath (Join-Path $PSScriptRoot 'proof-artifacts.csv') -NoTypeInformation
