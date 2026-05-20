# Development Runtime

The default local runtime is PostgreSQL-first. Visual Studio `http` and `https` launch profiles, plus `appsettings.Development.json`, point at:

```text
Host=127.0.0.1;Port=5432;Database=candoitall_development;Username=candoitall;Password=candoitall;Include Error Detail=true
```

Development workspace and control-plane files resolve to `%LOCALAPPDATA%\CanDoItAll\workspace` and `%LOCALAPPDATA%\CanDoItAll\control-plane`. They should not depend on repo `.artifacts` folders.

## Prepare PostgreSQL

For the repo-managed containers on a clean machine:

```powershell
docker compose up -d postgres qdrant
```

For a native PostgreSQL service:

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\dev\Ensure-DevelopmentPostgres.ps1
```

The script creates or updates the `candoitall` role and ensures the `candoitall_development` database exists. If your local PostgreSQL admin login is not `postgres/postgres`, pass `-AdminUsername`, `-AdminPassword`, `-AdminHost`, or `-AdminPort`.

## Run From Visual Studio

Start `CanDoItAll.Web` with the default `http` or `https` profile. Startup applies PostgreSQL EF migrations and initializes the runtime schemas. The first launch against an empty database can take a few minutes while migrations and seed data are applied. Confirm startup with:

```text
http://localhost:5032/_dev/runtime
http://localhost:5032/_dev/database/selection
```

The database selection endpoint should report provider `PostgreSql` and database `candoitall_development`.

## Qdrant

Qdrant is configured in `appsettings.json` at `localhost:6334` with collection `candoitall-knowledge`. It is needed for Cognitive Memory projection and vector recall validation. It is not authoritative storage; PostgreSQL remains the durable AppDbContext profile.

## SQLite Status

SQLite support still exists in code, migrations, and some tests, but it is not the default development runtime. It is probably going to be removed after more analysis because process automation is too slow on SQLite for realistic governed runs.
