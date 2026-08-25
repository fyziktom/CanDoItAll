# Repository evidence captured during preparation

Prepared on 2026-08-24.

## Provider runtime

- `CanDoItAll.AgentFramework.Providers` explicitly owns provider drivers, capabilities,
  descriptors, dispatch, and batching; UI/process code should not call SDKs directly.
- Internal provider request records embed `ProviderProfile` and binary payloads.
- Driver registry is capability/provider-kind oriented.
- MAF runtime gateway registers OpenAI, Azure, Ollama, and ComfyUI drivers.
- Ordinary agent factory still branches directly by `ProviderKind`.

## Workspace and projection

- `Workspace_ProviderProfiles` is the canonical EF table.
- Workspace provider save validates manifest, schema, secret requirement, timeout, pricing,
  and capability fields.
- Commit observers project changes to AgentFramework.
- Workspace-to-AgentFramework mapper resolves connector key, provider kind, transport,
  purpose, models, tags, and secret reference.
- Current mapper has explicit connector switches and runtime Ollama fallback.
- Provider UI is manifest/config-schema driven.
- Existing Workspace and MAF execution paths overlap.

## API

- API uses one `/api` group, optional JWT Bearer auth, exact scopes, structured errors, and
  explicit endpoint metadata.
- OpenAPI is exposed at `/openapi/v1.json` and `/swagger/v1/swagger.json`.
- Memory-provider API demonstrates the current endpoint/service/auth/error pattern.

## Persistence

- `AppDbContext` applies entity configurations from Infrastructure and registered module
  assemblies.
- PostgreSQL migration project owns migrations and snapshot.
- Application-managed concurrency tokens are stamped during SaveChanges.

## Testing/Compose

- repository guidance is focused tests first;
- broad stable graph is a final named gate;
- current Compose builds one Web image and uses PostgreSQL;
- multi-instance proof can reuse one app image with independent data/database state.

## SharedInfo

- bundle preparation/execution/validator and C# architecture skills require boundary maps,
  dependency direction, pattern records, testability, proof tiers, and progression gates;
- `_candoitall-api-shared` owns OpenAPI snapshot/provenance;
- API skills use route appendices and link to the shared snapshot.

## Confidence and revalidation

Confidence is high for the prepared commit. Codex must revalidate current paths and symbols
because the repository has recently undergone physical reorganization and may continue moving.
