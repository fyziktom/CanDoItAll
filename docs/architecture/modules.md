# Module Map

Product modules live under [`src/Modules`](../../src/Modules/README.md). A module owns its
pages, navigation contribution, UI orchestration, product-facing services, and any
first-party agent tool provider for that bounded area.

| Module | Responsibility |
|---|---|
| [AgentFramework](../../src/Modules/CanDoItAll.Modules.AgentFramework/README.md) | Agent catalog, provider configuration, governed execution, agent chat sessions, Simple Chats presentation, usage analytics, and capability setup |
| [Collaboration](../../src/Modules/CanDoItAll.Modules.Collaboration/README.md) | Collaboration records and collaboration-facing application surfaces |
| [CRM/HR](../../src/Modules/CanDoItAll.Modules.CrmHr/README.md) | Parties, accounts, opportunities, workforce, recruiting, skills, staffing, and agent/person relationships |
| [Memory](../../src/Modules/CanDoItAll.Modules.Memory/README.md) | Memory provider configuration, operations, diagnostics, and user-facing Memory surfaces |
| [Plugins](../../src/Modules/CanDoItAll.Modules.Plugins/README.md) | Plugin catalog, installation, activation, grants, OAuth, settings, and logs |
| [MAF Simple Chats](../../src/MAF/SimpleChats/CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence/README.md) | Reusable chat definitions, durable dispatch, replayable SSE events, provider-neutral invocation, and shared AgentFramework usage analytics |
| [Processes](../../src/Modules/CanDoItAll.Modules.Processes/README.md) | Process definition, launch, monitoring, recovery, assignments, and process UI |
| [Projects](../../src/Modules/CanDoItAll.Modules.Projects/README.md) | Project portfolio, hierarchy, phases, files, planning, and project-facing services |
| [Prompts](../../src/Modules/CanDoItAll.Modules.Prompts/README.md) | Prompt catalog, versions, assets, and curation surfaces |
| [Resources](../../src/Modules/CanDoItAll.Modules.Resources/README.md) | Reusable workspace resources and resource records |
| [Scheduler Planner](../../src/Modules/CanDoItAll.Modules.SchedulerPlanner/README.md) | Scheduled process/workflow launches, plans, cron projection, and run history |
| [Security](../../src/Modules/CanDoItAll.Modules.Security/README.md) | Security policy services and security-related application contracts |
| [Test Lab](../../src/Modules/CanDoItAll.Modules.TestLab/README.md) | Validation scenarios and product-facing testing workflows |
| [Workbench](../../src/Modules/CanDoItAll.Modules.Workbench/README.md) | Workbench views, canvas state, project structure, and workspace orchestration |
| [Workspace](../../src/Modules/CanDoItAll.Modules.Workspace/README.md) | Workspace settings, data sources, storage, and cross-module workspace state |

## Module Contract

Each module should:

- expose one clear dependency-injection registration entry point
- keep Razor components focused on rendering and orchestration
- place non-trivial behavior in typed services
- consume other domains through contracts rather than their UI or persistence internals
- register navigation and API/tool surfaces explicitly
- validate identifiers, ownership, authorization, and capability scope before mutation
- document its project-local build command and important entry points

Module-to-module references are acceptable only for an intentional product dependency.
Provider, transport, and persistence details remain behind their owning adapter boundary.

## Owner contracts and adapters

The module-decoupling refactor gave each business fact one authoritative writer and one
owner context. The composition root wires the owners together through typed contracts;
a projection or cache carries its source, scope and revision and never becomes a
fallback master. The table lists the owners, the ordinary entry point other modules,
HTTP endpoints, agent tools and UI must use, and the adapter that translates between
owners. [The coverage record](modules-decoupling/COVERAGE.md) keeps the per-owner test
evidence.

