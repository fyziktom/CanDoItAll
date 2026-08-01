# Development PostgreSQL Backup And Restore

The `db-data` volume contains authoritative application data. Use PostgreSQL-native
logical backups while the service is healthy. This guide covers only the repository's
development Compose database. For the separate installed Windows database, use
[Installed Windows Web App](installed-web-app.md#backup-and-restore).

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
