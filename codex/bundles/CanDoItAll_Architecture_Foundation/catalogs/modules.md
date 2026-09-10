# Module and boundary catalog

All 15 product modules and nine logical/infrastructure boundaries are retained. Domain ownership differs from current assembly placement.

## MOD-AGENTS

**Title:** Agents: definitions, capabilities and governed use

**Repository folder:** src/Modules/CanDoItAll.Modules.AgentFramework

**Primary source ids:** SRC-009; SRC-003; MAP-05; MAP-06

**Target owner:** Agents

**Target scope:** Technical agent identity, definitions and templates, capabilities, status and configuration; agent execution and governance policy. Provider prices belong to Agents/Providers and technical AI workflows to Agents/Workflows. The current module folder also includes integration and presentation; an assembly name does not define a domain.

**Provides:** Agent discovery and details as safe summaries; technical create/update/archive commands.; Governed start, observation, cancellation and approval continuation; execution-source registration.; Owned capabilities and agent-launch ports supporting curators of other domains.; Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

**Consumes:** Providers: runtime profiles, capability compatibility, quotes and usage.; Security: purpose-limited secret references and resolution.; Owner-published tools for Structure, Prompts, CRM, Memory, Scheduler and Plugins.; Conversation shell: presentation and input intent, not execution ownership.; Explicitly granted target-owner query/command APIs through registered adapters; runtime actor/purpose never grants blanket cross-module access (CON-057 through CON-076).

**Forbidden:** Do not write Party, AiResourceBinding or tasks directly through EF.; Do not move all product DTOs into generic MAF Core.; Do not store CRM profiles in agent ConfigurationJson or turn Simple Chats into agents.

**Persistence:** Agent-owned definitions and execution state remain in their canonical stores. Preserve profile fencing, concurrency-token stamping, committed-with-warning outcomes and history when splitting persistence. Do not indiscriminately move every runtime cache into the database or the reverse.

**Future:** REQUIRED FOR THE BOUNDARY: separate technical catalog and execution APIs from UI and CRM writers.; ESTIMATE: more precise resource-capacity/admission views and workload cost quotes; not authorization to implement new functions.; UNPROVEN: complete human-in-the-loop coverage across every workflow/backend; preserve existing approvals.

**Feature ids:** FEAT-001; FEAT-002; FEAT-003; FEAT-004; FEAT-005; FEAT-006; FEAT-007; FEAT-008; FEAT-009; FEAT-110; FEAT-120; FEAT-122

## MOD-PROVIDERS

**Title:** Agents / Providers: administration, pricing, sharing and usage

**Repository folder:** src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement

**Primary source ids:** SRC-022; SRC-007; MAP-11

**Target owner:** Agents / Providers

**Target scope:** Authority over provider profiles, publications, import bindings, model offers and tariffs. Transport protocols belong to adapters; provider execution belongs to the respective runtime. Historical invocation valuation is immutable evidence, not a live tariff copy.

**Provides:** Provider administration and query contracts; model, capability and health catalogs without secrets.; Cost quotes and versioned tariff summaries for Agents, CRM and Work Management.; Publication, import and synchronization using provider-neutral contracts; immutable usage evidence.; Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

**Consumes:** Security for secret lifecycle.; Storage, transport and network policy for external communication.; Execution runtimes for health checks, probes, invocations and evidence.; Workspace preferences reference opaque provider IDs rather than editable copies.

**Forbidden:** CRM must not maintain a second AI price list.; Workspace must not own a profile merely because its table is named Workspace_ProviderProfiles.; Caches, logs, quotes and UI must not expose resolved secrets.

**Persistence:** Preserve identities, imported publication/source bindings, tariff snapshots and request history. Moving the writer does not require cosmetic table renames. Distinguish relay and local usage to avoid charging for the same invocation twice.

**Future:** REQUIRED FOR THE BOUNDARY: one price-calculation owner and explicit unknown, free, estimated and actual states.; ESTIMATE: planning quotes for composite agents or workflows with uncertainty and limits.; ESTIMATE: scope-specific quotas and budgets; do not introduce a speculative billing aggregate.

**Feature ids:** FEAT-010; FEAT-011; FEAT-012; FEAT-013; FEAT-014; FEAT-015

## MOD-CRM

**Title:** CRM / HR: parties and human/AI resource planning

**Repository folder:** src/Modules/CanDoItAll.Modules.CrmHr

**Primary source ids:** SRC-010; MAP-08

**Target owner:** CRM / HR

**Target scope:** Personnel and commercial identities, contacts, relationships, recruitment, owned resource profiles, governance and capacity records. Technical agents and AI tariffs have external authorities. Project-level participation and WorkItem assignment must not collapse into one anonymous link.

