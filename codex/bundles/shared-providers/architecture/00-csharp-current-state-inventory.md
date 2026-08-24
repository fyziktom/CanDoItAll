# C# current-state inventory

Required by the C# architecture bundle guard.

## Role inventory

| Surface | Current owner | Current dependency role | Shared-provider relevance |
| --- | --- | --- | --- |
| Agent provider model | `CanDoItAll.AgentFramework.Models` | inner model/contracts | runtime projection target; must not become wire DTO |
| Provider capability contracts | `CanDoItAll.AgentFramework.Providers` | SDK-neutral capability layer | central adapters may reuse behavior, not HTTP DTOs |
| MAF provider runtime | `CanDoItAll.AgentFramework.Maf` | runtime/SDK integration | existing OpenAI-compatible execution path |
| Workspace provider entity/service | `CanDoItAll.Modules.Workspace` | canonical application/persistence owner | publication/source/import master data |
| Workspace connector registry | `CanDoItAll.Modules.Workspace` | connector manifests and basic execution | shared connector manifest/origin |
| AgentFramework Workspace projection | `CanDoItAll.Modules.AgentFramework` | outer adapter from Workspace to MAF | maps shared connector to effective runtime profile |
| Web API endpoint layer | `CanDoItAll.Web` | HTTP composition and envelopes | native catalog and compatibility routes |
| API token/scopes | Workspace API Access plus Web | auth contracts/policies | catalog/invoke scopes |
| Secret resolution | `CanDoItAll.Modules.Security` | vault/application service | source and upstream credentials |
| EF context/model registry | `CanDoItAll.Infrastructure` | persistence infrastructure | applies module entity configurations |
| PostgreSQL migrations | `CanDoItAll.Migrations.PostgreSql` | migration assembly | publication/source/import/audit migration |
| Provider usage | `CanDoItAll.AgentFramework.Usage` and related models | provider cost/usage projection | relay usage integration; no second ledger |
| Provider management UI | Workspace Razor component | desktop feature UI | publication/source/import workflows |
| SharedInfo OpenAPI | `_candoitall-api-shared` | external contract evidence | final snapshot and skill |

## Current smells relevant to this feature

1. `WorkspaceModels.cs` combines entities, EF configurations, editor models, and a large
   `WorkspaceService` partial surface. New feature types should not be appended there.
2. `WorkspaceAgentProviderProfileMapper` has connector-key switches. The shared connector needs
   one explicit mapping, but future upstream publication adapters must be registry-driven.
3. Ordinary MAF agent creation branches by `ProviderKind`; a naive Shared kind would widen that
   switch across runtime code.
4. Internal provider requests embed complete profiles and binary data.
5. Workspace adapter and MAF runtime paths overlap. Shared behavior must not be implemented
   independently in both.
6. API scopes are centralized and exact, so new endpoint registration must update both names
   and policy composition.
7. Existing usage classification knows Agent and Simple Chat. An external relay must not be
   mislabeled merely to fit the enum.
8. Normal provider profile fields do not express source/import/publication ownership.

## Characterization requirements for SB00

Before extraction or new projects, prove:

- which current production call paths create agents, simple chats, workflow provider calls,
  health checks, and image generation;
- which connector manifests are actually registered in the current branch;
- whether Azure has a Workspace-configurable production profile or only an MAF driver;
- exact OpenAI SDK base-address behavior for Responses, Chat Completions, and Images;
- current secret-resolution scope/lifecycle;
- current usage observation persistence and extension point;
- current provider deletion reference checks;
- current API OpenAPI serialization behavior for streaming and generic JSON;
- current compose image/context constraints.

The target design may be refined only with recorded evidence and without violating the mission.
