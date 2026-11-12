# CanDoItAll.AgentFramework.Persistence

## Purpose

Persistence contracts and file-backed storage support for AgentFramework catalogs, workspaces, executions, receipts, and artifacts.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/MAF/Common/CanDoItAll.AgentFramework.Persistence/CanDoItAll.AgentFramework.Persistence.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.AgentFramework.Persistence.csproj](CanDoItAll.AgentFramework.Persistence.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

Keep AgentFramework model contracts, persistence, provider-neutral orchestration, and provider/runtime adapters separated. Process automation should consume this layer through the AgentFramework module bridge instead of reaching into provider-specific code directly.

Session-only writes preserve a current usage projection without rereading historical runs or usage records. Under the workspace lock, the canonical execution index advances first; the usage projection may then advance only if its version and revision matched the previous index. Missing or stale projections retain the normal canonical rebuild path, including recovery after an interrupted write. `FileSandboxWorkspaceAdmissionReadScalingIntegrationTests` covers precreated sessions, edits, stale projections, interrupted writes and competing store instances.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