**Provides:** Party/resource discovery and personnel detail according to scope and privacy policy.; Owner CRM commands and HR lifecycle; CRM owns synchronization of AI bindings.; Capacity and staffing/reservation contracts where functionally proven or delivered under separate scope.; Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.; Existing HR-wired safe search, party creation, and affiliation operations are preservation requirements; broader staffing editing is a separately scoped extension.

**Consumes:** Agents: safe technical catalog and lifecycle events.; Providers: quote and tariff summaries.; Projects: existence and scope; Work Management: workload and assignment commands.; Memory and Collaboration through owned source gateways and references, not foreign writes.

**Forbidden:** Do not own a second agent definition or AI price list.; Do not send confidential notes to a generic agent picklist.; Do not repair assignments by writing Workbench rows; use the single owner command.

**Persistence:** CRM owns Party, AiResourceBinding and personnel/commercial aggregates. Moving work-item assignment authority to Work Management is an explicit data change requiring reconciliation. Project participation remains a separate CRM/Projects integration with one designated writer.

**Future:** REQUIRED FOR THE BOUNDARY: separate technical agent projections from CRM governance field by field.; REQUIRED FOR THE BOUNDARY: preserve multiple roles, primary assignments, time intervals and cardinality when migrating assignments.; ESTIMATE: fully enforced capacity reservations and concurrent planning; a CapacityBlock alone does not prove a complete reservation engine.

**Feature ids:** FEAT-016; FEAT-017; FEAT-018; FEAT-019; FEAT-020; FEAT-021; FEAT-022; FEAT-023; FEAT-024; FEAT-111; FEAT-112; FEAT-113; FEAT-121

## MOD-PROJECTS

**Title:** Projects: portfolio and lifecycle

**Repository folder:** src/Modules/CanDoItAll.Modules.Projects

**Primary source ids:** SRC-011; MAP-01

**Target owner:** Projects

**Target scope:** Projects, phases, lifecycle, portfolio and project hierarchy. A project is not the entire Project Structure graph. Projects coordinates its lifecycle and invites owner participants; it must not delete their tables itself.

**Provides:** Project catalog, authorized existence/scope queries, phases and lifecycle commands.; Project hierarchy and its changes; projection contributions to Structure.; Export, deletion and transfer lifecycle coordination with owner participants.; Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

**Consumes:** Structure/Work Management: structural content, tasks and workload summaries.; CRM: party summaries and project participation.; Storage/FileTools: file portfolios under authorized scopes.

**Forbidden:** Do not own technical runtime statuses or claim foreign files by path.; Do not replace bridges with a direct Projects-to-Workbench.UI reference.; Do not introduce a second ProjectId for the graph.

**Persistence:** The Projects runtime context maps owned project tables. Decide cross-domain foreign keys and lifecycle transactions explicitly. Deletion retains durable coordination and a blocked/deleting state preventing new writes.

**Future:** REQUIRED FOR THE BOUNDARY: a clean project query contract instead of every consumer listing all projects through concrete ProjectsService.; ESTIMATE: portfolio baselines and economic summaries; historical planning snapshots are not live price lists.

**Feature ids:** FEAT-025; FEAT-026; FEAT-027; FEAT-028; FEAT-029; FEAT-030; FEAT-031

## MOD-STRUCTURE

**Title:** Project Structure and Workbench: native graph plus projections

**Repository folder:** src/Modules/CanDoItAll.Modules.Workbench

**Primary source ids:** SRC-008; SRC-004; SRC-005; SRC-025; SRC-026; MAP-02; MAP-03; MAP-04

**Target owner:** Project Structure; Work Management; Workbench shell (separate responsibilities)

**Target scope:** Structure owns native notes/nodes, structural relationships, placements and bindings. Work Management within this area owns tasks, plans and assignments. Agent, project-hierarchy and execution projections do not create new source authorities. The shell owns presentation only.

**Provides:** Authorized Structure reads, native node/link commands and typed safe contribution protocol.; Task/Gantt/planning commands through Work Management; versioned asset binding and content coordination.; Invocation context, file scopes, a projection extension contract and execution-result attachment.; Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

**Consumes:** Projects existence/hierarchy; Agents identity; CRM resource facts; Processes/Workflows definitions and execution records.; Provider quotes and image generation through Agents; Security secret references; Storage/FileTools; Prompts, Resources and TestLab contributions.

**Forbidden:** Neither a projection-only cache nor a universal owner of every domain.; Do not create native nodes as a side effect of ordinary projection reads or rebuilds.; Do not calculate agent prices locally or write process runtime rows directly.; Task/subtype and system-owner flags are not arbitrary JSON switches.

**Persistence:** Separate Workbench_ProjectObjects/Links, bindings/references, lifecycle, layout, leases and mutation receipts logically by authority, not through mass identity changes. Native note bodies are canonical local content. Managed file content belongs to the storage object; Structure owns its use and binding.

