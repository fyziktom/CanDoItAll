# PostgreSQL Backup And Restore

The `db-data` volume contains authoritative application data. Use PostgreSQL-native
logical backups while the service is healthy.

## Backup

Create an ignored artifact directory, then capture a custom-format dump:

```powershell
New-Item -ItemType Directory -Force .\artifacts\backups | Out-Null
docker compose exec -T db pg_dump -U candoitall -d candoitall_development -Fc > .\artifacts\backups\candoitall.dump
```

Store required backups outside the development workstation with access control,
encryption, retention, and integrity verification appropriate to the data.

## Restore Validation

Restore into a new disposable Compose project and volume:

```powershell
$env:COMPOSE_PROJECT_NAME = "candoitall-restore-check"
docker compose up -d --wait db
Get-Content .\artifacts\backups\candoitall.dump -AsByteStream -Raw | docker compose exec -T db pg_restore -U candoitall -d candoitall_development --clean --if-exists
docker compose exec -T db psql -U candoitall -d candoitall_development -c "select 1"
docker compose down --volumes --remove-orphans
Remove-Item Env:COMPOSE_PROJECT_NAME
```

The `down --volumes` command is appropriate only for the explicitly disposable restore
project. It must not be used against the normal development or production project.

Before a production restore, verify the application version, migration level, backup
integrity, target database identity, rollback point, and expected recovery time.
