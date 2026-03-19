# 04 — Solution Architecture

## 1. Executive summary

The recommended architecture for CanDoItAll is a **local-first modular monolith** built with **.NET 10**, **C#**, **Blazor Web App using Interactive Server rendering**, **Tailwind CSS**, and **EF Core 10**. The modular monolith is intentionally designed with **explicit internal contracts**, **durable business events**, and **sidecar-ready boundaries** so the application can later split heavy subsystems into local or remote services without invalidating the original design.

This architecture is chosen because it solves the current problem well:
- one cohesive UI
- one deployable host
- low operational complexity on a local workstation
- strong modularity for future growth
- practical support for a large feature surface
- a clear place for a separate local development manager that accelerates delivery

Within this architecture, `PromptStudio` may exist only as an internal prompt-focused workspace/module concept inside CanDoItAll. It must not be treated as the application name.

## 2. Architectural goals

1. Deliver a usable v1 without a distributed-system tax.
2. Keep project, prompt, validation, and resource workflows unified in one application shell.
3. Support OpenAI and Ollama providers behind a common abstraction.
4. Store metadata safely and consistently across SQLite and PostgreSQL.
5. Handle secrets and execution-capable integrations carefully.
6. Enable future extraction of heavy workloads into sidecars/services.
7. Remain implementable in small Codex-friendly slices.
8. Keep the local development loop explicit, trustworthy, and automatable.

## 3. Architectural style

### 3.1 Primary style
**Modular monolith with vertical slices inside modules**

### 3.2 Why not full microservices now?
A full microservice architecture would:
- increase setup and runtime complexity on a local workstation
- slow initial delivery
- create unnecessary distributed consistency problems
- complicate debugging and development loops
- make the unified UI harder to evolve early

### 3.3 Why not a single undifferentiated web project?
A single flat web project would:
- blur boundaries
- make later extraction difficult
- increase accidental coupling
- become hard to reason about as the feature set expands

### 3.4 Design conclusion
Start with a modular monolith, but make the seams real:
- module contracts
- module registration
- internal event publishing
- clear persistence ownership
- separate feature folders
- serializable DTOs for eventual externalization

## 4. High-level solution structure

```text
CanDoItAll.slnx
│
├─ src/
│  ├─ CanDoItAll.Web/                      # Blazor host, composition root, shell
│  ├─ CanDoItAll.SharedKernel/             # Common primitives, results, base abstractions
│  ├─ CanDoItAll.Infrastructure/           # Cross-cutting infrastructure
│  ├─ CanDoItAll.ComponentKit/             # Shared UI wrappers and missing reusable components
│  │
│  ├─ CanDoItAll.Modules.Workspace/        # Settings, provider profiles, defaults
│  ├─ CanDoItAll.Modules.Security/         # Secret vault, redaction, approval gates
│  ├─ CanDoItAll.Modules.Projects/         # Projects, phases, statuses, stack profile
│  ├─ CanDoItAll.Modules.Workbench/        # Internal tabs, project structure, project calendar
│  ├─ CanDoItAll.Modules.Resources/        # Typed resources and connector profiles
│  ├─ CanDoItAll.Modules.Prompts/          # Prompt library, collections, versions, usage
│  ├─ CanDoItAll.Modules.Factory/          # Prompt factory, blueprints, context assembly
│  ├─ CanDoItAll.Modules.Validation/       # Review flows, checklists, findings
│  ├─ CanDoItAll.Modules.TestLab/          # Test plans, evidence, screenshot/test links
│  ├─ CanDoItAll.Modules.Activity/         # Activity timeline, audit, search documents
│  └─ CanDoItAll.Modules.Automation/       # Background jobs, execution gates, sidecar hooks
│
├─ tests/
│  ├─ CanDoItAll.Tests.Unit/
│  ├─ CanDoItAll.Tests.Integration/
│  ├─ CanDoItAll.Tests.Components/
│  └─ CanDoItAll.Tests.Playwright/
│
└─ docs/                                   # optional runtime docs/export area
```

The solution should also include a separate local tool project outside the product modules:

- `tools/CanDoItAll.Manager` — development manager for `dotnet watch`, local OpenAPI or SSE, capsule generation, and dev-only tuning orchestration

## 5. Project responsibilities

