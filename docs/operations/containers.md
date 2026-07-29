# Container Operations

`compose.yaml` provides the PostgreSQL dependency used by local development and
integration work. It does not define a production deployment.

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