**Future:** REQUIRED FOR THE BOUNDARY: typed safe creation/ensure from modules, run-correlated writeback and durable idempotency.; REQUIRED FOR THE BOUNDARY: separate pure queries from the existing GetStatus-to-ApplyStatus path without losing refresh.; DOCUMENTED FOLLOW-UP: content-version editing, capability-specific asset actions, typed relationships and storage-to-database compensation; verify current state.; ESTIMATE: deferred generation recovery after restart; the queue read during preparation does not provide this by itself.

**Feature ids:** FEAT-032; FEAT-033; FEAT-034; FEAT-035; FEAT-036; FEAT-037; FEAT-038; FEAT-039; FEAT-040; FEAT-041; FEAT-042; FEAT-043; FEAT-044; FEAT-045

## MOD-PROCESSES

**Title:** Processes: definitions and authoritative execution

**Repository folder:** src/Modules/CanDoItAll.Modules.Processes

**Primary source ids:** SRC-012; SRC-003; MAP-09

**Target owner:** Processes

**Target scope:** Process definitions, instances, step orchestration, run assignments, approvals and recovery. A task assignment is a work plan; ProcessStepAssignment is a run execution binding. Process runtime remains authoritative over its result.

**Provides:** Definition/version and run queries; start, cancel, resume, approval and recovery according to process policy.; Execution outcomes, artifact manifests and result contributions with stable lineage.; Subprocess start ports and source-authority descriptors.; Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

**Consumes:** Agents execution/catalog; Workflows execution; Plugins driver/executor capabilities.; Project/Structure context through contracts; CRM resource summaries; Storage for run-owned artifacts.; Explicitly granted target-owner query/command APIs through registered adapters; runtime actor/purpose never grants blanket cross-module access (CON-057 through CON-076).

**Forbidden:** Do not launch through Agents UI or a concrete workspace factory.; Structure must not mark ProcessRun Completed directly.; Do not invent a universal ProcessAgentRuntimeToolProvider by relabeling HTTP endpoints.

**Persistence:** Owned process persistence already exists. Do not prematurely split all Process Core again. Limit foreign mappings while preserving run history, transitions, idempotency, approval/recovery evidence and source generation.

**Future:** REQUIRED FOR THE BOUNDARY: a narrow execution port and atomic launch admission with durable origin binding.; REQUIRED FOR THE BOUNDARY: distinguish process results, artifact-attachment state and task acceptance.; UNPROVEN: every live-driver and human-in-the-loop scenario; a mock pass is not production proof.

**Feature ids:** FEAT-046; FEAT-047; FEAT-048; FEAT-049; FEAT-050; FEAT-051; FEAT-052; FEAT-117; FEAT-119

## MOD-PROMPTS

**Title:** Prompts: versioned library and curation

**Repository folder:** src/Modules/CanDoItAll.Modules.Prompts

**Primary source ids:** SRC-015; MAP-11; SRC-003

**Target owner:** Prompts

**Target scope:** PromptArtifact, its versions, collections, tags, compatibility and owned usage/reference evidence. A workflow or agent selects a versioned prompt without acquiring authority to edit it.

**Provides:** Versioned prompt lookup/search and typed owner create/edit/publish/import commands.; Projection contributions, composer use and curator application APIs.; Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

**Consumes:** Providers safe compatibility catalog; Agents for launching a curator.; Projects for scoped references; Security/Storage for attachments and access as needed.

**Forbidden:** MAF Core must not hardcode product prompt-tool names.; A Structure projection does not change the original prompt.; Never skip approval merely because a curator agent supplied the proposal.

**Persistence:** Prompts_* tables have one writer. A prompt version used by execution does not change when the current final/default changes. Historical references and source deletion have explicit retention rules.

**Future:** REQUIRED FOR THE BOUNDARY: curator mutation APIs belong to Prompts; the agent adapter translates intent.; ESTIMATE: stronger publishing/review policy and compatibility matrices; not a new framework for each edit.

**Feature ids:** FEAT-053; FEAT-054; FEAT-055; FEAT-056; FEAT-057; FEAT-058

## MOD-RESOURCES

**Title:** Resources: reusable material catalog

**Repository folder:** src/Modules/CanDoItAll.Modules.Resources

**Primary source ids:** SRC-014; MAP-11

**Target owner:** Resources

**Target scope:** Catalog metadata and connector/content references. A Resource is neither a CRM staffing resource nor every storage object. File access uses an authorized FileTools scope.

**Provides:** Resource catalog queries/commands, storage binding descriptors and source snapshots.; Structure projection contributions, resource attachment references and authorized file-browse scopes.; Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

