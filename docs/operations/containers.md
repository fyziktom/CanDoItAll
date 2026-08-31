# Container Operations

`compose.yaml` provides a complete Linux development instance: the Blazor web app and a
private PostgreSQL database. It does not define a production deployment or the installed
Windows web app database. The installed database has its own lifecycle in
[Installed Windows Web App](installed-web-app.md).

The installed app's optional Docker backend is a single installer-managed container and
named volume, not another Compose project. Commands in this guide target development
resources only and must not be used as installed-database repair or removal commands.

## Configuration

Copy the reviewed non-secret defaults:

```powershell
Copy-Item .env.example .env
New-Item -ItemType Directory -Force .secrets | Out-Null
Set-Content -NoNewline .secrets/db-password "replace-for-local-development"
```

`.env` is ignored. The app is published only on the configured loopback address. The
database is reachable only through the internal Compose network. Change
the ignored `.secrets/db-password` value before using a shared host; Compose grants that
file only to the app and database services as a read-only secret.

The image build requires `CanDoItAll.Components` and `CanDoItAll.FileTools` beside this
repository. Compose passes both directories as named build contexts, and the Dockerfile
builds against their direct project references rather than NuGet substitutes.
The named-context copies explicitly exclude `bin` and `obj`, so host build output cannot
mix with Linux-generated Razor markup and scoped CSS. Keep these exclusions even when a
sibling repository has its own `.dockerignore`. The `COPY --exclude` option requires
Dockerfile frontend 1.19 or later; the `docker/dockerfile:1` syntax directive selects the
current stable frontend.

PostgreSQL reads `POSTGRES_PASSWORD_FILE` only while initializing an empty `db-data`
volume. Replacing `.secrets/db-password` does **not** rotate the role password in an
existing database. It can instead make a recreated app use a password that PostgreSQL
does not recognize. For disposable development data, run `docker compose down --volumes`,
replace the ignored file, and create the stack again. To preserve an existing volume,
connect with the current credential, use the interactive `psql` `\password` command so
the new value is not placed in command history, then replace the secret file and recreate
the app service. Never pass either password on a command line or write it to logs.

Validate the resolved model:

```powershell
docker compose --env-file .env.example config --quiet
& .\tools\Validation\Test-Docker.ps1
```

## Lifecycle

```powershell
docker compose up -d --build --wait
docker compose ps --all
docker compose logs app db
docker compose down
```

Open `http://localhost:8080`. The application healthcheck does not pass until PostgreSQL
is ready, EF Core migrations complete, and the runtime reports ready.

Normal shutdown preserves the `app-data` and `db-data` named volumes. Do not add
`--volumes` to routine shutdown commands.

## Manual Shared-Provider Client

The persistent manual client on loopback port 5214 has a separate lifecycle from the
disposable shared-provider E2E harness. Its storage contract is
[`compose.shared-providers.manual.yaml`](../../compose.shared-providers.manual.yaml), an
overlay for `compose.shared-providers.e2e.yaml` that changes only client-A's `/data`
mount. Set `MANUAL_CLIENT_A_DATA_VOLUME` to the verified, migrated Docker named volume.
The volume is external because its authoritative workspace, vault and Data Protection
state must outlive Compose teardown. `nocopy` prevents image contents from initializing
it; migration and ownership validation must finish before the client starts.

This overlay is a storage contract, not a complete reconstruction of the live manual
instance. Do not run `compose up` with the E2E example environment: its ports, image and
application settings differ from the manual client. Use the maintained
[`Set-ManualClientDataVolume.ps1`](../../tools/SharedProviders/Set-ManualClientDataVolume.ps1)
with the verified container name/ID, volume name and private evidence directory. It
preserves the inspected live configuration and targets only client-A; `-Recreate`
retains that volume across container replacement. Publisher 5210, native host 5032,
PostgreSQL and secret mounts stay unchanged. The historical bundle and `.artifacts`
restart helpers are not maintained entry points for the migrated volume.

The operator requires PowerShell 7.5 or later on Windows with Docker Desktop Linux.
It is intentionally bounded to this small manual instance: a 64 MiB archive, 16 MiB per
entry and 10,000 entries. Unsupported data, active runs, overlapping writers and unsafe
permissions fail explicitly; no limit or safety check is bypassed automatically.

