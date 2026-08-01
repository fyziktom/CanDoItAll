# Container Operations

`compose.yaml` provides the PostgreSQL dependency used by local development and
integration work. It does not define a production deployment or the installed Windows
web app database. The installed database has its own lifecycle in
[Installed Windows Web App](installed-web-app.md).

The installed app's optional Docker backend is a single installer-managed container and
named volume, not another Compose project. Commands in this guide target development
resources only and must not be used as installed-database repair or removal commands.

## Configuration

Copy the reviewed non-secret defaults:

```powershell
Copy-Item .env.example .env
```

`.env` is ignored. The service publishes PostgreSQL only on the configured loopback
address. Change `POSTGRES_PASSWORD` before using a shared host.

Validate the resolved model:

```powershell
docker compose --env-file .env.example config --quiet
& .\tools\Validation\Test-Docker.ps1
```

## Lifecycle

```powershell
docker compose up -d --wait db
docker compose ps --all
docker compose logs db
docker compose down
```

Normal shutdown preserves the `db-data` named volume. Do not add `--volumes` to routine
shutdown commands.

## Data Classification

| Volume | Class | Owner | Recovery |
|---|---|---|---|
| `db-data` | Authoritative durable | PostgreSQL application persistence | PostgreSQL-native backup and tested restore |

The Compose project scopes the physical volume name. Use a unique
`COMPOSE_PROJECT_NAME` for concurrent worktrees and disposable validation.

## Resource And Log Bounds

The base model sets configurable memory, CPU, PID, graceful-stop, and local log-rotation
limits. Adjust the values in the ignored `.env` file when a development workload needs
different bounds.