**Consumes:** Projects existence/scope; Storage/FileTools content; Connector manifests/settings.; Memory ingestion and Agents contextual launch through narrow ports only.

**Forbidden:** Do not read Projects EF entities for catalog queries or move generic connector UI into a Workspace dependency.; Do not own connector secrets or use a physical path as business identity.

**Persistence:** Resources_ProjectResources belongs to Resources. A storage object can be shared by multiple Resource and Structure bindings; physical deletion is not simply Remove(Resource).

**Future:** REQUIRED FOR THE BOUNDARY: a Projects lookup contract and correct content references/retention.; ESTIMATE: versioned resources and improved connector health/reference validation; do not call every legacy type a complete connector.

**Feature ids:** FEAT-059; FEAT-060; FEAT-061; FEAT-062; FEAT-063; FEAT-064

## MOD-SCHEDULER

**Title:** SchedulerPlanner: execution calendar, not a task plan

**Repository folder:** src/Modules/CanDoItAll.Modules.SchedulerPlanner

**Primary source ids:** SRC-013; SRC-003; MAP-11

**Target owner:** SchedulerPlanner

**Target scope:** Schedules, time zone/cron, firing identity, dispatch and schedule history. Quartz is an execution projection of Scheduler. Target workflow/process state belongs to its runtime, not Scheduler.

**Provides:** Schedule CRUD and state; explicit suspension, deletion and reconciliation.; Workflow-scheduling tool adapter and firing correlation.; Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

**Consumes:** Workflows/Processes: typed launch and run state; CRM, Projects and Structure: input-option providers.; Agents: managed Scheduler agent lookup/launch; Security: scope.

**Forbidden:** Do not confuse Gantt planning with cron triggers.; No second process engine or direct reads of foreign workflow EF rows.; An enum or UI item alone does not establish target support.

**Persistence:** SchedulerPlanner_Plans/Runs and Quartz state retain one business authority in the plan and a reconciliation adapter in Quartz. Firing keys remain unique through retries and restarts. Avoid competing raw-SQL schema definitions.

**Future:** REQUIRES VERIFICATION: older README and analysis disagree about process scheduling. The revision audit confirms the managed Scheduler tool provider exposes workflow scheduling only; this does not certify all other scheduler surfaces.; REQUIRED FOR THE BOUNDARY: missed fires, DST, retry and disabling during dispatch must not duplicate launch.; ESTIMATE: additional target support is a separate feature.

**Feature ids:** FEAT-065; FEAT-066; FEAT-067; FEAT-068; FEAT-069; FEAT-116

## MOD-MEMORY

**Title:** Memory: providers, derived knowledge and provenance

**Repository folder:** src/Modules/CanDoItAll.Modules.Memory

**Primary source ids:** SRC-018; SRC-003; MAP-11; MAP-07

**Target owner:** Memory

**Target scope:** Memory configuration, operations, ingestion/retrieval and source provenance. Memory is derived context, not alternative CRM/project truth. Source owners determine exportable snapshots and permissions.

**Provides:** Memory operation/query/status and provider-configuration application contracts.; Ingestion contracts with source identity, revision and access metadata.; Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

**Consumes:** Source-owner snapshots and Security authorization.; Provider, transport, embedding and inference adapters according to configuration.

**Forbidden:** Memory must not authoritatively repair agents, parties or tasks from retrieval text.; Do not embed plaintext secrets or add them to generic context.; Do not couple source domains to a specific vector provider.

**Persistence:** Memory owns derived indexes and operation state. Rebuilding or deleting an index must not delete its sources. Revocation and source tombstones must block access even while physical cleanup is pending.

**Future:** REQUIRED FOR THE BOUNDARY: source scope/revision and revocation across all indexes and caches.; ESTIMATE: improved relevance, evaluation and retention lifecycle; outside a behavior-preserving refactor.

**Feature ids:** FEAT-070; FEAT-071; FEAT-072; FEAT-073; FEAT-074

## MOD-PLUGINS

**Title:** Plugins: installation and authorized activation

**Repository folder:** src/Modules/CanDoItAll.Modules.Plugins

**Primary source ids:** SRC-019; MAP-11

**Target owner:** Plugins

**Target scope:** Plugin installation/activation, manifests, grants, external connections/OAuth and package/runtime lifecycle. Domains invoked by plugins still enforce their own rules.

**Provides:** Manifest/capability catalogs and install/activate/disable/connection commands.; Tool/executor registration with demonstrated availability and grant policy.; Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

**Consumes:** Security for purpose-limited secrets/OAuth; transport/network adapters.; Host composition for activation and domain owners for actual commands.

**Forbidden:** A plugin grant is not blanket permission for any project write.; Do not bypass the Security broker by reading the vault without purpose.; An installed package is not necessarily a loaded, usable capability.

