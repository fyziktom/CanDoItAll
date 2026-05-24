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

The compose services expose PostgreSQL on `127.0.0.1:5432`, Qdrant HTTP on `localhost:6333`, and Qdrant gRPC on `localhost:6334`. The PostgreSQL service uses database `candoitall_development`, role `candoitall`, and password `candoitall`.

Check container health:

```powershell
docker compose ps
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

Qdrant is configured in `src\CanDoItAll.Web\appsettings.json` at `localhost:6334` with collection `candoitall-knowledge`, vector size `384`, cosine distance, and create-collection-if-missing enabled. It is needed for Cognitive Memory projection and vector recall validation. It is not authoritative storage; PostgreSQL remains the durable AppDbContext profile.

If the local vector index becomes disposable during development, reset only the container-backed Qdrant volume with:

```powershell
docker compose stop qdrant
docker compose rm -f qdrant
docker volume rm candoitall_qdrant_data
docker compose up -d qdrant
```

Use that reset only for local development data. It deletes the Qdrant collection storage.

## Main Runtime Database Status

The main CanDoItAll runtime is PostgreSQL-only. Legacy local profile catalog entries that reference SQLite are rejected with an explicit unsupported-provider message instead of being silently reactivated. Snapshot export and restore are deferred until they can be reintroduced as a portable package workflow outside the main AppDbContext provider contract.