## 5.1 CanDoItAll.Web
Responsibilities:
- application startup
- DI composition root
- render mode configuration
- route registration
- shell pages
- feature discovery
- authentication/authorization foundation for future growth
- top-level error handling
- development-only runtime readiness endpoint for the manager
- development-only tuning-mode integration points

Keep it thin. Business rules must live in modules.

## 5.2 CanDoItAll.SharedKernel
Responsibilities:
- entity base types
- strongly typed identifiers
- result/error types
- guard helpers
- time/provider abstractions
- domain event base types
- common enums that truly belong everywhere

Do not turn this into a dumping ground.

## 5.3 CanDoItAll.Infrastructure
Responsibilities:
- database bootstrap
- file storage abstractions
- background queue framework
- options binding and validation
- health checks
- logging/redaction helpers
- serialization helpers
- event dispatch plumbing
- common persistence helpers
- dev-only runtime probe contracts that do not belong in business modules

## 5.4 CanDoItAll.ComponentKit
Responsibilities:
- app-specific reusable Blazor components
- wrappers around the existing `CanDoItAll.Components` set
- visual consistency helpers
- page templates
- tab-strip and shell primitives
- canvas and workbench host components
- state presentation primitives
- tunable component boundaries and dev-only tuning UI primitives

## 5.5 CanDoItAll.Modules.Workbench
Responsibilities:
- internal tab workspace
- browser-state-backed tab restore
- sleeping and background tab lifecycle
- project structure canvas orchestration
- project events calendar orchestration
- deep-link resolution into internal tabs
- workbench-level artifact opening policies
- stable tab and selection metadata for tuning-mode context

## 5.5A CanDoItAll.Manager
Responsibilities:
- supervise `dotnet watch` for the main app
- normalize watch, build, and runtime states
- expose loopback-only OpenAPI and SSE endpoints for Codex and diagnostics
- generate capsule artifacts from source comments
- report capsule coverage and drift
- accept and track dev-only tuning requests
- correlate Codex jobs, watch readiness, and verification outcomes

This tool is intentionally separate from the runtime product modules. It accelerates local delivery and testing without polluting the product domain model.

## 5.6 Feature modules
Each module owns:
- domain entities/value objects
- commands/queries/services
- validators
- EF mappings
- module UI components/pages where appropriate
- integration contracts/events
- read models and display DTOs

UI grouping note:
The prompt-oriented workspace may be surfaced in the shell as `PromptStudio`, but that is only a workspace label over `CanDoItAll.Modules.Prompts`, `CanDoItAll.Modules.Factory`, and related prompt-validation flows. It is not the application name and should not drive solution or namespace naming.

## 6. Module design

## 6.1 Workspace module
Responsibilities:
- workspace defaults
- provider profile registry
- option catalogs
- user/workspace preferences
- startup configuration completeness checks

Key aggregates:
- `WorkspaceSettings`
- `ProviderProfile`
- `OptionCatalog`
- `OptionCatalogItem`

## 6.2 Security module
Responsibilities:
- secret storage
- secret references
- protection/unprotection
- approval policies
- redaction utilities
- sensitive action gates

Key aggregates:
- `SecretRecord`
- `SecretReference`
- `ApprovalPolicy`
- `ApprovalDecision`

## 6.3 Projects module
Responsibilities:
- projects
- statuses
- phases
- dates
- stack profiles
- option selections
- project summaries

Key aggregates:
- `Project`
- `ProjectPhase`
- `ProjectStatus`
- `ProjectStackProfile`
- `ProjectOptionSelection`

## 6.4 Workbench module
Responsibilities:
- internal tabs
- tab snapshots and restore
- tab sleep and wake policies
- project structure manifests
- project structure relationships
- project calendar events and views
- artifact-opening intents
- canvas command dispatch and action routing
- prompt-flow node projection into the workbench surface

Key aggregates:
- `WorkbenchSession`
- `WorkbenchTab`
- `TabSnapshot`
- `ProjectStructureManifest`
- `ProjectStructureLink`
- `ProjectCalendarEvent`
- `ProjectCalendarPreference`
- `WorkbenchCommand`
- `WorkbenchSelection`

## 6.5 Resources module
Responsibilities:
- typed project resources
- connector profiles
- resource validation
- preview and indexing registration
- sensitivity and storage policy

Key aggregates:
- `ProjectResource`
- `ResourceDescriptor`
- `ConnectorProfile`
- `ResourceValidationRecord`
- `ResourceSnapshot`