**Persistence:** Plugins owns lifecycle records; Security owns secrets. Installation recovery and disabling during execution have observable states. Keep schema changes on one migration path.

**Future:** REQUIRED FOR THE BOUNDARY: invalidate active tool/capability caches after grant changes.; ESTIMATE: finer version compatibility and package isolation; not an automatic platform redesign.

**Feature ids:** FEAT-075; FEAT-076; FEAT-077; FEAT-078; FEAT-079

## MOD-SECURITY

**Title:** Security: secrets, identities and access enforcement

**Repository folder:** src/Modules/CanDoItAll.Modules.Security

**Primary source ids:** SRC-017; SRC-003; SRC-009; MAP-11

**Target owner:** Security; domains own resource policy, transports validate incoming identity

**Target scope:** Secure secret storage and purpose-limited release, access/capability policy mechanics and references. HTTP authentication and domain authorization must not be confused with text requested by an agent.

**Provides:** Secret-reference queries without values and purpose-scoped resolution/use handles.; Policy and audit identity for a host-verified caller context.; Secret lifecycle commands and reference-aware deletion checks.; Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

**Consumes:** Host platform capabilities and secure key storage.; Reference participants from Providers, Storage and Plugins to validate usage.

**Forbidden:** Do not confuse a profile ID with a permission or tenant.; Do not store plaintext in logs, exceptions, DTOs, projections or exports.; Do not introduce permissive default policies to simplify sandbox DI.

**Persistence:** Vaults and key material may live outside the business database; do not export them implicitly with a project. Profile switching or database restore must not reuse secrets from the previous scope.

**Future:** REQUIRED FOR THE BOUNDARY: explicit execution authority and revocation at the point of use.; ESTIMATE: advanced enterprise identity/residency policies; do not expand scope without an assignment.

**Feature ids:** FEAT-080; FEAT-081; FEAT-082; FEAT-083; FEAT-084

## MOD-COLLAB

**Title:** Collaboration: discussion and human cooperation

**Repository folder:** src/Modules/CanDoItAll.Modules.Collaboration

**Primary source ids:** SRC-020; MAP-11

**Target owner:** Collaboration

**Target scope:** Threads, participants, messages and inbox state with their own collaboration semantics. They are not agent execution transcripts or Simple Chats merely because all contain text messages.

**Provides:** Thread/message queries and commands, participant membership and inbox/read state.; Safe object references and any explicitly supported cross-module activity feed.; Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

**Consumes:** Identity/party summaries and current authorization; owners of referenced objects.; Shared presentation primitives without inheriting execution state.

**Forbidden:** Do not merge all chat types into one business table for UI reuse.; Discussion content is not an implicit agent system prompt.

**Persistence:** Collaboration owns messages and membership. Project/task links have explicit lifecycle handling rather than indiscriminate cross-domain EF cascades.

**Future:** REQUIRED FOR THE BOUNDARY: verify current membership/privacy semantics before moving contracts.; ESTIMATE: real-time collaboration, mentions and richer attachments; do not claim these are all delivered.

**Feature ids:** FEAT-085; FEAT-086; FEAT-087

## MOD-WORKSPACE

**Title:** Workspace: environment settings and presentation

**Repository folder:** src/Modules/CanDoItAll.Modules.Workspace

**Primary source ids:** SRC-021; MAP-11

**Target owner:** Workspace

**Target scope:** Workspace preferences and working-environment identity; settings of other domains are composed only. Database-profile administration is control-plane work; secrets belong to Security, providers to Agents, and the storage catalog to Storage.

**Provides:** Scoped preference queries/updates and active-workspace information.; Settings composition through owner contracts; a UI registry may be extracted into a neutral presentation boundary.; Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

**Consumes:** Providers catalog, Storage administration, Security/API access and control-plane database profiles.; Connector manifest/command lifecycle for actually activated handlers.

**Forbidden:** Do not store arbitrary cross-module entities as Workspace settings.; No new provider truth in Workspace.; Do not retarget an in-flight operation to the database currently selected in the UI.

**Persistence:** Workspace_Settings contains owned preferences. Other historically colocated tables receive owners according to meaning, not prefix. The platform coordinates profile transfer and migration.

**Future:** REQUIRED FOR THE BOUNDARY: distinguish host/control-plane identity, domain workspace and UI editor instance.; REQUIRES VERIFICATION: dormant connector outbox and actual handlers; do not delete them based only on old analysis.; ESTIMATE: administrative capability catalogs and improved standalone hosts, without permissive fallbacks.

**Feature ids:** FEAT-088; FEAT-089; FEAT-090; FEAT-091; FEAT-092; FEAT-093

## MOD-TESTLAB

**Title:** TestLab: product test plans, tests and evidence

**Repository folder:** src/Modules/CanDoItAll.Modules.TestLab

