# 02 — General Technical Requirements

## 1. Scope statement

CanDoItAll is a local-first, modular software delivery environment focused on project planning, prompt workflows, validation, testing, and related technical assets.

The first release must already support the required project, prompt, provider, validation, and UI capabilities while remaining structurally ready for substantial future expansion.

## 2. Architectural objectives

1. Support current requirements completely without painting the solution into a corner.
2. Keep the first release practical for a single local workstation.
3. Stay modular enough for future extraction into sidecars or microservices.
4. Keep the UI unified even as functional areas expand.
5. Treat sensitive data and external execution paths as high-risk areas.
6. Enable Codex-friendly implementation through strong structure and explicit constraints.

## 3. Functional requirements

## 3.1 Workspace and configuration

- **FR-001** — The application shall support configuration of one primary local workspace.
- **FR-002** — The application shall support global settings for database provider, storage root, and default prompt settings.
- **FR-003** — The application shall support provider profiles for OpenAI, Ollama local, and Ollama remote.
- **FR-004** — The application shall support secure storage of API keys, passwords, private keys, and other secrets.
- **FR-005** — The application shall support health checks for provider and connector profiles.
- **FR-006** — The application shall support future multi-user policy design without requiring it in v1.

## 3.2 Persistence and storage

- **FR-007** — The application shall support EF Core persistence with SQLite and PostgreSQL.
- **FR-008** — The application shall support `IDbContextFactory`-based data access.
- **FR-009** — The application shall support design-time context creation for migrations.
- **FR-010** — The application shall support an in-memory database option for tests and development scenarios.
- **FR-011** — The application shall support hybrid storage where metadata is stored in the database and selected content is stored in the file system.
- **FR-012** — The application shall support per-project or per-resource storage policies.
- **FR-013** — The application shall support audit records for key business actions.

## 3.3 Project management

- **FR-014** — The application shall support project creation with name, description, dates, phases, and status.
- **FR-015** — The application shall support custom and predefined project phases.
- **FR-016** — The application shall support project stack profiling including primary language, secondary languages, DB type, UI type, external APIs, and storage strategy.
- **FR-017** — The application shall support notes on every relevant project option.
- **FR-018** — The application shall support project dashboard summaries and next-step recommendations.

## 3.4 Resource and connector management

- **FR-019** — The application shall support linked resources for folder, file, web link, FTP, PowerShell script, repository, Docker/Docker Compose, SSH, secrets, and prompt.
- **FR-020** — The application shall support a generalized resource model with resource-type-specific configuration.
- **FR-021** — The application shall support connection/profile reuse across projects.
- **FR-022** — The application shall support validation status, indexing status, and sensitivity classification for resources.
- **FR-023** — The application shall support file preview for high-value file types and generic handling for unsupported files.
- **FR-024** — The application shall support linking secret references instead of copying secret values into resource records.

## 3.5 Prompt library

- **FR-025** — The application shall support prompt drafts, templates, blueprints, final prompts, and prompt collections.
- **FR-026** — The application shall support prompt versioning.
- **FR-027** — The application shall support prompt tagging and search.
- **FR-028** — The application shall support prompt usage history linked to project, phase, time, provider, and repository context.
- **FR-029** — The application shall support prompt cloning and reuse.

## 3.6 Prompt factory and generation

- **FR-030** — The application shall support guided prompt generation by project phase.
- **FR-031** — The application shall support automatic prompt assembly from project metadata, option selections, notes, and linked resources.
- **FR-031A** — The application shall support a centrally managed catalog of shared prompt blocks that can be reused across phases, blueprints, and projects.
- **FR-031B** — The application shall support prompt flow templates composed from shared blocks plus project- or phase-specific setup data.
- **FR-031C** — The application shall support storing prompt-flow node state such as pending, prepared, running, used, skipped, failed, validated, and superseded.
- **FR-031D** — The application shall support multiple concurrent prompt runs or branches for the same project or feature while preserving traceability.
- **FR-031E** — The prompt wizard shall automatically apply recommended shared prompt blocks based on phase, flow template, or blueprint while still allowing controlled user customization.
- **FR-032** — The application shall support editable generated prompts before persistence or sending.
- **FR-033** — The application shall support saving partial prompt work.
- **FR-034** — The application shall support exporting prompts to clipboard or file.
- **FR-035** — The application shall support submitting prompts to selected LLM providers when the user chooses to do so.
- **FR-036** — The application shall support validation and warning checks before an external send operation.

