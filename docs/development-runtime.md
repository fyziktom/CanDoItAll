# Development Runtime

The application runs as a .NET 10 Blazor Web App backed by PostgreSQL.

## Database

The canonical Compose file starts a loopback-only PostgreSQL 16 service:

```powershell
Copy-Item .env.example .env
docker compose up -d --wait db
```

The default development connection is:

```text
Host=127.0.0.1;Port=5432;Database=candoitall_development;Username=candoitall;Password=candoitall
```

The credential is for a local loopback-bound development database. Change it before
using a shared host. Native PostgreSQL users can prepare the local role and database with:

```powershell
& .\tools\dev\Ensure-DevelopmentPostgres.ps1
```

## Run The Application

```powershell
dotnet run --project .\src\App\CanDoItAll.Web\CanDoItAll.Web.csproj
```

The default endpoints are:

- `http://localhost:5032`
- `https://localhost:7271`
- `http://localhost:5032/swagger`
- `http://localhost:5032/_dev/runtime`
- `http://localhost:5032/_dev/database/selection`

The local configuration disables API authorization. Keep the application on a trusted
development machine and do not expose it to an untrusted network.

## Local State

The default workspace and control-plane roots are under `%LOCALAPPDATA%\CanDoItAll`.
Build output, test output, browser evidence, MCP state, and repository-local runtime
state are ignored by Git.

## Tailwind

```powershell
npm install --prefix .\Tailwind
npm run tailwind:build
```

For a watch process:

```powershell
& .\tools\dev\Start-TailwindWatch.ps1
```

## Shutdown

Stop the application with `Ctrl+C`. Stop the database without deleting its volume:

```powershell
docker compose down
```

See [container operations](operations/containers.md) for health checks and lifecycle
details.
