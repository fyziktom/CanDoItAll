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

## Workstation Web Host

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