## 6.6 Prompts module
Responsibilities:
- prompt drafts
- prompt versions
- galleries/collections
- tags
- usage history
- export metadata

Key aggregates:
- `PromptArtifact`
- `PromptVersion`
- `PromptCollection`
- `PromptUsageRecord`
- `PromptTag`

## 6.7 Factory module
Responsibilities:
- shared prompt block catalog
- prompt flow template catalog
- prompt run orchestration
- phase-driven wizard
- blueprint catalog
- context assembly
- prompt rendering
- prompt pre-send validation
- prompt generation sessions

Key aggregates:
- `PromptBlockDefinition`
- `PromptFlowTemplate`
- `PromptRun`
- `PromptRunNode`
- `PromptBlueprint`
- `PromptBuildSession`
- `ContextAssembly`
- `PromptValidationResult`

## 6.8 Validation module
Responsibilities:
- story/use-case validation
- layout validation
- architecture validation
- plan validation
- prototype validation
- findings and decisions
- rule sets and AI-assisted reviews

Key aggregates:
- `ValidationRun`
- `ValidationChecklist`
- `ValidationFinding`
- `ReviewDecision`

## 6.9 TestLab module
Responsibilities:
- coverage planning
- test evidence
- screenshot records
- Playwright test linkage
- run results
- traceability to stories/features/phases

Key aggregates:
- `TestPlan`
- `TestCaseLink`
- `TestRunRecord`
- `EvidenceArtifact`

## 6.10 Activity module
Responsibilities:
- audit trail
- timeline
- queryable usage history
- search document registry
- notifications

Key aggregates:
- `ActivityEntry`
- `AuditRecord`
- `SearchDocument`
- `NotificationRecord`

## 6.11 Automation module
Responsibilities:
- background job queue
- sidecar integration contracts
- safe execution wrappers
- orchestration of indexing, validation, sync, or test tasks

Key aggregates:
- `QueuedJob`
- `JobExecutionRecord`
- `AutomationPolicy`
- `SidecarRegistration`

## 6.12 Development manager tool
Responsibilities:
- watch session supervision
- structured watch signals
- runtime readiness confirmation
- capsule document generation
- capsule coverage reporting
- tuning request orchestration
- local correlation history

Key models:
- `ManagerWatchSession`
- `ManagerWatchSignal`
- `CapsuleDocument`
- `CapsuleCoverageReport`
- `TuningRequest`
- `TuningJob`

## 7. Recommended internal architecture inside modules

Each module should use a practical vertical-slice layout:

```text
CanDoItAll.Modules.Prompts/
├─ Domain/
├─ Application/
│  ├─ Commands/
│  ├─ Queries/
│  ├─ Services/
│  └─ Validators/
├─ Infrastructure/
│  ├─ Persistence/
│  ├─ Mapping/
│  └─ Adapters/
├─ Ui/
│  ├─ Pages/
│  ├─ Components/
│  └─ ViewModels/
└─ Contracts/
```

This is intentionally pragmatic:
- enough structure for scale
- not so many projects that implementation becomes slow

## 8. Runtime architecture

## 8.1 Runtime layers
1. Blazor UI shell and module UIs
2. Application services / handlers
3. Domain model
4. Infrastructure adapters
5. External systems and local machine integrations

## 8.2 Request flow pattern
1. UI action triggers command/query.
2. Application service validates input.
3. Domain logic executes.
4. Persistence commits changes.
5. Domain/integration events are emitted.
6. Post-commit handlers update activity, search, background work, or notifications.
7. UI refreshes with updated read models.

## 8.3 Background work pattern
Used for:
- indexing files
- validating connectors
- importing metadata
- provider health refresh
- large prompt processing
- screenshot/test artifact handling

Pattern:
- enqueue job
- persist job record
- process in background service
- update job/result status
- publish activity event
- refresh UI state through polling or notifications

## 8.4 Workbench runtime pattern
The shell must treat internal tabs as a first-class runtime concern:

1. The user opens an artifact through the tab host.
2. The tab host resolves whether to activate, duplicate, background, or restore.
3. Heavy tabs can move into a sleeping snapshot state.
4. Workbench surfaces publish stable tab and selection metadata for deep links and tuning-mode context.

## 8.5 Development manager runtime pattern
The local development loop should work as follows:

