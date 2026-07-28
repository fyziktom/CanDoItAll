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

Development and Visual Studio `http`/`https` launch profiles are PostgreSQL-first. They target `127.0.0.1:5432/candoitall_development` with the tracked development credentials and keep development workspace/control-plane files under `%LOCALAPPDATA%\CanDoItAll`. Use `tools/dev/Ensure-DevelopmentPostgres.ps1` to prepare native PostgreSQL, or `docker compose up -d postgres` for the repository-managed database.

The host registers the generic Memory runtime with zero enabled providers and disabled
memory workers by default. Qdrant and the retained native Cognitive Memory module are
not base-host dependencies.

The checked-in API configuration is intended for a trusted local host and leaves bearer authorization disabled. Any remotely reachable deployment must enable `Api:Authorization:Enabled` and supply a secret signing key of at least 32 bytes before exposing Prompt Gallery or other mutation endpoints.

`/api/crm-hr` is a thin HTTP adapter over the CRM-HR application services. It intentionally has no seed route and no direct persistence. Operator automation should use the canonical API skills from the sibling `CanDoItAll.SharedInfo` repository, together with OpenAPI, and retain search-before-create identity handling. JWT scope claims are not endpoint policies in the current host; remotely reachable deployments must not treat a claimed `crmhr.*` scope as authorization enforcement.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture-beta.md`
- Development runtime: `docs/development-runtime.md`
- CRM-HR API: `docs/crm-hr-api.md`