| Owner | Authoritative facts | Ordinary entry point | Integration adapter |
|---|---|---|---|
| Agents / Providers (`Modules.AgentFramework`, `src/MAF`) | technical agent definitions, capabilities, provider and model configuration, AI tariffs and usage evidence | `IAgentFrameworkWorkspaceService` through `ICanDoItAllAgentWorkspaceFactory`; `/api/agents` | `AgentFrameworkAiTechnicalAgentBridge` is the only caller of the CRM projection store; the CRM `LegacyAiTechnicalAgentBridge` is a fallback for hosts without the Agents module and is replaced whenever the module is registered |
| CRM/HR (`Modules.CrmHr`, `CrmHrDbContext`) | parties, contacts, affiliations, workforce facts and human rates, staffing and capacity, technical-agent projections | `CrmHrServices`; `/api/crm-hr`; `crm_planning_*` agent tools (privacy-filtered, no rate disclosure) | implements the Projects-owned `IProjectPartyIntegrationBridge`, `IProjectPartyCostRateBridge` and `IProjectWorkAssignmentPartyFacts`; `IAiTechnicalAgentProjectionStore` accepts only the Agents bridge with a profile- and revision-pinned cursor |
| Projects (`Modules.Projects`, `ProjectsDbContext`) | project lifecycle, hierarchy, identity and lifetime admission | `ProjectsService`, `ProjectWriteAdmissionService`; `/api/projects` | owns the bridge contracts with no-op defaults that the owning modules replace; creation receipts and fingerprinted compensation for multi-owner journeys |
| Project Structure / Workbench (`Modules.Workbench`, `WorkbenchDbContext`) | native notes, nodes, placements, links, assets, canonical tasks, dependencies and assignments | `ProjectWorkbenchService`, `ProjectStructureAgentService`; `/api/project-structure`; `project_structure_*` and `project_task_*` tools | `ProjectNodeScopeBridge`, `ProjectNodeDetailsBridge`, `ProjectNodeAssignmentPolicyBridge`, `ProjectWorkItemAssignmentMutationBridge`; plan analytics read the recorded task execution state as the progress truth on every surface |
| Workflows and Processes (`Modules.Processes`, `src/Processes`, `WorkflowDbContext`, `ProcessPersistenceDbContext`) | definitions, admitted executions, checkpoints, approvals, cancellation, recovery and outcomes | `IProcessRuntimeUnitOfWork`, `IProcessPreparedLaunchStore`; `/api/processes`, `/api/workflows` | `AgentFrameworkProcessStepExecutor` uses the Agents hosting API only; the pre-dispatch tool preflight receives inert inventories from owner tool providers |
| Resources / Storage (`Modules.Resources`, `StorageDbContext`) | resource catalog metadata versus bytes, locators, placement and file-operation receipts | `StorageStablePlacementService`; `/storage`, `/api/storage-placement-recovery`; `storage_*` tools | intent states including `Uncertain`; native continuation enlists in the owner transaction |
| Simple Chats (`src/MAF/SimpleChats`, `SimpleChatsDbContext`) | ordinary definitions, conversations and turns | `LlmChatDefinitionApplicationService`; `/api/llm-chats` | `HrSimpleChatRuntimeToolProvider` administers definitions without transcript access or tools |
| Composition (`CanDoItAll.Composition`) | wiring only | `RuntimeHostServiceCollectionExtensions` | the complete `AppDbContext` model exists for migrations, transfer and schema health through `IProfileAppDbContextFactory`; product modules never inject the global context (guarded by `DatabaseCanonicalityArchitectureTests`) |

## Rendering libraries and contracts assemblies

Current rendering boundaries, including Workspace closure R2 and Projects P1, are listed
below. Routed hosts, production adapters, authorization and durable effects remain with
their owners. The boundary records distinguish completed rendering from application acceptance.