1. The manager launches `dotnet watch` for the main app.
2. The manager parses raw watch output into normalized state transitions.
3. The manager confirms runtime readiness through the development-only readiness endpoint exposed by the main app.
4. The manager publishes watch and readiness events through local API and SSE endpoints.
5. Capsule changes regenerate Codex-optimized artifacts incrementally.
6. Tuning requests correlate a component, capsule, Codex job, watch-ready event, and optional verification result.
4. Tab session state is persisted through a browser-storage abstraction.
5. On restart or reconnect, the shell restores the session and rehydrates tabs selectively.

This pattern is mandatory because the product runs on Interactive Server and must not depend on many browser tabs for normal work.

## 9. Rendering model

The web host should use:
- **Interactive Server** as the default interactive mode
- static SSR where beneficial for lightweight or non-interactive surfaces
- carefully controlled interactivity for complex pages

Why:
- the application is local-first
- server-rendered interactivity fits the workstation-hosted control plane
- the app benefits from central access to file system, secrets, and local integrations
- internal tabs let the app manage heavy screen lifecycles intentionally instead of delegating that to browser-tab behavior

## 10. Persistence architecture

## 10.1 Database providers
Supported providers:
- SQLite
- PostgreSQL

### Default recommendation
- SQLite for single-workstation default simplicity
- PostgreSQL when higher concurrency, larger data, or stronger operational DB features are desired

## 10.2 DbContext strategy
Use:
- one primary `AppDbContext` for v1 module persistence
- module-specific configuration classes per feature
- `IDbContextFactory<AppDbContext>` for runtime access
- `IDesignTimeDbContextFactory<AppDbContext>` for migrations

### Why a single `AppDbContext` in v1?
- simpler migration management
- less ceremony
- still allows clean modular boundaries through configuration ownership
- easier for Codex-driven implementation

### How future extraction still works
- entities remain module-owned
- integration events are explicit
- service boundaries are defined at module contracts, not at DbContext count

## 10.3 Database schema partitioning
Use table naming by module prefix, for example:
- `Workspace_ProviderProfiles`
- `Security_SecretRecords`
- `Projects_Projects`
- `Resources_ProjectResources`
- `Prompts_PromptArtifacts`
- `Factory_PromptBlueprints`
- `Validation_ValidationRuns`
- `TestLab_TestRunRecords`
- `Activity_AuditRecords`
- `Automation_QueuedJobs`

This works across SQLite and PostgreSQL even without schema-per-module support.

## 10.4 Storage split
Use a hybrid model:

### Database stores
- project metadata
- option selections
- prompt metadata and bodies
- connector profiles
- secret metadata and encrypted payloads
- usage history
- findings
- activity
- search documents

### File system stores
- uploaded or managed file copies
- extracted text caches
- preview artifacts
- screenshots
- exported prompt packages
- large evidence files
- optional extracted tab or canvas snapshot payloads when not suitable for direct database storage
- manager-owned capsule artifacts, tuning attachments, and correlation logs in excluded artifact paths

## 10.5 File storage abstraction
Create:
- `IFileStore`
- `IWorkspacePathResolver`
- `IManagedArtifactStore`

Support modes:
- reference only
- managed copy
- cached preview
- evidence artifact storage
- manager artifact storage with excluded watch paths

## 11. Secret handling architecture

## 11.1 Principles
- secrets are first-class records
- secrets are always referenced, not duplicated
- display is redacted by default
- logs never include raw secret values
- external action approval is separate from secret access

## 11.2 Secret model
`SecretRecord`
- Id
- Name
- Kind
- EncryptedPayload
- MetadataJson
- RotationNote
- Scope
- CreatedAt
- UpdatedAt

`SecretReference`
- Id
- SecretRecordId
- ContextType
- ContextId
- Purpose

## 11.3 Protection mechanism
Use a secret protection abstraction:
- `ISecretProtector`
- implementation backed by ASP.NET Core Data Protection for v1
- later pluggable support for OS-specific vaults

## 12. Provider integration architecture

## 12.1 Design goals
- support OpenAI and Ollama local/remote
- preserve provider-neutral application logic
- allow capability detection
- allow provider-specific extensions behind flags
- support streaming and structured prompt execution results

## 12.2 Recommended abstraction
Use a provider facade layer with:
- `IModelProviderRegistry`
- `IChatProviderClient`
- `IPromptExecutionService`
- `IProviderHealthService`
- `IModelCapabilityResolver`