**Primary source ids:** SRC-016; MAP-11

**Target owner:** TestLab

**Target scope:** Product test definitions, cases, runs and evidence. TestLab is not repository build/test infrastructure and does not replace its QA. Tested objects and versions are explicit references.

**Provides:** Test plan/case/run/evidence queries and owner commands; verdicts with source revisions.; Structure contributions, linked-test navigation and evidence export.; Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

**Consumes:** Projects/Structure source references; Agents, Workflows and Processes only for specifically supported runners.; Storage for evidence bytes; Security for authorized access.

**Forbidden:** A Test button opening only generic project TestLab does not prove a test of a specific node.; Runtime exit success is neither evidence quality nor task acceptance.

**Persistence:** TestLab owns test records and evidence metadata; Storage owns bytes. A historical verdict does not automatically apply to a new source version.

**Future:** REQUIRED FOR THE BOUNDARY: identify exactly which tests and runner operations are implemented.; ESTIMATE: richer evidence attachments and evaluation of more artifact kinds.

**Feature ids:** FEAT-094; FEAT-095; FEAT-096; FEAT-097

## BND-WORK

**Title:** Work Management within Project Structure

**Repository folder:** Not recorded

**Parent module id:** MOD-STRUCTURE

**Primary source ids:** SRC-025; MAP-02; MAP-08

**Target owner:** Work Management within Project Structure

**Target scope:** Tasks/work items, schedules, dependencies, assignments, baselines and acceptance. One mutation route serves Gantt, canvas, CRM and tools.

**Provides:** Canonical task queries and typed creation/edit/dependency/scheduling/assignment commands; Gantt is their view.; Work baselines, cost allocation and accepted-result rules; publishes workload summaries for CRM.; Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

**Consumes:** Agents catalog and Providers quotes; CRM resource/participation facts and capacity reservation.; Projects lifecycle/scope; Structure native placement; Processes/Workflows execution state and manifests.

**Forbidden:** Work assignment is not capacity reservation; a completed runtime does not automatically mean an accepted result.

**Persistence:** The initial target can retain ProjectObjectRecord as the canonical task. Keep one task identity, not synchronized writable Tasks and Nodes tables. Current ProjectPartyAssignment data requires semantic reconciliation before ownership moves.

**Future:** REQUIRED FOR THE BOUNDARY: distinguish tasks from process steps and assignments from reservations, and establish one writer for CRM/Gantt/tools.; ESTIMATE: workload-aware estimates, baseline versioning and formal result-specific acceptance; preserve supported policies without automatic expansion.

**Feature ids:** FEAT-105

## BND-WORKFLOWS

**Title:** Agents / Workflows

**Repository folder:** Not recorded

**Parent module id:** MOD-AGENTS

**Primary source ids:** SRC-026; SRC-027; SRC-003; MAP-09

**Target owner:** Agents / Workflows

**Target scope:** Versioned technical AI workflows, execution backends, runs/checkpoints/approvals and results. A product integration executor belongs to an adapter; generic runtime does not know the concrete product.

**Provides:** Definition/version catalogs, input schemas and compiler/executor contracts; explicit backend/simulation support.; Durable or currently supported launch/status/approval/cancel/recovery routes and owner output manifests.; Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

**Consumes:** Provider/Agent execution, Prompt versions, Plugin grants and capability-owned executors.; Product-owned Structure gateways and authority resolvers through integration executors; Scheduler/Processes provide source-origin bindings.; Explicitly granted target-owner query/command APIs through registered adapters; runtime actor/purpose never grants blanket cross-module access (CON-057 through CON-076).

**Forbidden:** Preserve ProjectStructureWorkflowExecutor and launch from Structure. Do not generalize a name-based enum into a universal service locator.

**Persistence:** Workflows owns definitions/versions, runs and checkpoints. Structure stores bindings and projections, not a second state machine. A later edit must not change the resolved definition of an accepted run; LastRunId is not complete history.

**Future:** REQUIRED FOR THE BOUNDARY: separate status queries from Structure projection updates while preserving launch/writeback in both directions.; UNPROVEN: identical human-in-the-loop/recovery capabilities across all backends; extend only against a verified support matrix.

**Feature ids:** FEAT-103; FEAT-104; FEAT-118

## BND-SIMPLECHATS

**Title:** Simple Chats: independent ordinary LLM conversations

**Repository folder:** Not recorded

**Parent module id:** Not recorded

**Primary source ids:** SRC-006; SRC-009

**Target owner:** Simple Chats

**Target scope:** Owns definitions, conversations, durable operations, leases and journals. Sharing UI or floating shells does not activate agent tools.

**Provides:** Ordinary conversation definitions, durable turn admission/status/replay, transcript/retention and operation identity.; Product-specific adapters for workspace/floating shells and authorized HTTP APIs.; Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