## 3.7 Validation and review

- **FR-037** — The application shall support validation of user stories and use cases.
- **FR-038** — The application shall support validation of ASCII layouts against stories and use cases.
- **FR-039** — The application shall support validation of architecture against requirements.
- **FR-040** — The application shall support validation of implementation plans against the architecture.
- **FR-041** — The application shall support validation of prototype outputs against implementation plans.
- **FR-042** — The application shall support test planning and test coverage mapping.
- **FR-043** — The application shall support storage of findings, actions, and review outcomes.

## 3.8 Testing and evidence

- **FR-044** — The application shall support test evidence records including screenshots and linked test runs.
- **FR-045** — The application shall support a test lab view that organizes planned, implemented, and executed tests.
- **FR-046** — The application shall support linking Playwright scenarios and results to stories, phases, and features.

## 3.9 UI and extensibility

- **FR-047** — The application shall provide functional UI coverage for all listed capabilities in v1.
- **FR-048** — The application shall use Tailwind CSS and a unified component strategy.
- **FR-049** — The application shall support adding new resource types and prompt phases without redesigning core flows.
- **FR-050** — The application shall remain structurally ready for future sidecar services or microservices.
- **FR-051** — The application shall provide an internal application-tab workspace instead of depending on many browser tabs for concurrent work.
- **FR-052** — The application shall support opening, closing, pinning, reordering, and reactivating internal tabs.
- **FR-053** — The application shall support active, background, and sleeping tab states for heavy work surfaces.
- **FR-054** — The application shall persist internal tab session state in browser storage and restore it after refresh, reconnect, close, or crash.
- **FR-055** — The application shall provide a project structure canvas for visualizing phases, resources, prompts, validations, tests, and relationships.
- **FR-056** — The project structure canvas shall support opening linked artifacts from the canvas into internal tabs.
- **FR-057** — The project structure canvas shall support representing prompt sessions and prompt steps, including branching from an existing step into a new follow-up prompt.
- **FR-057A** — The project structure canvas shall support representing prompt flow templates, reusable prompt blocks, and prompt-run nodes with visible execution state.
- **FR-057B** — Canvas and calendar JavaScript layers shall be limited to rendering, interaction capture, viewport behavior, and visual-only overlays; authoritative business logic, validation, persistence, and command execution shall remain in C# services and models.
- **FR-057C** — The project structure canvas shall provide a grouped hexagonal context menu pattern that supports primary actions and chained subcommands.
- **FR-057D** — If the canvas renders edit popups or modals for visual quality, those surfaces shall still submit intent to C# command handlers rather than owning business mutations themselves.
- **FR-058** — The application shall provide a project events calendar linked to milestones, phases, deadlines, validations, and related project artifacts.
- **FR-059** — The project events calendar shall support opening linked artifacts into internal tabs.
- **FR-060** — The shared UI architecture shall include shell-level components for the internal tab strip, sleeping-tab indicators, workbench inspectors, date and time editing, and canvas host surfaces.

## 3.10 Development acceleration and adaptive tuning

- **FR-061** — The solution shall provide a separate local development manager that can supervise `dotnet watch` for the main application.
- **FR-062** — The development manager shall normalize watch, build, hot reload, restart, and runtime-fault states into a machine-readable contract.
- **FR-063** — The development manager shall expose a loopback-only local OpenAPI for watch status, log history, capsule summaries, and tuning-request status.
- **FR-063A** — The development manager shall expose the active local application URLs that are valid for browser verification after each ready cycle.
- **FR-064** — The development manager shall expose an event stream suitable for waiting on watch readiness, capsule refresh completion, and tuning-request progress.
- **FR-065** — The main application shall expose a development-only runtime readiness endpoint so the manager can confirm actual application readiness instead of trusting console parsing alone.
- **FR-066** — The solution shall require short structured capsule comments on significant handwritten components and C# types, with explicit skip markers for approved exemptions.
- **FR-067** — The development manager shall watch source changes and incrementally generate Codex-optimized capsule artifacts from those comments.
- **FR-068** — The development manager shall report missing, malformed, or stale capsules through a coverage and drift contract.
- **FR-069** — The UI shall support a dev-only tuning mode in which a user can target a specific component or workbench surface from the running application.
- **FR-070** — A tuning request shall support route, project, tab, selection, capsule, screenshot, and free-form instruction context.
- **FR-071** — The development manager shall support tracked local Codex job orchestration for approved tuning requests inside the configured workspace boundary.
- **FR-072** — A tuning request shall not be marked ready for review until the Codex job has completed and the watched application is ready again.
- **FR-073** — The development manager shall retain local history tying tuning requests to watch events, changed files, and verification outcomes.
- **FR-074** — Generated artifacts, logs, and attachments created by the manager shall be excluded from self-triggering application rebuild loops.