Optional implementation detail:
- use `Microsoft.Extensions.AI` abstractions at the integration boundary
- wrap provider-specific SDKs/adapters behind the app’s own interfaces

## 12.3 Capability flags
Track capabilities such as:
- supports streaming
- supports tool calling
- supports structured output
- supports vision
- supports stateful conversation
- supports local execution
- requires API key
- supports model listing

This is important because OpenAI and Ollama will not always behave identically.

## 12.4 Provider profiles
`ProviderProfile`
- Id
- Name
- ProviderKind
- BaseUrl
- ApiKeySecretId
- DefaultModel
- TimeoutSeconds
- IsEnabled
- ExtraSettingsJson
- CapabilityOverridesJson

## 12.5 Provider execution result
`ProviderExecutionRecord`
- ProviderProfileId
- ModelId
- RequestSummary
- ResponseSummary
- TokensOrUsageJson
- Status
- DurationMs
- CreatedAt

Do not store full sensitive response content by default unless the user explicitly wants that behavior.

## 13. Resource architecture

## 13.1 Generalized resource model
All linked objects are modeled as `ProjectResource` with a resource kind and type-specific configuration.

`ProjectResource`
- Id
- ProjectId
- ResourceKind
- Name
- Description
- LocationOrIdentifier
- ConfigJson
- StoragePolicy
- Sensitivity
- ValidationStatus
- PreviewStatus
- IndexingStatus
- LinkedSecretIds
- LastValidatedAt
- LastIndexedAt

## 13.2 Why generalized resources?
This design:
- avoids a large entity explosion for every new asset type
- supports common list/detail behavior
- allows typed editors and validators per resource kind
- keeps future extension practical

## 13.3 Descriptor registry
Create:
- `IResourceDescriptorRegistry`
- `IResourceEditorFactory`
- `IResourceValidator`
- `IResourcePreviewProvider`
- `IResourceIndexer`

Each resource kind registers:
- editor component
- config model
- validation logic
- preview capability
- indexing capability
- execution capability flag

## 13.4 Required resource kinds
- folder
- file
- web link
- FTP profile
- PowerShell script
- repository
- Docker / Docker Compose
- SSH profile
- secret link
- prompt link

## 14. Prompt architecture

## 14.1 Core entities
`PromptArtifact`
- mutable draft metadata
- phase association
- collection membership
- current status

`PromptVersion`
- immutable prompt text snapshot
- version number
- creation reason
- output format
- source blueprint reference

`PromptCollection`
- named gallery or collection

`PromptUsageRecord`
- project
- phase
- provider
- repo
- branch
- commit SHA
- commit URL
- time
- usage note

## 14.2 Draft/final/version rule
- Drafts are editable.
- Final prompts create immutable versions.
- Cloning produces a new draft lineage.
- Usage records point to a specific version where possible.

## 14.3 Blueprint model
`PromptBlueprint`
- Name
- Phase
- Category
- PromptTemplate
- DefaultBlockIds
- AutoAppliedBlockRulesJson
- InputContractJson
- OutputContractJson
- DefaultChecklistIds
- DefaultTagSet
- Version

## 15. Prompt factory architecture

## 15.1 Pipeline
1. select project and phase
2. select or initialize the prompt flow template
3. select shared prompt blocks and per-run customizations
4. select blueprint
5. assemble relevant context
6. validate completeness
7. render prompt
8. allow user edits
9. persist draft/final
10. export/send
11. record usage or execution outcome

## 15.1A Shared block and flow-template model
The Factory module must treat recurring prompt instructions as first-class assets, not strings copied between wizards.

`PromptBlockDefinition`
- Name
- Category
- InstructionTemplate
- InputContractJson
- ActivationRulesJson
- DefaultOrder
- Version
- IsEnabled

`PromptFlowTemplate`
- Name
- Phase
- Category
- BlockSequenceJson
- RecommendedBlueprintId
- ConcurrencyPolicy
- Version

`PromptRun`
- ProjectId
- FlowTemplateId
- Status
- StartedAtUtc
- CompletedAtUtc

`PromptRunNode`
- PromptRunId
- PromptBlockDefinitionId
- ParentNodeId
- State
- CustomInstructionOverlay
- OrderIndex
- BranchKey

The design must allow multiple active branches for one project or feature without losing lineage between nodes.

