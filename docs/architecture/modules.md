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