## 4. Non-functional requirements

## 4.1 Architecture and maintainability

- **NFR-001** — The solution shall use a modular architecture with clear boundaries and explicit contracts.
- **NFR-002** — Each module shall own its business rules and persistence mappings.
- **NFR-003** — Cross-cutting concerns shall be centralized and reusable.
- **NFR-004** — The solution shall be readable and implementable by Codex in sequential slices.
- **NFR-005** — The design shall minimize framework lock-in where practical.

## 4.2 Security and privacy

- **NFR-006** — Secret values shall be encrypted at rest.
- **NFR-007** — Sensitive values shall never be written to application logs.
- **NFR-008** — External execution-capable actions shall require explicit user approval.
- **NFR-009** — The design shall support future policy-based authorization.
- **NFR-010** — Prompt send operations shall clearly indicate what data is leaving the machine.

## 4.3 Reliability and data integrity

- **NFR-011** — The application shall favor deterministic validation for business rules.
- **NFR-012** — State transitions shall be auditable.
- **NFR-013** — Long-running tasks shall not block the UI thread or degrade the workspace experience.
- **NFR-014** — Background failures shall surface as actionable diagnostics.
- **NFR-015** — The design shall support idempotent processing of internal events where relevant.
- **NFR-015A** — Internal tab restore shall degrade safely when one snapshot is incompatible or corrupted.
- **NFR-015B** — Canvas and calendar workbench surfaces shall restore enough user state to resume work meaningfully after interruption.
- **NFR-015C** — Prompt-flow restore shall preserve branch identity and node state without duplicating or silently dropping steps after interruption.

## 4.4 Performance

- **NFR-016** — Common project and prompt screens shall load quickly under ordinary local use.
- **NFR-017** — Large file handling shall degrade gracefully through previews, indexing, or references rather than forcing full in-memory processing.
- **NFR-018** — Prompt generation should feel interactive, with visible progress when remote operations take time.
- **NFR-019** — Search should remain acceptable across many projects and prompts within a single-user workspace.
- **NFR-020** — The architecture shall allow later optimization of search, indexing, and provider calls without redesigning business modules.
- **NFR-020A** — The internal tab workspace shall reduce browser-tab and Interactive Server circuit pressure by allowing heavy screens to sleep.
- **NFR-020B** — Tab wake and restore should feel fast enough that users prefer internal tabs over opening additional browser tabs.
- **NFR-020C** — Canvas interactions should feel immediate through the JavaScript renderer while preserving C# as the authoritative command and state layer.

## 4.5 Testability and observability

- **NFR-021** — Domain and application logic shall be unit-testable without UI concerns.
- **NFR-021A** — Prompt-flow orchestration, canvas command dispatch, and context-menu action routing shall be unit-testable without the JavaScript renderer.
- **NFR-022** — Persistence behavior shall be integration-testable with SQLite and PostgreSQL-compatible flows.
- **NFR-023** — UI components shall be component-testable.
- **NFR-024** — End-to-end workflows shall be testable with Playwright.
- **NFR-025** — Logs, health checks, and activity history shall help diagnose failures without exposing secrets.
- **NFR-026** — The development manager shall provide stable ready or not-ready semantics even if raw watch output wording changes between SDK versions.
- **NFR-027** — Capsule generation should be incremental and fast enough to remain part of the normal edit loop.
- **NFR-028** — One malformed capsule, failed tuning request, or failed watch cycle shall not permanently disable the development manager.
- **NFR-029** — The development manager API shall remain local-only, workspace-bounded, and token-protected for mutating operations.
- **NFR-030** — Tuning requests, watch logs, and capsule artifacts shall avoid storing secrets or raw sensitive payloads unless explicitly approved and redacted.

## 5. Constraints