## 15.2 Context assembly inputs
- project metadata
- selected phases/status
- option selections + notes
- selected resources
- linked prompt history
- prior validation findings
- user-entered objective
- output expectations
- provider/model settings

## 15.3 Context assembly output
`ContextAssembly`
- selected inputs
- generated sections
- warnings
- estimated size / token budget note
- omitted items list

## 15.4 Validation before send
Checks should include:
- missing project name
- missing phase
- no blueprint selected
- empty objective
- unsupported provider/model mismatch
- sensitive data presence
- overly large context warning
- required checklist not acknowledged

## 16. Validation architecture

## 16.1 Rule-first validation model
Each validation run should combine:
- deterministic rules
- structured checklists
- optional AI critique
- persistent findings

## 16.2 Validation types
- story completeness
- use-case completeness
- layout-to-story alignment
- architecture-to-requirement alignment
- implementation-plan-to-architecture alignment
- prototype-to-plan alignment
- test-plan-to-feature alignment

## 16.3 Validation result model
`ValidationRun`
- TargetType
- TargetId
- ValidationKind
- ChecklistVersion
- Status
- ScoreJson
- Summary
- StartedAt
- CompletedAt

`ValidationFinding`
- Severity
- FindingCode
- Title
- Detail
- RecommendedAction
- OwnerRole
- Status

## 17. Search and activity architecture

## 17.1 Activity
Every meaningful action publishes an activity entry:
- project created
- resource added
- prompt saved
- prompt used
- validation completed
- provider health changed
- job failed or completed

## 17.2 Search
Use a search abstraction:
- `ISearchIndexer`
- `ISearchQueryService`

Initial implementation:
- relational `SearchDocument` records with normalized text and filters

Later optional implementations:
- SQLite FTS
- PostgreSQL full-text
- vector search

## 18. Background processing architecture

## 18.1 Queue model
Use:
- `IBackgroundJobQueue`
- `BackgroundService` workers
- persisted job records for visibility and recovery

## 18.2 Job types
- file indexing
- preview generation
- resource validation
- provider health check
- repository sync metadata
- prompt evaluation
- screenshot ingestion
- evidence import

## 18.3 Safety model
Jobs may read and process sensitive data, so:
- logs must be redacted
- unsafe execution jobs require approval artifacts
- job inputs should use references instead of raw secret values where possible

## 19. Safe execution architecture

CanDoItAll may eventually trigger or assist actions involving:
- PowerShell
- Docker
- SSH
- FTP

These areas must be fenced.

### Required approach
- separate “store” from “execute”
- execution commands are explicit and approval-gated
- preview the command/action before approval
- never let AI-generated text auto-run system commands silently
- keep execution adapters behind feature flags if needed

## 20. Microservice and sidecar evolution path

## 20.1 Likely first extraction candidates
- heavy file parsing/indexing
- repository scanning
- screenshot/test evidence processing
- automation/execution workers

## 20.2 Extraction readiness rules
Each module should be extractable because:
- contracts are explicit
- integration events are serializable
- read/write flows are already separated conceptually
- UI depends on contracts, not internal persistence details

## 20.3 Expected future topologies
### Option A — In-process only
All modules remain in the monolith.

### Option B — Local sidecars
Heavy worker modules run as separate local .NET worker services or tools.

### Option C — Hybrid
UI/control plane remains local, while selected services move remote.

The v1 architecture must support all three options later.

## 21. Database design summary

### Major tables
- Workspace_Settings
- Workspace_ProviderProfiles
- Security_SecretRecords
- Projects_Projects
- Projects_ProjectPhases
- Projects_ProjectOptionSelections
- Workbench_WorkbenchSessions
- Workbench_WorkbenchTabs
- Workbench_ProjectStructureManifests
- Workbench_ProjectStructureLinks
- Workbench_ProjectCalendarEvents
- Resources_ProjectResources
- Resources_ResourceValidationRecords
- Prompts_PromptArtifacts
- Prompts_PromptVersions
- Prompts_PromptCollections
- Prompts_PromptUsageRecords
- Factory_PromptBlueprints

Manager watch history, capsule artifacts, and tuning attachments should remain outside the main product database unless a later requirement demands durable cross-session analytics.
- Factory_PromptBuildSessions
- Validation_ValidationRuns
- Validation_ValidationFindings
- TestLab_TestPlans
- TestLab_TestRunRecords
- TestLab_EvidenceArtifacts
- Activity_ActivityEntries
- Activity_SearchDocuments
- Automation_QueuedJobs
- Automation_JobExecutionRecords