Filesystem modification times retain whole-second Docker archive precision; review the
[backup limitations](backup-and-restore.md#manual-shared-provider-client) before migration.

From the repository root, choose the destination volume and inspect the full client ID.
This preflight does not stop containers, create volumes or write evidence:

```powershell
$clientName = 'candoitall-shared-providers-manual-client-a-1'
$clientId = & docker inspect --format '{{.Id}}' $clientName
if ($LASTEXITCODE -ne 0 -or $clientId -notmatch '^[a-f0-9]{64}$') {
    throw 'Could not inspect the exact manual client identity.'
}
$volumeName = Read-Host 'Name for the new persistent manual-client data volume'
$operation = @{
    ContainerName = $clientName
    ExpectedContainerId = $clientId
    VolumeName = $volumeName
    EvidenceDirectory = Join-Path (Get-Location).Path ('.artifacts/manual-client-data-' + [Guid]::NewGuid().ToString('N'))
}
& .\tools\SharedProviders\Set-ManualClientDataVolume.ps1 @operation -WhatIf
```

Review the target, immutable image, volume and run counts before executing the cutover:

```powershell
$migration = & .\tools\SharedProviders\Set-ManualClientDataVolume.ps1 @operation
```

The tool receives the selected name through `-VolumeName`; it does not load an E2E
`.env` file or launch Compose. Keep its private archives and operation report. To verify
persistence immediately afterwards, retain the same volume name, use the returned new
container ID and a fresh evidence directory, then preflight and recreate:

```powershell
$operation.ExpectedContainerId = $migration.CandidateContainerId
$operation.EvidenceDirectory = Join-Path (Get-Location).Path ('.artifacts/manual-client-recreate-' + [Guid]::NewGuid().ToString('N'))
& .\tools\SharedProviders\Set-ManualClientDataVolume.ps1 @operation -Recreate -WhatIf
```

After reviewing that preflight:

```powershell
& .\tools\SharedProviders\Set-ManualClientDataVolume.ps1 @operation -Recreate
```

For a later operation, inspect the current container ID again. Never reuse an old
migration ID or start a retained rollback container alongside the current client.

The E2E harness still uses host directories and its exact mount validation is unchanged.
Never run its `-Reset`, preparation, seeding or teardown operations against the manual
stack or its shared `.artifacts/shared-providers-e2e` directory: they can remove live
publisher data and credentials, or write stale client state. After migration,
`client-a/data` in that directory is a retained snapshot, not the client's active data.
Read current files through the running container or the named volume. See
[manual-client backup and rollback](backup-and-restore.md#manual-shared-provider-client).

## Workstation Web Host

### Blank provider setup

For a new manual-setup client set `AgentFramework__Providers__SeedDefaults=false`.
This skips default provider/credential bootstrap and excludes the generated runtime
Ollama fallback from the canonical provider catalog. Default is `true` for compatibility.
It does not delete existing providers or secrets, nor disable manually configured or
imported shared providers. Use a fresh database and app-data volume for a blank instance.
Keep this setting across restarts; setting it back to `true` enables normal seeding.

### Local browser access with API authorization enabled

The headless container profile disables OS desktop integrations, not interactive browser
features. Simple Chats grants its read/manage/execute permissions to an anonymous local
browser circuit; it does not authenticate HTTP API calls or grant broader API scopes.

Native loopback connections work without extra configuration. Docker may present a
loopback-published browser connection as its NAT gateway. For that deployment, explicitly
configure `WebHost:LocalOperatorUi:TrustedAddresses` with the inspected ingress IP, for
example the environment entry `WebHost__LocalOperatorUi__TrustedAddresses__0`. The default
list is empty. Hostnames, CIDR ranges, wildcards and unspecified addresses fail startup.

Only enable this for an ingress exclusively reachable by trusted local users. Verify
every published app port binds to `127.0.0.1` or `::1`; never trust a gateway that also
forwards anonymous remote clients. This is not a substitute for remote user authentication.
Both the original transport IP and the effective forwarded IP must be loopback or in
the explicit list. A forwarded loopback header alone cannot confer local access.

The two-instance validation helper `Restart-TestInstances.ps1` accepts
`-TrustLoopbackPublishedUi`: it checks loopback bindings and one inspected gateway before
replacing each test app's configuration. Ordinary deployments must configure their
own verified address. Recheck this setting when changing Docker networks or ingress.

### Native workstation process

To run `CanDoItAll.Web` directly on the workstation while keeping PostgreSQL in Compose:

```powershell
Copy-Item compose.override.yaml.example compose.override.yaml
docker compose up -d --wait db
dotnet run --project .\src\App\CanDoItAll.Web\CanDoItAll.Web.csproj
```

The ignored override makes the containerized app opt-in through the `container-app`
profile and publishes PostgreSQL on loopback for the workstation process. Remove the
override before using the full containerized stack again.

## Data Classification

| Volume | Class | Owner | Recovery |
|---|---|---|---|
| `app-data` | Authoritative durable | Workspace files, control-plane state, Data Protection keys, and local secret-vault payloads | Quiesced filesystem backup and tested restore together with the matching database backup |
| `db-data` | Authoritative durable | PostgreSQL application persistence | PostgreSQL-native backup and tested restore |

The Compose project scopes the physical volume name. Use a unique
`COMPOSE_PROJECT_NAME` for concurrent worktrees and disposable validation.

## Resource And Log Bounds

The base model sets configurable memory, CPU, PID, graceful-stop, and local log-rotation
limits. The application image runs as the .NET image's non-root `app` user with a
read-only root filesystem; only `/data` and `/tmp` are writable. Adjust the resource
values in the ignored `.env` file when a development workload needs different bounds.

This image intentionally contains the ASP.NET Core runtime, not a general-purpose
development workstation. Host-integrated desktop launching and interactive terminal
capabilities remain disabled inside the container.
