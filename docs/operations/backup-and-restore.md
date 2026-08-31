# Development Container Backup And Restore

The `db-data` and `app-data` volumes form the authoritative development state. Use a
PostgreSQL-native logical backup for the database and capture `app-data` only while the
application is stopped so workspace files, control-plane state, Data Protection keys,
and local vault payloads remain consistent. This guide covers only the repository's
development Compose instance. For the separate installed Windows database, use
[Installed Windows Web App](installed-web-app.md#backup-and-restore).

## Provider sharing and history preservation

Sharing identities, publications, imports and source-secret references live in the
database. Preserve their stable IDs together with vault material; copying database rows
without accessible credentials does not restore a usable source. Generic AI-provider
transfer explicitly refuses publication/import references in either database and secret
replacement affecting a target shared source. Do not bypass that guard: a successful
history-only transfer does not establish support for moving the full sharing graph.

[Provider request history](../provider-request-history.md) contains partition-bound
metadata, quota, policy, projection checkpoints and encrypted standalone details.
Canonical agent, Simple Chat and workflow content stays with its owning data/files.
Capture database, application data, Data Protection and vault state consistently.
Database-only restore can leave content or credentials unavailable even when metadata
looks intact. Retention cleanup must preserve retained attempt references and cannot be
used as an automatic substitute for a consistent backup.

The premerge upgrade/preservation checks use disposable databases. Their fixture
creation or cleanup commands must never target a live development or installed profile.

## Backup

Create an ignored artifact directory, then capture a custom-format dump:

```powershell
$backupPath = ".\artifacts\backups\candoitall-development.dump"
$containerDump = "/tmp/candoitall-development-$([guid]::NewGuid().ToString('N')).dump"

try {
    New-Item -ItemType Directory -Force .\artifacts\backups | Out-Null
    & docker compose exec -T db pg_dump `
        -U candoitall `
        -d candoitall_development `
        -Fc `
        -f $containerDump
    if ($LASTEXITCODE -ne 0) {
        throw "Development pg_dump failed with exit code $LASTEXITCODE."
    }

    & docker compose cp "db:$containerDump" $backupPath
    if ($LASTEXITCODE -ne 0) {
        throw "Copying the development backup failed with exit code $LASTEXITCODE."
    }
}
finally {
    & docker compose exec -T db rm -f $containerDump *> $null
    if ($LASTEXITCODE -ne 0) {
        Write-Warning "Could not remove the temporary dump from the development container."
    }
}
```

Store required backups outside the development workstation with access control,
encryption, retention, and integrity verification appropriate to the data.

After creating the PostgreSQL dump, stop the stack without removing volumes and back up
the Compose-scoped `app-data` volume with the workstation's approved volume-backup tool.
Restore it only into a new empty project-scoped volume, restore the matching database
dump into that project's empty database volume, and validate `/health` plus representative
workspace files before cutover. Never restore over the only known-good volumes.

## Restore Validation

Restore into a new disposable Compose project and volume:

```powershell
$backupPath = ".\artifacts\backups\candoitall-development.dump"
$containerDump = "/tmp/candoitall-restore-$([guid]::NewGuid().ToString('N')).dump"
$hadProjectName = Test-Path Env:\COMPOSE_PROJECT_NAME
$previousProjectName = $env:COMPOSE_PROJECT_NAME
$restoreProjectSelected = $false

try {
    $env:COMPOSE_PROJECT_NAME = "candoitall-restore-$([guid]::NewGuid().ToString('N').Substring(0, 12))"
    $restoreProjectSelected = $true

    & docker compose up -d --wait db
    if ($LASTEXITCODE -ne 0) {
        throw "Starting the disposable restore database failed with exit code $LASTEXITCODE."
    }

    & docker compose cp $backupPath "db:$containerDump"
    if ($LASTEXITCODE -ne 0) {
        throw "Copying the backup into the restore container failed with exit code $LASTEXITCODE."
    }

    & docker compose exec -T db pg_restore `
        -U candoitall `
        -d candoitall_development `
        --clean `
        --if-exists `
        --no-owner `
        $containerDump
    if ($LASTEXITCODE -ne 0) {
        throw "Restore validation failed with exit code $LASTEXITCODE."
    }

    & docker compose exec -T db psql `
        -U candoitall `
        -d candoitall_development `
        -c "select 1"
    if ($LASTEXITCODE -ne 0) {
        throw "Post-restore query validation failed with exit code $LASTEXITCODE."
    }
}
finally {
    try {
        if ($restoreProjectSelected) {
            & docker compose exec -T db rm -f $containerDump *> $null
            if ($LASTEXITCODE -ne 0) {
                Write-Warning "Could not remove the temporary dump from the restore container."
            }

            & docker compose down --volumes --remove-orphans
            if ($LASTEXITCODE -ne 0) {
                Write-Warning "Disposable restore-project cleanup failed with exit code $LASTEXITCODE."
            }
        }
    }
    finally {
        if ($hadProjectName) {
            $env:COMPOSE_PROJECT_NAME = $previousProjectName
        }
        else {
            Remove-Item Env:\COMPOSE_PROJECT_NAME -ErrorAction SilentlyContinue
        }

        $previousProjectName = $null
    }
}
```

The `down --volumes` command is appropriate only for the explicitly disposable restore
project. It must not be used against the normal development or production project.

Before a production restore, verify the application version, migration level, backup
integrity, target database identity, rollback point, and expected recovery time.