## 22. API and service boundaries

Even without external APIs in v1, treat module services as internal APIs:
- project query service
- prompt query service
- provider execution service
- validation service
- search service
- activity service
- job queue service

This keeps the UI from talking directly to persistence details.

## 23. Recommended conventions

### 23.1 Naming
- singular aggregate names
- module-prefixed table names
- feature-folder route names
- explicit DTO names for UI read models

### 23.2 Validation
- use data annotations for simple UI form validation
- use richer validators in application services for business rules
- keep cross-field rules outside simple DTO attributes where needed

### 23.3 Logging
- structured logs
- correlation id per UI action or background job
- secret redaction everywhere
- no full prompt or response payload logs by default

### 23.4 Configuration
Use strongly typed options:
- provider settings
- storage settings
- file handling limits
- safety policy settings
- job processing settings
- workbench tab sleep settings
- browser restore and snapshot settings
- development manager watch settings
- capsule generation and coverage settings
- tuning mode settings

## 24. Architecture decisions record (summary)

### ADR-01
**Decision:** Use modular monolith first.  
**Reason:** Best balance between delivery speed, control, and future growth.

### ADR-02
**Decision:** Use Blazor Web App with Interactive Server as the primary UI runtime.  
**Reason:** Strong fit for local-first UI plus direct access to server-side services and integrations.

### ADR-03
**Decision:** Use EF Core with SQLite/PostgreSQL and `IDbContextFactory`.  
**Reason:** Strong portability, testability, and suitability for Blazor Server concurrency patterns.

### ADR-04
**Decision:** Treat secrets as separate protected records.  
**Reason:** Safer than embedding raw credentials in project/resource tables.

### ADR-05
**Decision:** Use a generalized resource model with a type descriptor registry.  
**Reason:** Satisfies current breadth of asset types and future extensibility.

### ADR-06
**Decision:** Build an internal application-tab workspace with browser-state restore.  
**Reason:** Interactive Server needs deliberate tab lifecycle control; browser tabs are too expensive and too weak for the intended workstation model.

### ADR-07
**Decision:** Wrap the documented JavaScript project-structure and calendar engines before attempting deeper rewrites.  
**Reason:** The proven canvas and calendar engines already exist and should be reused through typed Blazor contracts first.

### ADR-08
**Decision:** Separate prompt library from prompt factory.  
**Reason:** Managing prompts and generating prompts are related but distinct capabilities.

### ADR-09
**Decision:** Treat shared prompt blocks and prompt-flow templates as centrally governed Factory assets.
**Reason:** Repeated delivery instructions must be improved once and reused everywhere instead of copied into page-local strings.

### ADR-10
**Decision:** Keep canvas and calendar JavaScript engines as rendering and interaction adapters only.
**Reason:** Business logic, orchestration, validation, persistence, and command semantics must stay testable and authoritative in C#.

### ADR-11
**Decision:** Use rule-first validation with optional AI augmentation.  
**Reason:** Keeps the system trustworthy and testable.

### ADR-12
**Decision:** Implement a separate local development manager using official `dotnet watch`, loopback-only APIs, and a runtime readiness probe.
**Reason:** The agent-development loop needs trustworthy machine-readable signals without polluting the product runtime model.

### ADR-13
**Decision:** Require structured source capsules for significant components and types, then generate Codex-optimized artifacts from them.
**Reason:** Agent context must stay short, current, and close to the real source instead of drifting into stale manual documentation.

### ADR-14
**Decision:** Keep tuning mode explicitly development-only and route it through the manager with correlation ids and watch-ready confirmation.
**Reason:** This maximizes iteration speed without turning local automation into an unsafe or ambiguous workflow.

## 25. Architecture conclusion

CanDoItAll should be built as a **modular, local-first, C#-centric Blazor workstation** with:
- a unified shell
- an internal recoverable tab workspace
- a project structure workbench
- a project events calendar
- a separate local development manager for watch readiness, capsules, and tuning orchestration
- governed source capsules that stay near the real code
- typed project resources
- a strong prompt domain
- centrally governed shared prompt blocks and prompt-flow templates
- secure secret handling
- provider-agnostic model integration
- review and evidence workflows
- background processing
- sidecar-ready boundaries

This architecture fully supports the current requested scope while remaining credible for significant future growth.
