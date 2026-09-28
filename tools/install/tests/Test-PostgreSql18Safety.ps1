[CmdletBinding()]
param([switch]$RunDocker, [string]$NativeBinPath = "")

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repositoryRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../../..'))
$installerPath = Join-Path $repositoryRoot 'tools/install/Install-CanDoItAllWebAppDatabase.ps1'
$tokens = $null
$errors = $null
$ast = [Management.Automation.Language.Parser]::ParseFile($installerPath, [ref]$tokens, [ref]$errors)
if ($errors.Count -ne 0) { throw 'Installer does not parse.' }
foreach ($statement in $ast.EndBlock.Statements) {
    if ($statement -is [Management.Automation.Language.FunctionDefinitionAst]) { break }
    if ($statement -is [Management.Automation.Language.AssignmentStatementAst]) {
        . ([scriptblock]::Create($statement.Extent.Text))
    }
}
foreach ($definition in $ast.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] }, $false)) {
    . ([scriptblock]::Create($definition.Extent.Text))
}

$readyDefinition = $ast.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Wait-DockerDatabaseReady' }, $false)
. ([scriptblock]::Create($readyDefinition.Extent.Text.Replace('function Wait-DockerDatabaseReady {', 'function Wait-DockerDatabaseReadyCore {')))
function Wait-DockerDatabaseReady {
    param([string]$ContainerName)
    try { Wait-DockerDatabaseReadyCore -ContainerName $ContainerName }
    catch {
        $diagnosticPath = Join-Path ([IO.Path]::GetTempPath()) "$ContainerName-failure.txt"
        $previousPreference = $ErrorActionPreference
        $ErrorActionPreference = 'Continue'
        & $script:DockerExe logs $ContainerName 2>&1 | Set-Content -LiteralPath $diagnosticPath
        $ErrorActionPreference = $previousPreference
        Write-Host "Disposable fixture startup diagnostics: $diagnosticPath"
        throw
    }
}

function Invoke-GeneratedDatabaseStart {
    param([object]$Manifest, [string]$Backend, [string]$Root)
    $webInstaller = Join-Path $repositoryRoot 'tools/install/Install-CanDoItAllWebApp.ps1'
    $webAst = [Management.Automation.Language.Parser]::ParseFile($webInstaller, [ref]$null, [ref]$null)
    $literalHelper = $webAst.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'ConvertTo-PowerShellSingleQuotedLiteral' }, $false)
    . ([scriptblock]::Create($literalHelper.Extent.Text))
    $factory = $webAst.Find({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] -and $node.Name -eq 'Get-LauncherScriptContent' }, $false)
    . ([scriptblock]::Create($factory.Extent.Text))
    $content = Get-LauncherScriptContent -BindHost '127.0.0.1' -BindUrlHost '127.0.0.1' -Port 5032
    if (-not $content.Contains('candoitall-webapp-db-data')) { throw 'Generated launcher resource contract changed.' }
    $content = $content.Replace('candoitall-webapp-db-data', $script:DockerVolumeName).Replace('candoitall-webapp-db', $script:DockerContainerName)
    $launcherAst = [Management.Automation.Language.Parser]::ParseInput($content, [ref]$null, [ref]$null)
    foreach ($definition in $launcherAst.FindAll({ param($node) $node -is [Management.Automation.Language.FunctionDefinitionAst] }, $false)) {
        . ([scriptblock]::Create($definition.Extent.Text))
    }
    $databaseRoot = $Root
    if ($Backend -eq 'docker') { Start-DockerDatabase -Manifest $Manifest }
    else { Start-NativeDatabase -Manifest $Manifest }
}

function Assert-Rejected {
    param([scriptblock]$Operation, [string]$Case, [string]$ExpectedMessage = '*Migrate-PostgreSql16To18.md*')
    $rejected = $false
    try { & $Operation | Out-Null }
    catch {
        if ($_.Exception.Message -notlike $ExpectedMessage) { throw }
        $rejected = $true
    }
    if (-not $rejected) { throw "Unsafe state was accepted: $Case" }
    Write-Output "Rejected safely: $Case"
}

