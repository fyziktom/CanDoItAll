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

## SB00 decision lock — 2026-08-24

- Workspace `ProviderProfile` plus its EF configuration is the canonical provider master.
  The AgentFramework file catalog is a post-commit compatibility projection; the effective
  integrated runtime snapshot is reloaded from EF.
- The integrated host registers Workspace first and AgentFramework second, so normal
  `IProviderRuntimeGateway` resolution is AgentFramework-backed. `LegacyProviderRuntimeGateway`
  remains a Workspace-only fallback and is not a second implementation target.
- Ordinary agents use `MafProviderAgentFactory`; Simple Chat and workflow LLM nodes use the
  provider-driver runtime; provider health uses `MafProviderRuntimeGateway`; image generation
  uses `ProviderRuntimeImageGenerationService`. Shared imports must project into these existing
  paths rather than create another agent runtime.
- Six Workspace manifests are registered: OpenAI, scenario harness, process mock, ComfyUI,
  local Ollama, and remote Ollama. Azure has no separate Workspace manifest; AgentFramework
  metadata preserves `AzureOpenAi` over the OpenAI connector. OpenAI voice uses a normal Chat
  profile and the voice driver; there is no separate audio manifest/purpose.
- With the pinned OpenAI SDK, the mapped endpoint `https://relay.example.test/custom/v1` produces
  `/custom/v1/chat/completions` and `/custom/v1/responses` for both normal and streaming calls.
  The production image driver produces `/custom/v1/images/generations`. Chat streaming surfaces
  its terminal finish reason; Responses streaming yields the typed text delta and completes after
  the terminal event without exposing a separate typed completion update.
- Current hard deletion is not a reusable reference policy. It can delete the canonical row
  before outer catalog cleanup, and it does not protect Workspace defaults or other provider
  selections. SB02 must introduce an explicit publication/import reference policy.
- Existing provider usage aggregation is the correct extension direction, but the current
  Agent/SimpleChat/Unknown workload values cannot truthfully identify an external relay. SB02
  owns a dedicated relay classification and durable metadata-only invocation record.
- CodeAnalytics snapshots `snap-20260824190346-9451b9e9` and
  `snap-20260824195319-b6470538` contain the same 11 projects, 23 direct references, and no
  project-level cycle. The two pre-existing module cycles and one nested-type cycle are
  classified in SB00 proof and are not widened by this feature.
- The prepared two-project Integration boundary is confirmed. No collapse is justified.

## SB01 realized checkpoint — 2026-08-24

SB01 added the SDK-free `CanDoItAll.SharedProviders.Abstractions` project and the Web-owned
access-context binding. The contract project has no package or project references and contains
no EF, ASP.NET Core, Workspace, Web, MAF, provider-SDK, secret, or internal-profile type. Web is
the only production project that gained a reference in this checkpoint.

The public contract now freezes schema `1.0`, repository-owned route/header constants, strict
catalog JSON, typed failures and transport ports, canonical public revisions, opaque routing
model IDs, and a bounded access-context value/accessor. No persistence, catalog endpoint,
outbound HTTP, inference endpoint, or UI behavior was introduced. SB02 still owns the explicit
publication/import reference policy and truthful relay workload identified by SB00.

## SB02 realized checkpoint — 2026-08-24

Workspace now owns explicit publication, source, import, stable service-identity, and invocation
metadata entities plus pure transition policies and scoped application services. The existing
provider master remains canonical; linked shared profiles are derived caches, not a second source
of truth. Both production provider-delete paths and destructive database transfer now consult the
same typed reference rule, with PostgreSQL `Restrict` as the final authority.

The existing usage direction was extended with appended shared-relay workload/consumer values;
existing numeric values and `Both` semantics remain compatible. SB02 adds no HTTP endpoint,
network client, relay dispatch, connector registration, or UI surface.

## SB06 realized checkpoint — 2026-08-25

Imported profiles now enter the existing provider catalog through a pure Workspace materializer and
the outer AgentFramework mapper. They remain `ProviderKind.OpenAi`; typed credential, network,
feature, origin, and exact model-selection metadata constrain the existing raw driver and MAF SDK
paths. Composition selects hardened public/trusted named clients and adds access context per request,
never as cached-client state.

No second runtime or `ProviderKind.Shared` was introduced. Personal profiles retain their existing
client, model, health, diagnostics, and voice behavior. Source-managed audio is explicitly excluded:
both OpenAI audio operations reject it before credential or HTTP use, voice settings omit it, and a
persisted ineligible voice selection remains empty instead of silently switching to a personal
provider.
