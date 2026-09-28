# Developer data preservation: PostgreSQL 16 to 18

The provisioned target is PostgreSQL **18.6**. The installer pins
`postgres:18.6-alpine@sha256:77f585114c32fbca283dc835b0596f4e52b51b4c6662d7810b2f4084f60a1873`.
The verified native archive is `postgresql-18.6-4-windows-x64-binaries.zip`
(382,815,572 bytes; SHA-256
`1DF55002AFE95B945D934C078B13E82C1603FA546731E511D068AA983B4EAD28`).
Keep all installation integrity and ownership checks enabled.

## Who should use this procedure

Use this procedure when updating a CanDoItAll installation created before the PostgreSQL 18 change and preserving its existing data. Give it to your coding agent **before** running the new database installer, changing the database image, or resetting local volumes. The installer does not automatically convert old PostgreSQL data.

This is a one-time developer-operated migration, not a production zero-downtime upgrade system. The default is to preserve data. A developer may deliberately choose a separate fresh-start procedure for an identified disposable instance; lack of valuable-looking records is not consent to discard it.

For the workstation assignment that introduced this change, preservation of the existing application on **port 5032 is mandatory**. The port identifies the web application; discover its database endpoint rather than assuming PostgreSQL listens on the same port.

The PostgreSQL target is the verified 18.x release selected in the maintained scripts. At handoff preparation that release is 18.6. Do not copy older example image pins into the updated installation.

## Source checkout and runtime are separate identities

For the implementation assignment, the owner has already created a branch from `development`. Use the currently checked-out branch; do not switch to a historical review commit or require any specific SHA, branch name or snapshot ancestry. Record the actual source identity only for traceability. Read the current repository instructions and adapt this runbook to any subsequent changes. Routine users of the published runbook likewise use their intended current application version, not this preparation snapshot.

The build you are preparing may differ from the application already serving port 5032. Identify both. Preserve a compatible recovery launch path before allowing a newer host to apply application-schema migrations or seed updates to the source. A new branch or a newer selected database profile does not by itself change the running deployment.

## Identify the deployment and the authoritative data

Find the actual application process, launch mechanism, runtime configuration, active database profile and any runtime overrides. Record the source PostgreSQL server version, endpoint, database name, role, cluster directory/volume, and application/schema version without recording passwords. Distinguish an active profile from a saved selection awaiting restart.

Determine which of these applies:

| Deployment | Points to inspect |
|---|---|
| Source-run application | `tools/App`, launch configuration, environment/user-local settings, active control-plane profile, and any independently installed PostgreSQL service. |
| Development Compose | The exact Compose project, ignored `.env`/override files, service `db`, its database volume, and matching `app-data` volume. The base stack's database is private; host access needs the intended publication/override. |
| Installed Windows application, Docker backend | The installed launcher and `runtime/database/database-manifest.json`; the separately owned container/volume and protected credentials. This is not the development Compose stack. |
| Installed Windows application, native backend | The manifest, native binary/data locations, owning Windows identity, current-user protected credentials, and precise PostgreSQL process/service. |
| Shared-provider manual client | Its own retained database and application volume. Do not treat it as an expendable E2E fixture. Follow its existing recovery guidance as well. |

Inspect configuration privately. Do not print connection strings, complete Docker environments, authentication headers, SQL passwords, vault material, or private keys into terminal transcripts or tracked files. Do not guess credentials or change a role password to make discovery easier. The updated `tools/dev/Ensure-DevelopmentPostgres.ps1` requires an explicit PostgreSQL 18 `-PsqlPath`, rejects a different server major, and retains existing role credentials and database ownership. It provisions a reviewed empty development target; it is not a migration discovery or credential-reset tool.

Verify source and destination identities independently, including effective cluster/server/database and actual backing resources; different profile IDs or host aliases do not prove separate databases. Resolve Docker names to actual resources, inspect ownership/mounts, and check whether any other application shares the database or role. Stop only the resources belonging to this migration. Changing only the install directory does not isolate the installed Docker backend's fixed resource names.

## Establish a recoverable source

Record the current table/schema inventory, populated-table counts, migration history, representative stable IDs and relationships, sequence/identity state, extensions, ownership/grants, and file inventory. Include histories, credentials' references, API access records, provider-sharing records, and unexpected legacy tables. Do not select only the entities visible on a few UI screens. Recheck the baseline after writer quiescence when discovery was done against an active host.

Include newer persisted contracts where present:

