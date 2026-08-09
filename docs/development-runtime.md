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

Runtime state no longer defaults inside the repository. Purpose-specific roots are:

| Purpose | Windows | Linux | macOS |
| --- | --- | --- | --- |
| Workspace | `%LOCALAPPDATA%\CanDoItAll\workspace` | `$XDG_DATA_HOME/candoitall/workspace`, or `~/.local/share/candoitall/workspace` | `~/Library/Application Support/CanDoItAll/workspace` |
| Control plane | `%LOCALAPPDATA%\CanDoItAll\control-plane` | `$XDG_CONFIG_HOME/candoitall/control-plane`, or `~/.config/candoitall/control-plane` | `~/Library/Application Support/CanDoItAll/control-plane` |
| Data Protection keys | control-plane `dataprotection-keys` | `$XDG_DATA_HOME/candoitall/dataprotection-keys` | Application Support `CanDoItAll/dataprotection-keys` |
| State and logs | `%LOCALAPPDATA%\CanDoItAll\{state,logs}` | `$XDG_STATE_HOME/candoitall` and its `logs` child | Application Support `CanDoItAll/state` and `~/Library/Logs/CanDoItAll` |
| Runtime temporary data | `%TEMP%\CanDoItAll\runtime` | `$XDG_RUNTIME_DIR/candoitall`, or `$TMPDIR/candoitall-runtime` | `$TMPDIR/CanDoItAll/runtime` |

Service and container deployments can set all four Linux XDG variables without a
home directory. Application configuration can override roots with
`Storage__WorkspaceRoot`, `ControlPlane__RootPath`,
`ControlPlane__DataProtectionKeysPath`, `ControlPlane__StateRootPath`,
`ControlPlane__LogsRootPath`, and `ControlPlane__RuntimeTemporaryRootPath`.
Configured roots accept `~/...` and `${VARIABLE}/...`; legacy `%VARIABLE%` tokens are
read only at this typed configuration boundary.

Persisted physical paths are bound to an opaque host identity. Containers and
headless services must set a stable `CANDOITALL_HOST_BINDING_ID` value containing
8-128 ASCII letters, digits, hyphens, or underscores. Changing that value safely
makes existing workspace, filesystem-storage, and executable records require an
explicit rebind; it never guesses or executes a foreign path.

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