## 5.1 Mandatory technology constraints
- C# is the primary implementation language wherever reasonably possible.
- Target runtime is .NET 10.
- Main application shell is a Blazor Web App using Interactive Server rendering.
- Styling uses Tailwind CSS and a shared custom component strategy.
- The architecture must integrate the existing `CanDoItAll.Components` library and document missing shell or workbench components explicitly.
- The architecture must include a separate local development manager using official `dotnet watch` and local ASP.NET Core APIs for the agent-feedback loop.
- Shared prompt blocks and prompt-flow templates must be centrally managed artifacts, not page-local strings copied between screens.
- The canvas and calendar JavaScript engines must remain rendering or interaction adapters; business rules, orchestration, persistence, and validation stay in C#.
- Persistence uses EF Core with SQLite and PostgreSQL support.
- The solution must support `IDbContextFactory`, design-time factory, and in-memory test mode.
- The system must support OpenAI and Ollama integrations.

## 5.2 Delivery constraints
- The first release must already feel complete enough to be used productively.
- The architecture must not assume a cloud-only deployment.
- The architecture must not require a large distributed system in v1.
- The solution must remain approachable for one lead developer plus agent assistance.

## 5.3 Security constraints
- Do not store credentials in plain text.
- Do not mix raw secrets into prompt history by default.
- Do not allow agent-created code to auto-execute system-level actions without approval.
- Do not let any provider integration define the internal domain model.

## 6. Assumptions for v1

1. The primary runtime is a trusted local machine.
2. A single local workspace is sufficient for the first release.
3. Multi-user and remote collaboration are later concerns but not ignored.
4. The app may eventually manage heavy background work and optional sidecars.
5. Not every resource type needs full semantic preview in v1, but all required types need supported registration and management.
6. Internal tabs, workbench restore, project structure canvas, and project calendar are part of the first serious usable version, not future polish.
7. The local development manager, source capsules, and dev-only tuning loop are part of the intended build velocity strategy, not optional late tooling.
8. Shared prompt blocks, prompt-flow templates, and their canvas representation are part of the first serious usable prompt-workflow version, not optional polish.

## 7. Recommended technical principles

1. **Modular monolith first**  
   Build strong boundaries now, extract only when the pressure is real.

2. **Local-first with selective remote integration**  
   Use the local machine as the default control plane; external providers are optional tools, not architectural masters.

3. **Strong typing where the business is stable, generalized models where the catalog evolves**  
   This is especially relevant for project stack options and resource types.

4. **Metadata in relational storage, heavyweight content in file storage**  
   Keep the relational model crisp.

5. **Secrets as first-class citizens**  
   Centralize, encrypt, reference, audit.

6. **AI as an augmentation layer, not the domain core**  
   The domain should still function if provider calls fail.

7. **Deterministic validation before AI review**  
   Validation should start with hard rules, then use LLMs for deeper critique or improvement suggestions.

## 8. Data classification

### Class D1 — Public or low-risk metadata
Examples:
- project name
- prompt title
- tags
- UI preferences

### Class D2 — Internal project context
Examples:
- project descriptions
- stack choices
- architecture notes
- linked local paths
- validation findings

### Class D3 — Sensitive operational data
Examples:
- FTP/SSH connection details
- repository tokens
- deployment endpoints
- internal URLs
- script references

### Class D4 — Secret material
Examples:
- passwords
- API keys
- private keys
- certificates
- passphrases

Handling rule:
- D4 must always be encrypted.
- D3 requires careful logging and export handling.
- D2 may be sent externally only with explicit user choice and visible warning.
- D1 is unrestricted within the local app.

## 9. Storage requirements

## 9.1 Data categories
- structured metadata
- prompt bodies and versions
- audit/activity records
- binary attachments
- cached previews / extracted text
- screenshots and test evidence
- provider profile definitions
- secret payloads
- search documents

## 9.2 Storage strategy rules
- Store canonical metadata in the database.
- Store large binary or file-derived artifacts in the file system under a managed workspace root.
- Store secret payloads encrypted.
- Store resource references separately from captured snapshots.
- Support both “reference only” and “managed copy” patterns.

## 9.3 Storage policy options
- `ReferenceOnly`
- `MetadataOnly`
- `MetadataPlusCache`
- `ManagedFileCopy`
- `HybridManaged`

## 10. Integration requirements

## 10.1 LLM providers
The integration layer must:
- support multiple provider profiles
- support local and remote endpoints
- expose capability flags per provider/model
- support request logging without logging sensitive content by default
- support streaming-friendly UX
- remain provider-agnostic at the application boundary

## 10.2 Connector profiles
The integration layer must support typed profiles for:
- FTP
- SSH
- repository
- local path
- web link
- Docker/Docker Compose
- PowerShell
- secret references

## 10.3 Future service integration
The application must stay ready for:
- parser worker sidecar
- repository scanner sidecar
- screenshot/testing sidecar
- execution/automation sidecar
- remote API extraction of heavy modules