| Data | Preservation check |
|---|---|
| Provider configuration and pricing | Preserve image-input and cached-image-input rates, long-context values, custom model/options, null versus zero, tariffs, overrides and frozen historical prices. Preserve source/publication/import identities and vault bindings. |
| Durable execution journals | Preserve legacy v1 and Brotli-compressed v2 tool envelopes, package/type fingerprints, payloads, digests, batch ordering and long-journal content. Do not decode/re-encode through old DTOs or truncate at the former batch limit. |
| Conversations and operations | Preserve IDs, transcript content, operation state and profile identity; drain active work using existing lifecycle semantics rather than bypassing guards. |

Report absent categories as absent. Exercise newly supported formats using separate synthetic validation data, never by creating paid model traffic or historical side effects in this instance. Codec acceptance can also depend on the installed SDK fingerprint; record any pre-existing explicit-recovery state rather than changing safety checks or treating every recovery refusal as data corruption.

Choose a private backup location outside tracked repository content and protect it with the appropriate permissions. Ensure sufficient disk space for the dump, old cluster, new cluster, and application-files backup. Retain the old executable/tool versions and the configuration needed to start the old application if recovery is required.

Quiesce application and background writers **before** the final dump and matching filesystem capture. Prevent the local development supervisor from automatically restarting them during the switch. The source PostgreSQL server can remain running for a logical dump. Capture workspace/content files, control-plane and local runtime state, vault payloads, Data Protection keys and their protection context, and relevant local configuration as one recoverable set. Do not create a database snapshot first and let application files continue changing before the files backup.

Keep backups and private migration evidence out of Git, release artifacts, and issue comments. A successful backup command and a checksum are not enough: prove that the backup restores into the target.

## Preferred route: logical dump and restore into a separate target

Use explicitly resolved PostgreSQL 18 client tools. Inspect their version output and the connected source version separately. A missing PATH entry is usually a tooling problem to solve, not a reason to reconstruct business data manually. A verified native client or isolated client container is acceptable; configure its network access to the existing source rather than exposing the source publicly.

Produce a complete custom-format `pg_dump` archive, with the equivalent of `--format=custom --file=<private-file>`, and check the exit code. Use a file path rather than a binary PowerShell pipeline. For an in-container dump, create the archive in that container and copy the file out with `docker cp`. Inspect its contents with the selected `pg_restore --list` and compute a checksum. Inventory the required global roles separately; do not blindly apply a whole cluster's superuser configuration to a shared server.

Prepare a separate empty PostgreSQL 18 cluster and a distinct verified target database/endpoint. Preserve the old data directory or volume. For Docker 18, use the new image's `/var/lib/postgresql` persistent mount with `/var/lib/postgresql/18/docker` as its data directory. Reusing or moving the old cluster's files is not a major-version conversion. An existing ignored `.env` may still force the old image despite a changed YAML default; inspect the effective configuration.

For native Windows, retain old binaries until recovery has been proved and initialize a different data directory for PostgreSQL 18. Do not invoke the new binaries on the old major's data. For installed Docker, plan the final fixed resource names explicitly; staging names are temporary and must not be misrepresented in the managed manifest.

Create the necessary target roles, database settings, locale/collation and privileges deliberately. Keep the application identity and credential continuity wherever possible. Do not regenerate vault keys or broaden runtime database privileges just to make restore pass. Ensure all required extensions are available. If privileges require an administrative restore followed by ownership adjustments, verify the resulting objects under the actual application role.

Restore the archive into the empty target, with `--exit-on-error` or equivalent checked failure handling. For an empty target there is no reason to blindly copy a destructive `--clean` recipe from another context. Do not start CanDoItAll schema initialization or sample-data seeding in the target before restoring the complete schema and data. Missing roles, rejected statements, and extension failures must be resolved, not ignored.

Compare source-baseline and restored-target data before application startup changes it. Check all populated tables, representative full records, stable IDs and references, migration history, sequences, schema objects, and relevant file checksums. Exact physical files and database dump bytes need not match between major versions; the persisted logical content and identities must. Review collation-dependent text behavior and prompt search, not just object existence. Apply suitable post-restore statistics maintenance.

Managed provider seeds and catalog synchronization can modify configuration after startup. Compare the restored data before these operations and document expected differences afterward; do not replace custom/source-linked profiles with new seed records. Preserve opaque fields and values even when JSON storage formatting differs between equivalent database representations.

Any pending CanDoItAll EF migrations are a separate application-schema operation. Preserve the original recovery set and test their effects explicitly. Do not squash history, fake applied migrations, or substitute fresh sample records for old content.

## Logical backup and restore commands

The implementation was exercised with the pinned PostgreSQL 18.6 Windows clients against
an Alpine 16.13 source and a separate Alpine 18.6 target. Both databases were restored
before application startup; all table row counts/content digests and schema metadata
were reconciled. Native and Docker installer fixtures independently verified clean
initialization, repair and restart. These observations do not substitute for identifying
your own deployment.

