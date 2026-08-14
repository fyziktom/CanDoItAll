# Installed Windows Web App

This guide covers the self-contained Windows web app and its dedicated PostgreSQL
runtime. The repository-root `compose.yaml` is only for source-tree development and must
not be used to prepare the installed app.

Use [Installing instances](installing-instances.md#windows) to compare this installer with
direct development, containers, and the framework-dependent Windows host.

For framework-dependent Windows/Linux/macOS headless deployments, use the separate
[Headless Web Host Operations](headless-web-host.md) runbook. The Windows installer in
this guide remains the owner of desktop shortcuts and its dedicated managed database.

## Install

Run the canonical installer from the repository root:

```powershell
& .\tools\install\Install-CanDoItAllWebApp.ps1 -StartAfterInstall
```

The default install root is `%LOCALAPPDATA%\CanDoItAll\WebApp`. The legacy
`tools\Install-CanDoItAllWebApp.ps1` path forwards to the canonical script.

The web installer calls the database installer automatically. Run the database-only entry
point when repairing or validating an existing installation:

```powershell
& .\tools\install\Install-CanDoItAllWebAppDatabase.ps1
```

Use `-WhatIf` on the database script to preview engine selection and target paths without
creating files, downloading archives, or changing Docker resources.

`-SkipDatabaseSetup` on the full web installer is only for an existing managed database.
It validates the current manifest and DPAPI credential before replacing app files; it
cannot be used to create a fresh installation without a database.

## Database Selection

On first setup, the database installer follows this order:

1. Use Docker only when `docker info` proves that a working Linux engine is reachable.
2. Otherwise, on 64-bit Windows, download and install the pinned PostgreSQL binary archive
   from EDB.

The native fallback requires a local, non-UNC install root. Its resolved
`runtime\database` path must be no longer than 120 characters so extraction remains
reliable under Windows PowerShell 5.1 archive path limits. Use a shorter local
`-InstallRoot` when a custom path would exceed that bound.

An existing database manifest keeps its selected engine on an idempotent rerun. This
prevents a later Docker installation or outage from silently moving the app to a different
empty database. Change engines only through an explicit data migration.

Before first backend mutation, setup writes versioned, non-secret JSON to
`database-engine.pending`. It records the selected engine, port, database name, and
application role. If setup is interrupted before the final manifest is committed, the
next run resumes those same values instead of selecting a newly available backend;
explicit conflicting arguments are rejected. The pending state is removed only after
`database-manifest.json` is written successfully.

Both engines expose the same application contract:

| Setting | Default |
|---|---|
| Host | `127.0.0.1` |
| Port | `55432` |
| Database | `candoitall` |
| Application role | `candoitall_app` |
| Provider | `PostgreSql` |

The application role owns the database but is not a PostgreSQL superuser. At launch,
`Start-CanDoItAll.ps1` starts or validates the selected backend, reads the protected
application password, and sets `Database__Provider` and `Database__ConnectionString`
before starting `CanDoItAll.Web.exe`. This explicit override takes precedence over stale
development or previously persisted database profiles. The application applies its EF
Core migrations during startup before `/health` reports `Healthy`.

## Installed State

Runtime-owned database state lives below
`%LOCALAPPDATA%\CanDoItAll\WebApp\runtime\database`:

| Path | Purpose |
|---|---|
| `database-manifest.json` | Non-secret engine and connection metadata |
| `database-engine.pending` | Versioned non-secret recovery state retained only while first setup is incomplete |
| `secrets\app-password.dpapi` | Application password protected for the current Windows user |
| `secrets\admin-password.dpapi` | PostgreSQL administrative password protected for the current Windows user |
| `downloads` | Rebuildable, integrity-checked native PostgreSQL archive cache |
| `native\pgsql` | Replaceable native PostgreSQL binaries and retained notices |
| `native\data` | Authoritative native PostgreSQL cluster data |
| `native\logs` | Native PostgreSQL logs |

Do not copy DPAPI-protected password files to another Windows user or machine and expect
them to decrypt. Do not edit the manifest or data directory by hand. Rerun the database
installer to validate or repair managed state.

### Docker mode

Docker mode is managed directly by the installation script and does not join the
development Compose project. Its steady state is exactly one labeled container and one
labeled named volume; it does not create a second Compose project or reuse the
development `db` service. Repository-root `docker compose` commands cannot start, stop,
repair, or remove this installed resource set.

| Resource | Identity |
|---|---|
| Container | `candoitall-webapp-db` |
| Data volume | `candoitall-webapp-db-data` |
| Readable image tag | `postgres:16.14-alpine` |
| Immutable multi-platform digest | `sha256:57c72fd2a128e416c7fcc499958864df5301e940bca0a56f58fddf30ffc07777` |
| Managed image reference | `postgres:16.14-alpine@sha256:57c72fd2a128e416c7fcc499958864df5301e940bca0a56f58fddf30ffc07777` |
| Published endpoint | `127.0.0.1:55432` |

An existing container is reused or started only when its managed identity, immutable
image reference, writable volume, loopback port, resource limits, restart policy, and
bounded logging configuration all match. Incompatible managed state is rejected instead
of being changed implicitly; the named volume remains authoritative. New initializer and
stable containers use the immutable reference, which Docker fetches when it is not
already present locally. Updating the tag or digest is an explicit installer change to
managed resources, not an automatic image refresh.

The launcher starts a stopped managed container. Docker uses the `local` logging driver
with `max-size=10m` and `max-file=3`, bounding retained container logs to approximately
30 MiB. Inspect the managed resource set with:

```powershell
docker logs candoitall-webapp-db
docker inspect candoitall-webapp-db
```

Never remove `candoitall-webapp-db-data` as part of ordinary stop, reinstall, or repair.

### Native mode

Native mode pins the official EDB Windows x64 PostgreSQL 16.14-2 archive and verifies its
expected byte length and SHA-256 before extraction:

- source catalog: <https://www.enterprisedb.com/download-postgresql-binaries>
- archive: <https://get.enterprisedb.com/postgresql/postgresql-16.14-2-windows-x64-binaries.zip>
- SHA-256: `8A7F54C1968D5D49BDCD3F66B1291F736C74B8CB6A26E9874771FCC7837DBF38`

The full `pgsql` layout and its license/third-party notice files remain together under the
install root. The cluster uses SCRAM authentication and listens only on `127.0.0.1`. The
launcher uses `pg_ctl` and `pg_isready`, so no elevated Windows service is required.

Before starting or restarting PostgreSQL, the installer and launcher rotate
`native\logs\postgresql.log` when it has reached 10 MiB and retains one
`postgresql.log.1` archive. This is start-time rotation, not continuous rotation while a
long-running PostgreSQL process remains up; include the directory in normal disk-usage
monitoring.

The verified ZIP remains under `runtime\database\downloads` as a rebuildable repair
cache. A later repair reuses it only when both the exact byte length and SHA-256 still
match. It is not authoritative data or a database backup; with the app and installer
stopped, it may be deleted to reclaim space and will be downloaded again if binary repair
is later required.

The raw archive depends on the supported Microsoft Visual C++ x64 runtime. Database setup
validates `postgres.exe` after extraction and reports the official runtime prerequisite if
Windows cannot load it.

## Backup And Restore

The installed database is authoritative. Back it up before application upgrades, database
repairs, or engine migration. Keep backups outside the install root and test restoration
into a disposable database.

For Docker mode, decrypt the application credential in memory, pass a temporary pgpass
entry over standard input, create the custom-format dump inside the container, and then
copy it out:

```powershell
$databaseRoot = Join-Path $env:LOCALAPPDATA "CanDoItAll\WebApp\runtime\database"
$manifest = Get-Content (Join-Path $databaseRoot "database-manifest.json") -Raw | ConvertFrom-Json
$securePassword = Get-Content (Join-Path $databaseRoot $manifest.appPasswordFile) -Raw | ConvertTo-SecureString
$credential = [pscredential]::new($manifest.appUsername, $securePassword)
$plainPassword = $null
$pgPassLine = $null
$containerDump = "/tmp/candoitall-$([guid]::NewGuid().ToString('N')).dump"
$containerPgPass = "/tmp/candoitall-$([guid]::NewGuid().ToString('N')).pgpass"

try {
    $plainPassword = $credential.GetNetworkCredential().Password
    $pgPassLine = "127.0.0.1:5432:$($manifest.databaseName):$($manifest.appUsername):$plainPassword"
    New-Item -ItemType Directory -Force .\artifacts\backups | Out-Null

    $pgPassLine | & docker exec --interactive `
        --env "CDA_DB_USER=$($manifest.appUsername)" `
        --env "CDA_DB_NAME=$($manifest.databaseName)" `
        --env "CDA_DUMP_PATH=$containerDump" `
        --env "CDA_PGPASS_PATH=$containerPgPass" `
        candoitall-webapp-db `
        sh -ec 'umask 077; trap "rm -f \"$CDA_PGPASS_PATH\"" EXIT; tr -d "\r" > "$CDA_PGPASS_PATH"; PGPASSFILE="$CDA_PGPASS_PATH" pg_dump -w -h 127.0.0.1 -p 5432 -U "$CDA_DB_USER" -d "$CDA_DB_NAME" -Fc -f "$CDA_DUMP_PATH"'
    if ($LASTEXITCODE -ne 0) {
        throw "Docker pg_dump failed with exit code $LASTEXITCODE."
    }

    & docker cp "candoitall-webapp-db:$containerDump" .\artifacts\backups\candoitall.dump
    if ($LASTEXITCODE -ne 0) {
        throw "Copying the Docker database backup failed with exit code $LASTEXITCODE."
    }
}
finally {
    $pgPassLine = $null
    $plainPassword = $null
    $credential = $null
    $securePassword = $null

    & docker exec candoitall-webapp-db rm -f $containerDump $containerPgPass *> $null
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Could not remove one or more temporary backup files from the database container."
    }
}
```

For native mode, read the manifest for the binary path and use the DPAPI-protected
application credential without printing it:

```powershell
$databaseRoot = Join-Path $env:LOCALAPPDATA "CanDoItAll\WebApp\runtime\database"
$manifest = Get-Content (Join-Path $databaseRoot "database-manifest.json") -Raw | ConvertFrom-Json
$securePassword = Get-Content (Join-Path $databaseRoot $manifest.appPasswordFile) -Raw | ConvertTo-SecureString
$credential = [pscredential]::new($manifest.appUsername, $securePassword)
$pgDump = Join-Path $databaseRoot (Join-Path $manifest.native.binPath "pg_dump.exe")
$hadPgPassword = Test-Path Env:\PGPASSWORD
$previousPgPassword = $env:PGPASSWORD

try {
    New-Item -ItemType Directory -Force .\artifacts\backups | Out-Null
    $env:PGPASSWORD = $credential.GetNetworkCredential().Password
    & $pgDump -w -h $manifest.host -p $manifest.port -U $manifest.appUsername -d $manifest.databaseName -Fc -f .\artifacts\backups\candoitall.dump
    if ($LASTEXITCODE -ne 0) {
        throw "Native pg_dump failed with exit code $LASTEXITCODE."
    }
}
finally {
    if ($hadPgPassword) {
        $env:PGPASSWORD = $previousPgPassword
    }
    else {
        Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
    }

    $previousPgPassword = $null
    $credential = $null
    $securePassword = $null
}
```

Use `pg_restore --clean --if-exists --no-owner` against an empty, validated recovery
target. Do not overwrite the only known-good Docker volume or native data directory during
restore.

## Development Database

Development remains separate:

```powershell
Copy-Item .env.example .env
docker compose up -d --wait db
docker compose down
```

Those commands manage `candoitall_development` and development credentials. They do not
install, repair, stop, or remove the installed web app database.