**Consumes:** Provider-neutral LLM/model contracts and provider-runtime adapters; persistence and owner admission/leases.; Neutral conversation presentation; any explicit Prompt Gallery/report adapters live outside product core.

**Forbidden:** Implicit Project Structure context is not delivered according to documentation. An explicit bounded-input adapter is a future feature, not a required refactor. Closing a surface is not operation cancellation.; Adding tools, agent execution, implicit context, or transcript administration merely because an HR agent can edit a definition.

**Persistence:** Owned conversation/operation/event journals, leases and PostgreSQL persistence stay isolated from Agent execution. SSE replays committed journal events. Do not merge transcript identities or retention rules with Collaboration or Agents.

**Future:** REQUIRED FOR THE BOUNDARY: preserve ordinary no-tools/no-implicit-project-context behavior and non-idempotent conversation creation versus retry-safe turns.; ESTIMATED FROM DOCUMENTATION: explicit project-context adapters and deployment/channel/human-handoff models belong to separate later features. Characterize existing reporting hooks first.; Requested HR/CRM-specialist definition administration via an external adapter reusing existing definition API. Keep all product/persistence dependencies on agent tooling absent; do not grant transcript access.

**Feature ids:** FEAT-098; FEAT-099; FEAT-100; FEAT-114; FEAT-115

## BND-CONVERSATIONS

**Title:** Shared conversation presentation

**Repository folder:** Not recorded

**Parent module id:** Not recorded

**Primary source ids:** SRC-006; SRC-009; MAP-03; MAP-07

**Target owner:** Shared conversation presentation

**Target scope:** Neutral message rendering, composers and floating windows. Session instances, focus, overlay order and subscription lifecycle.

**Provides:** Reusable message rendering, composer intent, floating-window layout, focus, overlay order and session lifecycle.; UI-only state/intent contracts for different product-specific adapters.

**Consumes:** Sanitized immutable presentation state from an Agents or Simple Chats adapter, not a domain runtime service.; Shell navigation/JS handles and explicit view lifetime. Attachment selection emits intent rather than writing files or calling providers.

**Forbidden:** Execution, approvals, tool availability, transcript retention and API access belong to the respective product adapter.

**Persistence:** Owns no business transcript or run persistence. Local drafts/window/view state are UI-owned and scope-bound; persisted UI state must not masquerade as business authority. The product adapter supplies data and cancellation policy.

**Future:** REQUIRED FOR THE BOUNDARY: preserve floating agents over Structure/Gantt without references from neutral UI to those domains.; ESTIMATE: another conversation product can supply another adapter without adding its business rules to shared components.

**Feature ids:** FEAT-101

## BND-STORAGE

**Title:** Storage and FileTools

**Repository folder:** Not recorded

**Parent module id:** Not recorded

**Primary source ids:** SRC-004; SRC-005; SRC-008; MAP-11

**Target owner:** Storage and FileTools

**Target scope:** Storage catalogs, logical locators, managed bytes/revisions, path containment/provenance and authorized browsing/opening.

**Provides:** Managed object/revision/placement and content streams; staging/finalization/cleanup according to driver.; Provenance-aware deletion, reference-aware eligibility and authorized file scopes; desktop opening is a separate host capability.; Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

**Consumes:** Security secret resolution for a specific driver; control-plane current profile/root identity.; Owner-issued bindings/scopes and deletion eligibility, not arbitrary agent-authored paths.

**Forbidden:** Resource metadata, native Structure nodes and run artifact manifests are not one storage business entity. FileTools exposes only owner-issued scopes.

**Persistence:** Physical bytes are not a SQL transaction. Storage objects and revisions have identity/provenance; domain bindings remain with their owners. Safe cleanup considers references, running leases and authorized roots, not merely cached reference counts.

**Future:** REQUIRED FOR THE BOUNDARY: preserve read/create/file/preview/delete outcomes and compensation for partial database/storage failures.; ESTIMATE: unified versioned content editing and operational reconciliation by driver; scope requires separate confirmation.

**Feature ids:** FEAT-102

## BND-CONNECTORS

**Title:** Connectors / external integrations

**Repository folder:** Not recorded

**Parent module id:** Not recorded

**Primary source ids:** MAP-11; SRC-019

**Target owner:** Connectors / external integrations

**Target scope:** Manifest/schema/driver registries and concrete command adapters, with durable delivery where needed.

**Provides:** Manifest/capability/configuration-schema mechanisms and transport-adapter registries.; Explicit typed command/result boundaries only for actually registered, supported handlers.; Automation uses the same canonical owner application boundary as other callers where supported; required read/write surfaces and rollout status are specified in catalogs/module-operation-matrix.json. This is not a default grant.