For a repeatable logical migration, configure two private libpq service entries
(`candoitall-migration-source` and `candoitall-migration-target`) with the already
verified endpoints, database names and roles. Supply passwords through a protected
password file; keep both service/password files outside Git. The target service must
name the separate, empty database created with the source locale and reviewed roles.
The following PowerShell example uses the verified client directory and private backup
directory supplied by the operator; it neither creates nor deletes either cluster:

```powershell
$pgBin = Read-Host 'Absolute path of the verified PostgreSQL 18 client bin directory'
$backupDirectory = Read-Host 'Absolute path of the private migration backup directory'
$archive = Join-Path $backupDirectory 'application.dump'
if (Test-Path -LiteralPath $archive) {
    throw 'Choose a new archive path; do not overwrite existing recovery material.'
}
& (Join-Path $pgBin 'pg_dump') --version
if ($LASTEXITCODE -ne 0) { throw 'Client validation failed.' }
& (Join-Path $pgBin 'psql') -X -w -At -v ON_ERROR_STOP=1 `
    --dbname 'service=candoitall-migration-source' -c 'show server_version_num;'
if ($LASTEXITCODE -ne 0) { throw 'Source identity check failed.' }
& (Join-Path $pgBin 'psql') -X -w -At -v ON_ERROR_STOP=1 `
    --dbname 'service=candoitall-migration-target' -c 'show server_version_num;'
if ($LASTEXITCODE -ne 0) { throw 'Target identity check failed.' }
& (Join-Path $pgBin 'pg_dump') -w --format=custom --file $archive `
    --dbname 'service=candoitall-migration-source'
if ($LASTEXITCODE -ne 0) { throw 'Backup failed; do not activate the target.' }
Get-FileHash -LiteralPath $archive -Algorithm SHA256
& (Join-Path $pgBin 'pg_restore') -w --exit-on-error --single-transaction `
    --dbname 'service=candoitall-migration-target' $archive
if ($LASTEXITCODE -ne 0) { throw 'Restore failed; leave the target unselected.' }
& (Join-Path $pgBin 'psql') -X -w -v ON_ERROR_STOP=1 `
    --dbname 'service=candoitall-migration-target' -c 'analyze;'
if ($LASTEXITCODE -ne 0) { throw 'Target statistics maintenance failed.' }
```

Check that the client output is the selected 18.x version and the target reports
`180000 <= server_version_num < 190000` before the dump/restore commands. Also capture
`pg_dumpall --globals-only` privately with an authorized source administrator and
restore the required role attributes, memberships and grants deliberately; it includes
password hashes and must never be printed or committed. The application archive alone
does not contain cluster roles, filesystem attachments, vaults or key rings.

For an endpoint-preserving Docker cutover, first reconcile the staging volume and stop
all writers. Stop the old container and disable its automatic restart, retaining its
volume and inspected configuration. Recreate only the staging container against its
verified **18** volume, using the application's intended loopback port and the parent
mount `/var/lib/postgresql`. An already initialized stable container needs no bootstrap
password delivery mount. Record the old and new container/volume identities and final
port explicitly. Never attach the 16 volume to the 18 container. Compose deployments
instead update only their owned image/volume/override selection; an existing ignored
`.env` still overrides YAML defaults. Installed deployments must retain the launcher's
strict fixed names and ownership contract, so archive the old resources under distinct
recovery identities before assigning the final managed names.

## Alternative route: supported CanDoItAll API

Use this route when direct migration cannot be safely completed after reasonable tooling/access remedies. The original host must remain available for export; quiesce business/background mutations without stopping the HTTP interface needed by the transfer.

Discover the running application's API contract and required authorization. The reviewed source provides `/api/access/status` and optional OpenAPI documents at `/openapi/v1.json` and `/swagger/v1/swagger.json`; an older running build may differ or have documentation disabled. Use existing authorized access. Current user-authenticated Swagger may redirect to HTTPS independently of the host-wide redirection switch; direct API routes and the OpenAPI alias can behave differently. Resolve the trusted effective HTTPS/loopback configuration and appropriate operator session/scopes. Setting a redirect port does not create a TLS listener. Do not treat a redirect as DB failure, forward bearer tokens to an unverified redirect destination, disable certificate validation, turn authorization off or add a public SQL execution endpoint.

A service class is not an HTTP endpoint. Inspect mapped routes and request/response schemas before constructing calls. In particular, the workspace-settings endpoint is not database deployment configuration. The existing database-transfer service transfers selected handler groups, not an automatically complete server backup, and records failures per group. Check every result, not only HTTP status.

Before copying, map every populated source category to an available export/import operation and its preservation guarantees. Check stable identifiers, dependency ordering, pagination, histories, attachments, external content, and secret/control-plane continuity. Prefer a supported complete archive operation if available. Do not infer that agent definitions, project exports, or provider-request histories cover the entire installation. The public shared catalog's schema 1.1 uses `CanDoItAll-Catalog-Features: image-pricing` for two optional price fields; without it they are intentionally absent. Even with it, private configuration, history, vault references and administration state are not a complete catalog export. Source/import/publication administration remains an in-process/UI boundary in the reviewed source. Re-discover the actual running implementation rather than guessing a corresponding HTTP route.

Use a distinct PostgreSQL 18 target. Validate source/target profile IDs and effective server/database identity on every mutating operation and apply replacement only to that target. Transfers can partially succeed; inspect each group and establish safe retry semantics before retrying, instead of replaying an entire non-idempotent batch blindly. Read back and reconcile the transferred records and files. Preserve existing guards against unsupported provider-sharing graphs; do not bypass them to get a green result.

If complete preservation cannot be proved with the actual API, retain the source and all backups, leave the partial target unselected, and report exactly which data/operations are missing. A partial transfer is not a successful migration. Do not silently delete unsupported source rows or invent missing API routes.

## Activate and prove the result

Switch the exact configuration mechanism used by the application: the supported active-profile mechanism, launch environment/configuration, or an accurate installed manifest and regenerated launcher. A saved profile alone may require restart to take effect. Keep filesystem state and key material matched to the restored database.

Start the intended application at its original address, including port 5032 for the owner's development instance. Verify the effective application-to-database binding and PostgreSQL version. A server identity probe may include:

```sql
SELECT current_setting('server_version') AS server_version,
       current_setting('server_version_num') AS server_version_num,
       current_database() AS database_name,
       current_user AS database_role,
       inet_server_addr() AS server_address,
       inet_server_port() AS server_port;
