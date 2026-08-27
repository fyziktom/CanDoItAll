$ErrorActionPreference = 'Stop'
$composePath = 'C:\repositories\CanDoItAll\codex\bundles\shared-provider-real-catalogs\subbundles\04-avatar-and-fresh-client\compose.yaml'
$container = (docker inspect candoitall-spui-fresh-app-1 | ConvertFrom-Json)[0]
if ($LASTEXITCODE -ne 0 -or $container.Config.User -ne '1654:1654') {
    throw 'The isolated 5214 container is unavailable or has unexpected ownership.'
}
$port = $container.HostConfig.PortBindings.'8080/tcp'
$dataMount = @($container.Mounts | Where-Object Destination -eq '/data')
if ($port.Count -ne 1 -or $port[0].HostIp -ne '127.0.0.1' -or $port[0].HostPort -ne '5214' -or
    $dataMount.Count -ne 1 -or $dataMount[0].Name -ne 'candoitall-spui-fresh_app-data') {
    throw 'Refusing to reset an unexpected port or data volume.'
}
if (-not ($container.Config.Env | Where-Object { $_ -match '^Database__ConnectionString=.*Database=candoitall_e2e_fresh_client;' })) {
    throw 'The 5214 database target does not match the explicit fresh-client database.'
}
$owner = docker exec candoitall-spui-db psql -U candoitall_e2e_admin -d postgres -Atc "SELECT pg_get_userbyid(datdba) FROM pg_database WHERE datname='candoitall_e2e_fresh_client';"
if ($LASTEXITCODE -ne 0 -or $owner -ne 'candoitall_e2e_fresh_client') {
    throw 'The fresh-client database owner is unexpected.'
}
$backupCount = docker exec candoitall-spui-db psql -U candoitall_e2e_admin -d postgres -Atc "SELECT count(*) FROM pg_database WHERE datname='candoitall_e2e_fresh_before_admin_20260827';"
if ($LASTEXITCODE -ne 0 -or $backupCount -ne '0') {
    throw 'The recovery database name is already present; do not overwrite it.'
}
$volumes = docker volume ls --format '{{.Name}}'
if ($LASTEXITCODE -ne 0 -or $volumes -contains 'candoitall-spui-fresh_app-data-reset-20260827') {
    throw 'The replacement data volume already exists; refusing to reuse it as fresh.'
}
docker compose -f $composePath stop app
if ($LASTEXITCODE -ne 0) {
    throw 'Could not stop 5214.'
}
$connections = docker exec candoitall-spui-db psql -U candoitall_e2e_admin -d postgres -Atc "SELECT count(*) FROM pg_stat_activity WHERE datname='candoitall_e2e_fresh_client';"
if ($LASTEXITCODE -ne 0 -or $connections -ne '0') {
    throw 'Fresh-client database still has active connections; no database was renamed.'
}
docker exec candoitall-spui-db psql -v ON_ERROR_STOP=1 -U candoitall_e2e_admin -d postgres -c 'ALTER DATABASE candoitall_e2e_fresh_client RENAME TO candoitall_e2e_fresh_before_admin_20260827;'
if ($LASTEXITCODE -ne 0) {
    throw 'Could not retain the recovery database.'
}
docker exec candoitall-spui-db psql -v ON_ERROR_STOP=1 -U candoitall_e2e_admin -d postgres -c 'CREATE DATABASE candoitall_e2e_fresh_client OWNER candoitall_e2e_fresh_client;'
if ($LASTEXITCODE -ne 0) {
    throw 'Clean database creation failed; original data remains in the recovery database.'
}
docker exec candoitall-spui-db psql -v ON_ERROR_STOP=1 -U candoitall_e2e_admin -d postgres -c 'REVOKE ALL ON DATABASE candoitall_e2e_fresh_client FROM PUBLIC;'
if ($LASTEXITCODE -ne 0) {
    throw 'Could not restrict the replacement database to its owner.'
}
docker compose -f $composePath up -d
if ($LASTEXITCODE -ne 0) {
    throw 'Fresh startup failed; recovery database and original volume are retained.'
}
Write-Output '5214 reset: original database retained as candoitall_e2e_fresh_before_admin_20260827.'
Write-Output 'Original volume retained as candoitall-spui-fresh_app-data. New volume: candoitall-spui-fresh_app-data-reset-20260827.'
Write-Output '5210 and 5212 databases and volumes were not changed.'
