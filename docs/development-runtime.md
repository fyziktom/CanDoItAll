# Development Runtime

The supported application runtime uses PostgreSQL. The default `http` and `https` launch profiles and `appsettings.Development.json` use:

```text
Host=127.0.0.1;Port=5432;Database=candoitall_development;Username=candoitall;Password=candoitall;Include Error Detail=true
```

The InMemory database driver exists for tests only. SQLite profiles are retired and are rejected instead of being silently reactivated.

## Prepare PostgreSQL

Start the repository-managed service from the repository root:

```powershell
docker compose up -d postgres
docker compose ps postgres
```

The service publishes host port `5432` and uses the database, role, and development password shown above. The checked-in Compose mapping is not restricted to loopback; use it only on a trusted development host and restrict the binding before joining an untrusted network.

For a native PostgreSQL service:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\dev\Ensure-DevelopmentPostgres.ps1
```

The script ensures the `candoitall` role and `candoitall_development` database exist. Supply its `-AdminUsername`, `-AdminPassword`, `-AdminHost`, or `-AdminPort` parameters when the local administrator connection differs from the script defaults.

The checked-in credentials are for local development only. Do not expose these credentials or the checked-in port bindings. Use secret-backed configuration and enable API authorization before running in any shared environment.

## Run The Host

```powershell
dotnet run --project .\src\App\CanDoItAll.Web\CanDoItAll.Web.csproj
```

Open `http://localhost:5032`. Startup applies the PostgreSQL migrations and initializes runtime data. A first launch against an empty database can take longer while migrations and seed data run.

Confirm readiness at:

```text
http://localhost:5032/_dev/runtime
http://localhost:5032/_dev/database/selection
```

The database-selection endpoint should report provider `PostgreSql` and database `candoitall_development`.

## Local State

The default development roots are:

```text
%LOCALAPPDATA%\CanDoItAll\workspace
%LOCALAPPDATA%\CanDoItAll\control-plane
```

Override them with `Storage__WorkspaceRoot` and `ControlPlane__RootPath`. Keep machine-specific state, credentials, and generated artifacts outside Git.

An explicit connection override uses the normal .NET configuration keys:

```powershell
$env:Database__Provider = "PostgreSql"
$env:Database__ConnectionString = "<secret-backed PostgreSQL connection string>"
dotnet run --project .\src\App\CanDoItAll.Web\CanDoItAll.Web.csproj
```

Do not commit the connection string or include it in diagnostic output.

## Memory Defaults

The base host composes the provider-neutral Memory subsystem. These provider drivers can be registered through configuration but are disabled by default:

- deterministic mock
- HTTP
- native remote
- MCP

Memory background workers are also disabled by default. Enabling a provider or worker is an explicit environment-specific decision; a missing provider must fail predictably rather than falling back to another provider.

The experimental `/api/memory-providers` surface exposes only provider-neutral profile,
query, and owned-status operations. The main host does not map `/api/cognitive-memory`;
native service operations belong to the standalone Cognitive Memory repository.

## Qdrant

Qdrant is not configured or required by the base host. The `qdrant` Compose service remains only for optional external-provider or legacy integration work:

```powershell
docker compose up -d qdrant
```

Starting that service alone does not enable a Memory provider. Follow the owning provider repository's configuration and data-lifecycle documentation. PostgreSQL remains the authoritative application database.

## Troubleshooting

- If startup cannot connect, run `docker compose ps postgres` and inspect `docker compose logs postgres`.
- If the selection endpoint reports anything other than PostgreSQL, check launch-profile and environment overrides.
- If a retired SQLite profile exists in local control-plane data, replace it with a PostgreSQL profile; there is no SQLite fallback.
- If Memory tools are absent, verify that the intended provider and its authorization policy are explicitly enabled.
- If a clean checkout behaves differently from an existing machine, compare environment variables and the two `%LOCALAPPDATA%` roots before changing repository configuration.