```

Run this against the database proven to be used by the host, not just a staging connection with the expected version. Confirm health, existing known IDs/content through authenticated APIs/UI, attachment access, relevant configuration and non-secret proof that the original credentials remain usable. Avoid real paid inference or other external side effects in these smoke tests.

Restart the application and repeat the key checks through new requests and connections. Respect the current profile-lease serialization and stream cancellation behavior; an old open stream cannot prove the target is active. For disposable validation also prove that replacing the target database container preserves its data. Do not use the owner's live database as an integration-test fixture. Investigate unexpected reseeding, missing content, a new empty database, or a return to the old connection; none is a successful migration.

Retain a sanitized migration report with the actual versions, source/target identity, private backup locations and checksums, restored-data reconciliation, restart evidence, and remaining issues. Do not put secret values in that report.

PostgreSQL 18 reports SQLSTATE `23001` (`restrict_violation`) for a foreign key `ON DELETE RESTRICT` rejection, where PostgreSQL 16 reported `23503` (`foreign_key_violation`). [The upstream correction](https://github.com/postgres/postgres/commit/086c84b23) changes the error classification, not the constraint guarantee. Keep orphan-insert assertions on `23503` and RESTRICT-deletion assertions on `23001`; do not weaken the constraint or accept arbitrary integrity errors.

## Recovery and an explicit fresh start

Keep the old cluster/recovery set until the owner intentionally retires it. Prevent old and new applications from writing conflicting copies. After the new target accepts writes, a switch to the old copy would lose those changes. A data rollback must deliberately restore a matching database, files, keys and compatible application version; changing the image tag alone is not a downgrade.

A developer who explicitly elects to discard a different instance's test data should first identify that instance and its exact resources, retain recovery material where appropriate, and initialize a new PostgreSQL 18 database. Prefer leaving old data unselected over deleting it automatically. Do not run global Docker pruning or broad Compose volume removal. Never treat this option as permission to erase the owner's 5032 instance, a retained manual client, or application/vault files outside the chosen disposable scope.

## Primary references

- [PostgreSQL versioning and upgrade policy](https://www.postgresql.org/support/versioning/)
- [PostgreSQL Docker image data layout](https://hub.docker.com/_/postgres)
- [PostgreSQL 18 pg_dump](https://www.postgresql.org/docs/18/app-pgdump.html)
- [PostgreSQL 18 pg_restore](https://www.postgresql.org/docs/18/app-pgrestore.html)
- [PostgreSQL 18 pg_dumpall and global objects](https://www.postgresql.org/docs/18/app-pg-dumpall.html)

Also read the maintained repository guides [installed Windows guide](../../docs/operations/installed-web-app.md) and [backup guide](../../docs/operations/backup-and-restore.md), including the separate retained manual-client recovery section. Quiesce writers before the coherent final database and filesystem capture.