| Module | Contracts assembly | Rendering library | Scenario host |
|---|---|---|---|
| CRM / HR | `CanDoItAll.Modules.CrmHr.Contracts` | `CanDoItAll.CrmHr.UI` | `CanDoItAll.CrmHr.UiSandbox` |
| Prompts | `CanDoItAll.Modules.Prompts.Contracts` | `CanDoItAll.Prompts.UI` | `CanDoItAll.Prompts.UiSandbox` |
| Collaboration | `CanDoItAll.Modules.Collaboration.Contracts` | `CanDoItAll.Collaboration.UI` | `CanDoItAll.Collaboration.UiSandbox` |
| TestLab | `CanDoItAll.Modules.TestLab.Contracts` | `CanDoItAll.TestLab.UI` | `CanDoItAll.TestLab.UiSandbox` |
| Plugins | `CanDoItAll.Modules.Plugins.Contracts` | `CanDoItAll.Plugins.UI` | `CanDoItAll.Plugins.UiSandbox` |
| Scheduler Planner | `CanDoItAll.Modules.SchedulerPlanner.Contracts` | `CanDoItAll.SchedulerPlanner.UI` | `CanDoItAll.SchedulerPlanner.UiSandbox` |
| Memory | `CanDoItAll.Modules.Memory.Contracts` | `CanDoItAll.Memory.UI` | `CanDoItAll.Memory.UiSandbox` |
| Resources | `CanDoItAll.Modules.Resources.Contracts` | `CanDoItAll.Resources.UI` | `CanDoItAll.Resources.UiSandbox` |
| Workspace Core | `CanDoItAll.Modules.Workspace.Contracts` | `CanDoItAll.Workspace.UI` | `CanDoItAll.Workspace.UiSandbox` |
| Workspace API Access | `CanDoItAll.Modules.Workspace.ApiAccess.Contracts` | `CanDoItAll.Workspace.ApiAccess.UI` | `CanDoItAll.Workspace.ApiAccess.UiSandbox` |
| Workspace Storage catalog | `CanDoItAll.Modules.Workspace.StorageCatalog.Contracts` | `CanDoItAll.Workspace.StorageCatalog.UI` | `CanDoItAll.Workspace.StorageCatalog.UiSandbox` |
| Workspace Storage selection | `CanDoItAll.Modules.Workspace.StorageSelection.Contracts` | `CanDoItAll.Workspace.StorageSelection.UI` | `CanDoItAll.Workspace.StorageSelection.UiSandbox` |
| Workspace Storage recovery | `CanDoItAll.Modules.Workspace.StorageRecovery.Contracts` | `CanDoItAll.Workspace.StorageRecovery.UI` | `CanDoItAll.Workspace.StorageRecovery.UiSandbox` |
| Workspace Data Sources | `CanDoItAll.Modules.Workspace.DataSources.Contracts` | `CanDoItAll.Workspace.DataSources.UI` | `CanDoItAll.Workspace.DataSources.UiSandbox` |
| Projects P1 | `CanDoItAll.Modules.Projects.Contracts` | `CanDoItAll.Projects.UI` | `CanDoItAll.Projects.UiSandbox` |

The [Configuration renderer](../../src/UI/CanDoItAll.Configuration.UI/README.md) is a neutral
schema loop over SharedKernel types, with Configuration.UiSandbox. Trusted renderer registration
and adaptation remain production composition. The [Workspace census](workspace-closure-map.json)
classifies its remaining wrappers and external render consumers; a retained Razor host is not
automatically unfinished rendering work. See the [Workspace completion record](workspace-completion-ui-boundaries.md).

AgentFramework is partially extracted: its [UI library](../../src/UI/CanDoItAll.AgentFramework.UI/README.md)
and sandbox cover the catalog, capabilities and Overview. The technical Agent detail editor and
other provider/runtime dialogs still contain substantial rendering in the module. Existing
Simple Chats and Workflows UI libraries under `src/MAF` are additional foundations, not evidence
that every Agent or Workflow authoring surface has moved.

`CanDoItAll.Modules.Projects.Contracts` lets renderers and other modules name portfolio/editor
data, project write admissions and assignment queries without referencing the implementation.
The [Projects P1 record](projects-portfolio-ui-p1.md) covers the portfolio, hierarchy inspection,
overview, five-step editor, package presentation and deletion notices. Its typed Files slot
retains the actual Files owners; [Files P2](projects-files-ui-decoupling.md) extracts both Files renderers and their independent sandbox. Contract namespaces and wire fields
remain stable when their assembly changes.

The owners above remain authoritative. Renderers and presentation controllers may own drafts
and read lifetimes; production adapters own durable writes and authorization.
[UI component seams](ui-component-seams.md) describes the seam, and the per-module records
under this directory describe what each slice moved and what it deliberately left behind.

Stable seams for the next UI decoupling are the owner application services and the
Projects-owned bridge contracts above, together with the agent chat context registry
(`IAgentChatContextRegistry`, one active scope per circuit with `IsActive` leases) and the
`IAgentRuntimeToolProvider` composition. Presentation-only coupling that remains in module
Razor pages (Agents pages importing CRM, Security, Prompts and Workspace types) is
intentionally deferred to that work and does not introduce a second writer.

The ordinary multi-turn LLM conversation foundation under `src/MAF/Common` is not globally active. The
LLM Chats persistence adapter constructs it only inside the scoped product engine, paired with canonical
PostgreSQL state and profile-generation fencing. Other products must opt in through their own explicit
composition boundary rather than publishing the generic service globally.

The Simple Chats domain remains under `src/MAF/SimpleChats`; the AgentFramework module owns only its
product presentation adapter, route state, Prompt Gallery action, and shared usage projection. This is
intentional composition, not a move of conversation rules into the Agent module. See
[LLM Chats boundary and integration ownership](llm-chats-boundary-and-handoffs.md).