**Consumes:** Concrete provider/resource/plugin configuration references and authorization; purpose-bound Security resolution.; Host/runtime I/O adapters and the affected owner service for business mutations.

**Forbidden:** A schema renderer does not own configuration. A dormant outbox is not a completed integration. Plugin and connector are not synonyms.

**Persistence:** Registries and durable command records have their own operational lifecycle. Manifest metadata is not a second provider/resource master. Configuration changes use owner APIs; arbitrary settings JSON is not an escape hatch.

**Future:** REQUIRED FOR THE BOUNDARY: distinguish configuration rendering/manifests from executable capabilities.; UNPROVEN: complete current handler coverage; absence in an old map proves no absence now. Unsupported never means fake success.

**Feature ids:** FEAT-106

## BND-COMPOSITION

**Title:** Host, composition and control plane

**Repository folder:** Not recorded

**Parent module id:** Not recorded

**Primary source ids:** SRC-002; SRC-008; MAP-10

**Target owner:** Host, composition and control plane

**Target scope:** Implementation registration, profile lifetimes, migration/bootstrap, workers and shutdown, desktop/headless capabilities and HTTP adapters.

**Provides:** Explicit module/adapter registration, runtime host-capability descriptors and short-operation scope factories.; Control plane for profile activation/draining, complete schema composition, transfer coordination and worker lifetime.

**Consumes:** Concrete implementations and owner registration/lifecycle participants; composition does not transfer ownership.; Host OS/database/storage configuration through responsible infrastructure and operator authorization.

**Forbidden:** Composition may know every implementation for assembly, but must not absorb business logic or provide a global database backdoor.

**Persistence:** A complete design-time schema model can know all mappings; applications must not receive it as a global writer. Control-plane operation plans have their own persistence; domain data moves through participant owner APIs.

**Future:** REQUIRED FOR THE BOUNDARY: distinguish lightweight presentation hosts from runtime feature hosts; fence profile lifetime and preserve bootstrap/upgrades.; ESTIMATE: more multi-instance/runtime partitioning only as needed, not immediate microservices or a generic plugin framework.

**Feature ids:** FEAT-107

## BND-MAF

**Title:** Neutral runtime / SDK boundary

**Repository folder:** Not recorded

**Parent module id:** Not recorded

**Primary source ids:** SRC-003; SRC-027; MAP-07

**Target owner:** Neutral runtime / SDK boundary

**Target scope:** Provider/LLM/tool/executor abstractions and generic execution algorithms; external SDK isolation.

**Provides:** Neutral provider/LLM/tool/executor contracts, descriptor composition and generic execution mechanics.; SDK adapters with controlled failure/cancellation semantics and no product-specific master models.

**Consumes:** Registered capability/executor adapters and verified purpose/scope; explicit provider-neutral configuration.; External SDKs inside adapters; product tool-policy inputs through published contracts.

**Forbidden:** Concrete CRM, Project Structure, Prompt Gallery and Scheduler semantics belong to product-owned packs/adapters, not generic Core.

**Persistence:** Not a universal application store. Runtime checkpoints, usage and operation persistence need explicit execution owners. Preserve serialized payload/version compatibility when moving product types out of neutral Core.

**Future:** REQUIRED FOR THE BOUNDARY: move product policy/vocabulary to owner packs without deleting needed tools.; OUT OF SCOPE: SDK/MAF upgrades and concurrent tools; preserve serial order and approval barriers.

**Feature ids:** FEAT-108

## BND-API

**Title:** HTTP / transport adapters

**Repository folder:** Not recorded

**Parent module id:** Not recorded

**Primary source ids:** SRC-003; SRC-006; SRC-002

**Target owner:** HTTP / transport adapters

**Target scope:** Incoming authentication, DTOs/versions, authorized route scopes, ETags, Problem Details, streaming and client disconnection.

**Provides:** Authorized HTTP/OpenAPI transport, DTO/status/ETag/problem mapping and committed-event streaming according to existing APIs.; External client contracts with compatible routes and explicit scope/operation semantics.

**Consumes:** Owner application queries/commands rather than Razor pages/controllers or foreign DbContexts.; Authentication/authorization and correct profile source authority; runtime tool grants are separate permissions.

**Forbidden:** An existing route does not attach a runtime tool. Domain commands are shared with UI/tools; a disconnected request token does not prove rollback.

**Persistence:** Owns no business master and performs no direct domain EF writes. Transport tracking can reference owner receipts but must not create a second execution state. SSE wake-ups are not durable journals.

**Future:** REQUIRED FOR THE BOUNDARY: preserve supported HTTP surfaces, routes and serialized semantics while moving services.; ESTIMATE: expose new public operations only when owner capabilities exist; do not automatically publish every internal method.

**Feature ids:** FEAT-109