## 10.4 Immediate local development-manager integration
The solution must also support an immediate local development sidecar that:
- supervises `dotnet watch`
- confirms runtime readiness through a development-only endpoint
- exposes loopback-only OpenAPI and SSE contracts
- generates capsule artifacts from source comments
- coordinates dev-only tuning requests

## 11. UX-to-technical implications

1. Because the UI is project-centered, the backend must expose fast project summary read models.
2. Because the resource system is typed but extensible, a descriptor registry is required.
3. Because prompts are reusable and versioned, immutable versions plus mutable drafts are required.
4. Because validations are traceable, validation runs need durable records.
5. Because the system is local-first, file and process boundaries must be treated as part of the core architecture.
6. Because the application uses Interactive Server rendering, internal workspace tabs must be application-managed and recoverable.
7. Because prompt workflows can branch, the project structure surface must represent prompt sessions and prompt steps as first-class linked artifacts.
8. Because Codex and Playwright need trustworthy timing, the development manager must expose explicit ready semantics instead of relying on arbitrary sleeps.
9. Because agent context drifts quickly, compressed source capsules must be treated as maintainable product-adjacent artifacts.

## 12. Validation and quality requirements

The delivered system is acceptable only if:
- every required capability has a corresponding UI entry point
- project creation and prompt generation can be demonstrated end-to-end
- secrets remain protected across create/read/update/log/export flows
- at least one project can link every required resource type
- prompt usage history is queryable
- validation results can be stored and revisited
- sidecar growth paths are documented and not blocked by current design
- automated tests cover domain, integration, UI, and end-to-end layers

## 13. Requirements prioritization

### Must-have in v1
- workspace settings
- provider profiles
- secret management
- project creation and editing
- typed resource linking
- prompt gallery
- prompt factory wizard
- validation center
- project activity history
- functional UI shell
- EF Core persistence
- SQLite and PostgreSQL support
- Tailwind-based component use
- testing baseline

### Should-have in v1
- model capability metadata
- search index abstraction
- screenshot evidence records
- repository-aware prompt usage metadata
- connector health checks
- import/export foundations

### Could-have later
- multi-user auth
- remote collaboration
- vector search
- model evaluation dashboards
- sidecar process supervisor UI
- remote job execution agents
- advanced diff and semantic comparison

## 14. Implementation risk register

### Risk T1 — Scope explosion through connectors
**Risk:** Too many connector-specific edge cases slow delivery.  
**Mitigation:** Use a common descriptor model and build typed adapters incrementally.

### Risk T2 — Over-designed module layering
**Risk:** Too many projects/layers make the codebase heavy.  
**Mitigation:** Use pragmatic module boundaries and only split deeper when needed.

### Risk T3 — Blazor server-state misuse
**Risk:** Long-lived state and concurrency bugs degrade reliability.  
**Mitigation:** Use one `DbContext` per operation, background queues, and explicit UI loading guards.

### Risk T4 — Prompt logic becomes untestable
**Risk:** Template and generation logic drift into ad hoc string concatenation.  
**Mitigation:** Build structured prompt blueprints, validators, and render pipelines.

### Risk T5 — Secrets leak through history or logs
**Risk:** Debugging and activity tracking accidentally expose sensitive values.  
**Mitigation:** Redaction policies, secure wrappers, safe logging, and strict review checklists.

### Risk T6 — Validation depends too much on LLMs
**Risk:** Deterministic quality controls become weak.  
**Mitigation:** Rule-first validation engine with optional LLM review augmentation.

### Risk T7 — Browser-tab overload undermines the workstation model
**Risk:** Users open many browser tabs, multiplying Blazor Server circuits and memory use while losing coherent workspace state.
**Mitigation:** Build the internal tab workspace, sleep policy, and restore path as core architecture.

### Risk T8 — Visual orchestration surfaces are treated as optional polish
**Risk:** The application ships with many disconnected pages but without the structure canvas and calendar that make project control practical.
**Mitigation:** Treat the workbench canvas and calendar as first-class deliverables in requirements, milestones, prompts, and QA gates.

## 15. Final technical conclusion

The application requires:
- a modular, project-centered architecture
- strong handling of typed resources and secrets
- provider-agnostic AI integration
- a hybrid relational + file storage strategy
- deterministic review and testing foundations
- a UI shell designed for future growth

These requirements justify the modular monolith architecture proposed in the next documents.
