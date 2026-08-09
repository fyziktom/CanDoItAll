# CanDoItAll.Web

## Purpose

Blazor Web App host that composes the runtime, maps development endpoints, loads module assemblies, and serves the local-first UI.

## Project Type

- SDK: `Microsoft.NET.Sdk.Web`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/App/CanDoItAll.Web/CanDoItAll.Web.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Web.csproj](CanDoItAll.Web.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

The web host should orchestrate startup, endpoint mapping, and Blazor rendering. Keep non-trivial product behavior in modules or application services.

Development and Visual Studio `http`/`https` launch profiles are PostgreSQL-first. They
target `127.0.0.1:5432/candoitall_development` with the tracked development credentials
and use purpose-specific platform roots rather than repository-local state. Windows
uses `%LOCALAPPDATA%\CanDoItAll`; Linux separates XDG data, config, state, and runtime
roots; macOS separates Application Support, Logs, and temporary runtime data. See the
development-runtime root matrix for exact defaults and service/container overrides.
Use `tools/dev/Ensure-DevelopmentPostgres.ps1` to prepare native development PostgreSQL,
or `docker compose up -d --wait db` for the repository-managed development database.

Development uses `Auto`, so a first launch does not require an interactive or external
vault. Windows selects current-user DPAPI and reports `Strong`. Unix selects
`LocalUserFile`, enforces `0700` vault directories and `0600` files, and reports
`BasicLocal` with a warning that the same operating-system account can access its key.
Deployments that need stronger Unix isolation should explicitly select Keychain,
Secret Service, or the external-wrapping-key provider; explicit strong providers fail
closed when unavailable. See
[`docs/secure-configuration.md`](../../../docs/secure-configuration.md).

The installed Windows web app does not use that Compose service. Its canonical
`tools/install/Install-CanDoItAllWebApp.ps1` entry point prepares an isolated database,
using either one installer-managed Docker container/volume set or a per-user native
cluster. The generated launcher supplies the exact `Database__Provider` and
`Database__ConnectionString` overrides. See
[`docs/operations/installed-web-app.md`](../../../docs/operations/installed-web-app.md).

The host registers the experimental generic Memory runtime with zero enabled providers
and disabled memory workers by default. Qdrant and native Cognitive Memory are not
base-host dependencies; the native implementation and API belong to its standalone
work-in-progress repository.

The checked-in API configuration is intended for a trusted local host and leaves bearer authorization disabled. Any remotely reachable deployment must enable `Api:Authorization:Enabled` and supply a secret signing key of at least 32 bytes before exposing Prompt Gallery or other mutation endpoints.

`/api/crm-hr` is a thin HTTP adapter over the CRM-HR application services. It intentionally has no seed route and no direct persistence. Operator automation should use the canonical API skills from the sibling `CanDoItAll.SharedInfo` repository, together with OpenAPI, and retain search-before-create identity handling. JWT scope claims are not endpoint policies in the current host; remotely reachable deployments must not treat a claimed `crmhr.*` scope as authorization enforcement.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
- Development runtime: `docs/development-runtime.md`
- CRM-HR API: `docs/crm-hr-api.md`