$testId = [guid]::NewGuid().ToString('N')
$fixtureRoot = Join-Path ([IO.Path]::GetTempPath()) "candoitall-pg18-$testId"
$ownedVolumes = @()
$temporaryFiles = $null
$nativeDataPath = $null
$script:DockerContainerName = "candoitall-pg18-test-$testId"
$script:DockerVolumeName = "$($script:DockerContainerName)-data"
$script:DockerExe = Find-DockerExecutable
New-Item -ItemType Directory -Path $fixtureRoot | Out-Null
try {
    foreach ($case in @('legacy', 'unknown', 'partial', 'missing-existing', 'empty-existing')) {
        $installRoot = Join-Path $fixtureRoot $case
        $dataPath = Join-Path $installRoot 'runtime/database/native/data'
        if ($case -ne 'missing-existing') {
            New-Item -ItemType Directory -Path $dataPath -Force | Out-Null
        }
        if ($case -eq 'legacy') { [IO.File]::WriteAllText((Join-Path $dataPath 'PG_VERSION'), '16') }
        elseif ($case -eq 'partial') { [IO.File]::WriteAllText((Join-Path $dataPath 'PG_VERSION'), '18') }
        elseif ($case -eq 'unknown') { [IO.File]::WriteAllText((Join-Path $dataPath 'unknown-data'), 'retain') }
        else {
            $existingRoot = Join-Path $installRoot 'runtime/database'
            New-Item -ItemType Directory -Path $existingRoot -Force | Out-Null
            Write-DatabaseManifest -ManifestPath (Join-Path $existingRoot 'database-manifest.json') -Engine 'native' `
                -ConfiguredPort 55432 -ConfiguredDatabaseName 'candoitall_webapp' -ConfiguredAppUsername 'candoitall_app' `
                -AppPasswordFile $script:AppPasswordRelativePath -NativeBinRelative $script:NativeBinRelativePath `
                -NativeDataRelative $script:NativeDataRelativePath -NativeLogRelative $script:NativeLogRelativePath
        }
        $before = @(Get-ChildItem $installRoot -File -Recurse | ForEach-Object { (Get-FileHash $_.FullName).Hash }) -join '|'
        Assert-Rejected { & $installerPath -InstallRoot $installRoot } "native $case before provisioning"
        $after = @(Get-ChildItem $installRoot -File -Recurse | ForEach-Object { (Get-FileHash $_.FullName).Hash }) -join '|'
        if ($before -ne $after -or (Test-Path (Join-Path $installRoot 'runtime/database/secrets'))) {
            throw "Native rejection changed state: $case"
        }
    }

    if ($RunDocker) {
        if (-not (Test-LinuxDockerEngine -DockerPath $script:DockerExe)) { throw 'Docker proof requested but Linux Docker is unavailable.' }
        $volumeName = $script:DockerVolumeName
        foreach ($case in @('legacy-root', 'legacy-subdirectory', 'unknown', 'partial', 'conflicting')) {
            $script:DockerVolumeName = "$volumeName-$case"
            $ownedVolumes += $script:DockerVolumeName
            Invoke-CheckedDocker -Arguments @('volume', 'create', $script:DockerVolumeName) | Out-Null
            $prepare = switch ($case) {
                'legacy-root' { 'echo 16 > /fixture/PG_VERSION' }
                'legacy-subdirectory' { 'mkdir /fixture/data; echo 16 > /fixture/data/PG_VERSION' }
                'unknown' { 'echo retain > /fixture/unknown' }
                'partial' { 'mkdir -p /fixture/18/docker; echo 18 > /fixture/18/docker/PG_VERSION' }
                'conflicting' { 'mkdir -p /fixture/18/docker/global /fixture/16/docker; echo 18 > /fixture/18/docker/PG_VERSION; touch /fixture/18/docker/global/pg_control; echo 16 > /fixture/16/docker/PG_VERSION' }
            }
            Invoke-CheckedDocker -Arguments @('run', '--rm', '--network', 'none', '--mount', "type=volume,source=$($script:DockerVolumeName),target=/fixture", '--entrypoint', 'sh', $script:DockerImage, '-ec', $prepare) | Out-Null
            Assert-Rejected { Get-DockerVolumeState } "Docker $case"
            if ($case -eq 'unknown') {
                Assert-Rejected {
                    Invoke-DockerDatabaseSetup -TemporaryRoot $fixtureRoot -ConfiguredPort 5432 -ConfiguredDatabaseName 'unused' -ConfiguredAppUsername 'unused' -ManifestExists $false
                } 'foreign volume before setup' '*not owned*'
                $marker = @(Invoke-CheckedDocker -Arguments @('run', '--rm', '--network', 'none', '--read-only', '--mount', "type=volume,source=$($script:DockerVolumeName),target=/fixture,readonly", '--entrypoint', 'cat', $script:DockerImage, '/fixture/unknown'))
                if (($marker -join '').Trim() -ne 'retain') { throw 'Foreign volume rejection changed its contents.' }
            }
        }
        $script:DockerVolumeName = $volumeName
        $ownedVolumes += $volumeName
        $secretRoot = Join-Path $fixtureRoot 'secrets'
        $secrets = Get-OrCreateProtectedSecrets -SecretRoot $secretRoot -AppPasswordPath (Join-Path $secretRoot 'app-password.dpapi') -AdminPasswordPath (Join-Path $secretRoot 'admin-password.dpapi') -DatabaseAlreadyExists $false
        $temporaryFiles = New-PlaintextCredentialFiles -DatabaseRoot $fixtureRoot -AppSecret $secrets.App -AdminSecret $secrets.Admin -ConfiguredDatabaseName 'candoitall_test' -ConfiguredAppUsername 'candoitall_app' -ServerPort 5432
        $listener = New-Object Net.Sockets.TcpListener([Net.IPAddress]::Loopback, 0)
        $listener.Start()
        $testPort = $listener.LocalEndpoint.Port
        $listener.Stop()
        Invoke-DockerDatabaseSetup -TemporaryRoot $temporaryFiles.Root -ConfiguredPort $testPort -ConfiguredDatabaseName 'candoitall_test' -ConfiguredAppUsername 'candoitall_app' -ManifestExists $false
        $client = @('-X', '-w', '-tA', '-h', '127.0.0.1', '-p', '5432', '-U', 'candoitall_app', '-d', 'candoitall_test', '-v', 'ON_ERROR_STOP=1')
        Write-Utf8NoBomFile -Path (Join-Path $temporaryFiles.Root 'marker.sql') -Content "create table preservation_marker (id text primary key); insert into preservation_marker values ('$testId');"
        Invoke-DockerClientPsql -ServerContainerName $script:DockerContainerName -TemporaryRoot $temporaryFiles.Root -Credential app -PsqlArguments ($client + @('-f', '/input/marker.sql')) | Out-Null
        Assert-ManagedDockerContainer -Inspect (Get-DockerContainerInspect -Name $script:DockerContainerName) -ConfiguredPort $testPort
        Invoke-CheckedDocker -Arguments @('stop', $script:DockerContainerName) | Out-Null
        Invoke-CheckedDocker -Arguments @('rm', $script:DockerContainerName) | Out-Null
        Invoke-DockerDatabaseSetup -TemporaryRoot $temporaryFiles.Root -ConfiguredPort $testPort -ConfiguredDatabaseName 'candoitall_test' -ConfiguredAppUsername 'candoitall_app' -ManifestExists $true
        Write-Utf8NoBomFile -Path (Join-Path $temporaryFiles.Root 'read-marker.sql') -Content "select current_setting('server_version_num') || '|' || id from preservation_marker;"
        $result = @(Invoke-DockerClientPsql -ServerContainerName $script:DockerContainerName -TemporaryRoot $temporaryFiles.Root -Credential app -PsqlArguments ($client + @('-f', '/input/read-marker.sql')))
        if (($result -join '').Trim() -ne "180006|$testId") { throw 'PostgreSQL 18.6 data did not survive container recreation.' }
        Invoke-CheckedDocker -Arguments @('stop', $script:DockerContainerName) | Out-Null
        $manifest = [pscustomobject]@{
            host = '127.0.0.1'; port = $testPort; databaseName = 'candoitall_test'; appUsername = 'candoitall_app'
            docker = [pscustomobject]@{ containerName = $script:DockerContainerName; volumeName = $script:DockerVolumeName; image = $script:DockerImage }
        }
        Invoke-GeneratedDatabaseStart -Manifest $manifest -Backend 'docker' -Root $fixtureRoot
        Write-Output 'Docker 18.6 fresh installation, repair, recreation persistence and generated launcher restart passed.'
    }
    if (-not [string]::IsNullOrWhiteSpace($NativeBinPath)) {
        $NativeBinPath = (Resolve-Path -LiteralPath $NativeBinPath).Path
        $fixturePackage = Join-Path $fixtureRoot 'native/pgsql'
        New-Item -ItemType Directory -Path $fixturePackage -Force | Out-Null
        foreach ($directory in @('bin', 'lib', 'share', 'server_license.txt', 'commandlinetools_3rd_party_licenses.txt')) {
            Copy-Item -LiteralPath (Join-Path (Split-Path $NativeBinPath -Parent) $directory) -Destination $fixturePackage -Recurse
        }
        $NativeBinPath = Join-Path $fixturePackage 'bin'
        Assert-NativePostgreSqlLoads -BinPath $NativeBinPath
        if ($null -ne $temporaryFiles) { Remove-SafeTemporaryDirectory -Path $temporaryFiles.Root -DatabaseRoot $fixtureRoot }
        $listener = New-Object Net.Sockets.TcpListener([Net.IPAddress]::Loopback, 0)
        $listener.Start()
        $nativePort = $listener.LocalEndpoint.Port
        $listener.Stop()
        $secretRoot = Join-Path $fixtureRoot 'native-secrets'
        $secrets = Get-OrCreateProtectedSecrets -SecretRoot $secretRoot -AppPasswordPath (Join-Path $secretRoot 'app-password.dpapi') -AdminPasswordPath (Join-Path $secretRoot 'admin-password.dpapi') -DatabaseAlreadyExists $false
        $temporaryFiles = New-PlaintextCredentialFiles -DatabaseRoot $fixtureRoot -AppSecret $secrets.App -AdminSecret $secrets.Admin -ConfiguredDatabaseName 'candoitall_test' -ConfiguredAppUsername 'candoitall_app' -ServerPort $nativePort
        $nativeDataPath = Join-Path $fixtureRoot 'native/data'
        $setup = @{
            DatabaseRoot = $fixtureRoot
            BinPath = $NativeBinPath
            DataPath = $nativeDataPath
            LogPath = (Join-Path $fixtureRoot 'native/logs/postgresql.log')
            TemporaryRoot = $temporaryFiles.Root
            AdminPasswordPath = $temporaryFiles.AdminPassword
            AdminPgPassPath = $temporaryFiles.AdminPgPass
            AppPgPassPath = $temporaryFiles.AppPgPass
            ProvisioningSqlPath = $temporaryFiles.ProvisioningSql
            ConfiguredPort = $nativePort
            ConfiguredDatabaseName = 'candoitall_test'
            ConfiguredAppUsername = 'candoitall_app'
        }
        Invoke-NativeDatabaseSetup @setup
        $client = @('-X', '-w', '-tA', '-h', '127.0.0.1', '-p', [string]$nativePort, '-U', 'candoitall_app', '-d', 'candoitall_test', '-v', 'ON_ERROR_STOP=1')
        $markerPath = Join-Path $temporaryFiles.Root 'marker.sql'
        Write-Utf8NoBomFile -Path $markerPath -Content "create table preservation_marker (id text primary key); insert into preservation_marker values ('$testId');"
        Invoke-NativePsql -PsqlPath (Join-Path $NativeBinPath 'psql.exe') -PgPassPath $temporaryFiles.AppPgPass -Arguments ($client + @('-f', $markerPath)) | Out-Null
        Invoke-CheckedExternal -FilePath (Join-Path $NativeBinPath 'pg_ctl.exe') -Arguments @('stop', '-D', $nativeDataPath, '-m', 'fast', '-w') | Out-Null
        Invoke-NativeDatabaseSetup @setup
        Write-Utf8NoBomFile -Path $markerPath -Content "select current_setting('server_version_num') || '|' || id from preservation_marker;"
        $result = @(Invoke-NativePsql -PsqlPath (Join-Path $NativeBinPath 'psql.exe') -PgPassPath $temporaryFiles.AppPgPass -Arguments ($client + @('-f', $markerPath)))
        if (($result -join '').Trim() -ne "180006|$testId") { throw 'Native PostgreSQL 18.6 data did not survive restart.' }
        Invoke-CheckedExternal -FilePath (Join-Path $NativeBinPath 'pg_ctl.exe') -Arguments @('stop', '-D', $nativeDataPath, '-m', 'fast', '-w') | Out-Null
        $manifest = [pscustomobject]@{
            host = '127.0.0.1'; port = $nativePort; databaseName = 'candoitall_test'; appUsername = 'candoitall_app'
            native = [pscustomobject]@{ binPath = 'native\pgsql\bin'; dataPath = 'native\data'; logPath = 'native\logs\postgresql.log' }
        }
        Invoke-GeneratedDatabaseStart -Manifest $manifest -Backend 'native' -Root $fixtureRoot
        $result = @(Invoke-NativePsql -PsqlPath (Join-Path $NativeBinPath 'psql.exe') -PgPassPath $temporaryFiles.AppPgPass -Arguments ($client + @('-f', $markerPath)))
        if (($result -join '').Trim() -ne "180006|$testId") { throw 'Generated native launcher did not preserve data.' }
        Write-Output 'Native Windows PostgreSQL 18.6 fresh initialization, repair, SCRAM and generated launcher restart persistence passed.'
    }
}
finally {
    if ($null -ne $nativeDataPath -and (Test-Path (Join-Path $nativeDataPath 'postmaster.pid'))) {
        Invoke-CheckedExternal -FilePath (Join-Path $NativeBinPath 'pg_ctl.exe') -Arguments @('stop', '-D', $nativeDataPath, '-m', 'fast', '-w') | Out-Null
    }
    if ($RunDocker) {
        if ($script:DockerContainerName -notmatch '^candoitall-pg18-test-[a-f0-9]{32}$') { throw 'Unexpected cleanup container.' }
        if (Test-DockerContainerExists -Name $script:DockerContainerName) {
            Invoke-CheckedDocker -Arguments @('rm', '-f', $script:DockerContainerName) | Out-Null
        }
        foreach ($volume in $ownedVolumes) {
            if (-not $volume.StartsWith("candoitall-pg18-test-$testId-data", [StringComparison]::Ordinal)) { throw 'Unexpected cleanup volume.' }
            Invoke-CheckedDocker -Arguments @('volume', 'rm', $volume) | Out-Null
        }
    }
    if ($null -ne $temporaryFiles) { Remove-SafeTemporaryDirectory -Path $temporaryFiles.Root -DatabaseRoot $fixtureRoot }
    $expectedRoot = [IO.Path]::GetFullPath((Join-Path ([IO.Path]::GetTempPath()) "candoitall-pg18-$testId"))
    if ([IO.Path]::GetFullPath($fixtureRoot) -ne $expectedRoot) { throw 'Unexpected cleanup directory.' }
    Assert-DirectoryTreeHasNoReparsePoints -PathValue $fixtureRoot -Description 'Safety fixture cleanup'
    Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
}