Workspace Settings Core shares its shell, defaults, Secrets, Files and provider history
renderers through Workspace.Contracts, Workspace.Presentation and Workspace.UI, with a
standalone Workspace.UiSandbox. Data Sources now has its own rendering and presentation leaf;
the module retains its canonical profile, schema, secret, transfer and restart owners. See the
[boundary and validation record](workspace-settings-core-ui-boundary.md).

Workspace API Access has a separate ApiAccess.Contracts / ApiAccess.UI leaf and
Workspace.ApiAccess.UiSandbox. Its status, issuance, token metadata and ordinary account
editors share presentation controllers; the production module retains authorization,
canonical scopes, signing, password and instance-local persistence owners. Core acquires
no API dependency. See the [API boundary record](workspace-api-access-ui-boundary.md).

Storage catalog administration has a separate StorageCatalog.Contracts / StorageCatalog.UI
leaf and Workspace.StorageCatalog.UiSandbox. Its three-step wizard uses independent read
lanes, stable drafts and explicit owner-stage receipts. Production retains credentials,
drivers, routing, profile fences and persistence. Neither Core nor API gains a Storage edge.
Recovery, Storage selection, Data Sources and generic Configuration rendering have completed
their separate boundaries. Agent parent Save and runtime allow-lists still own applied storage
selection; Recovery retains the original interrupted intent and continuation authority. See the
[Storage boundary record](workspace-storage-catalog-ui-boundary.md).

## Remaining rendering roadmap

This is a source-backed planning order, not authorization to start another module during
Workspace closure. Effort is relative architectural complexity (1–5), not elapsed time.
A slice includes the production renderer and host seam, assets, representative independent
sandbox, owner/consumer checks and browser proof. Counts are provisional until a complete
caller, descendant, asset and evaluated-dependency census precedes that assignment.

| Order | Family and current source | Proposed scope | Effort / provisional slices |
|---|---|---|---|
| 1 | [Projects](../../src/Modules/CanDoItAll.Modules.Projects/README.md): P1 / Files P2 complete | Portfolio, hierarchy inspection, overview, editor and both Files renderers are extracted. File authorization, coordinators and content leases retain their original owners; see the P1 and P2 validation records | Completed selected surfaces; actual file owners retained |
| 2 | [AgentFramework](../../src/Modules/CanDoItAll.Modules.AgentFramework/README.md): extracted catalog/capabilities/Overview alongside the remaining detail forms and provider/runtime/dialog renderers | Technical editor and dependent selectors, then provider/history/configuration and residual chat/usage surfaces; preserve permissions and nested lifetimes | 3–4/5; 2–4 slices |
| 3 | [Workflow authoring](../../src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor): substantial canvas/toolbox/inspector markup remains despite the existing light Workflows.UI project | Reuse Workflows.UI; separate catalog/run views, canvas/inspector and settings/admission dialogs while retaining immutable version/input and launch authority | 4/5; 2–4 slices |
| 4 | [Workbench](../../src/Modules/CanDoItAll.Modules.Workbench/README.md): calendar, assignments, native editors and Structure canvas/runtime/file composition remain | Smaller calendar/read panels first; Structure canvas and cross-module runtime/file context last within the family | 5/5; 4–6 slices |
| 5 | [Processes](../../src/Modules/CanDoItAll.Modules.Processes/README.md): thin routes still compose implementation-bound rendering and runtime services | Catalog/read panels, editor/configuration, launch/approval, monitoring/recovery and final integration; preserve SSE, claims, snapshots and receipts | 5/5; 4–6 slices; last major family |

Simple Chats needs a bounded residual-renderer, adapter and scenario audit around its existing
UI library (roughly 2/5), not a replacement domain module. Web Home/dashboard and runtime
capability pages are composition surfaces; assess reusable rendering after the major feature
cuts (2–3/5, roughly 1–2 possible slices). Security has no separate extraction assignment
without an actual remaining screen: Secrets and API administration already belong to Workspace.
Shared AppComponents, Conversations and Configuration retain their generic boundaries.

These estimates inspect current projects, routes and representative remaining markup; they
are not a line-by-line audit of every large module or a completion percentage. CRM/HR, Prompts,
Collaboration, TestLab, Plugins, Scheduler Planner, Memory, Resources and Workspace retain
their completed rendering work and require regression evidence, not new replacement projects.
