# Scope Inventory

## Current Repository Scope

| Area | Existing owner and entry points | Planned minimal effect |
|---|---|---|
| Shared relay and pricing | src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders | Frozen pricing/caller evidence, finalizer extraction and canonical outbox projection. |
| Price arithmetic and identity context | src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers | Validated long-count tariff arithmetic and typed optional correlation/ownership fields where required. |
| Shared protocol and import | src/Integration/CanDoItAll.SharedProviders.Abstractions and .Http | Compatible caller snapshot/observed remote request relation only; no required JSON/SSE protocol change. |
| Buffered/streaming LLM | src/MAF/Common/CanDoItAll.AgentFramework.Llm.ProviderRuntime | Per-dispatch typed capture and truthful terminal use. |
| SDK/image/voice/batch | src/MAF/Common/CanDoItAll.AgentFramework.Maf, .Voice, .ProviderPipelines and existing item adapters | Capture actual observed sends; preserve production factory/retry/tool behavior. |
| Chat canonical source | src/MAF/SimpleChats | Own transcript/invocation evidence plus same-transaction intent and attempt links. |
| Agent canonical files | src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage | Small metadata journal/owner adapters; no whole-store redesign. |
| Workflow canonical source | src/Modules/CanDoItAll.Modules.AgentFramework/Persistence | Versioned metadata intent and multi-owner mapping, preserving source observation IDs. |
| Existing usage | src/MAF/Common/CanDoItAll.AgentFramework.Usage and existing projection adapters | No history search through this aggregate API; avoid counting new projections again. |
| Credentials/authorization | src/App/CanDoItAll.Web/Api and existing interactive policy adapters | Map validated managed jti/issuer/subject; enforce new history operations and fences. |
| Provider/Agents UI | src/Modules/CanDoItAll.Modules.AgentFramework/Pages | Shared History panel/controller, two tabs, minimal provider form extraction. |
| General Settings | src/Modules/CanDoItAll.Modules.Workspace/Pages | Workspace-owned policy editor using neutral ports. |
| EF/host/migrations | Infrastructure registry, Composition ModuleAssemblies and existing PostgreSQL migration project | Additive history schema/configuration/worker registration through outer composition. |
| Tests | tests/Unit, tests/Integration, tests/Components, tests/Playwright | Extend relevant fixtures; new history tests only for behavior lacking an existing owner. |

Exact source file links and cases are preserved in
[pricing](../architecture/06-sharing-pricing-analysis.md),
[history/performance](../architecture/07-history-performance-analysis.md) and
[UI](../architecture/08-ui-search-analysis.md). This table is an ownership inventory,
not authorization to edit every listed file.

## Proposed Files And Projects

- Three small projects under `src/MAF/ProviderHistory`: ProviderHistory.Abstractions,
  ProviderHistory.Application and ProviderHistory.Persistence, using the full
  CanDoItAll.AgentFramework namespace/project prefix.
- New top-level typed adapters/collaborators in their existing owners; no new runtime partial.
- ProviderRequestHistoryPanel and ProviderHistoryQueryController in AgentFramework UI;
  ProviderProfileEditorForm wraps editable panes only.
- ProviderHistoryPolicyPanel in Workspace UI, not an AgentFramework component imported backward.
- One additive PostgreSQL migration/model registration change after disposable validation.
- Focused test additions according to the phase's contract and existing natural test home.

Concrete public symbols/references and exact source seams are frozen in SB01 before
implementation. No production file/project/migration has been created by this preparation.

## Dependency And Composition Inventory

[Project graph JSON](03-project-reference-inventory.json) contains the scan method, 104
project / 534 reference totals, missing/cycle checks and selected references. CodeAnalytics
[summary JSON](02-codeanalytics-summary.json) records its narrower 10-project scope and
diagnostic limitations. [Dependency contract](../architecture/02-csharp-dependency-direction.md)
defines the permitted additions and forbidden edges.

## Out Of Scope

All sibling repositories remain unchanged, including Components, SharedInfo, RAG and
SemanticCompletion. No shared standard, provider catalog refresh, token registry mutation,
real model call, package upgrade, user database operation, deployment or root layout change
is authorized in this preparation. Existing bundle locations and documentation conventions
are preserved. No new general-purpose framework, repository layer or audit event bus.
