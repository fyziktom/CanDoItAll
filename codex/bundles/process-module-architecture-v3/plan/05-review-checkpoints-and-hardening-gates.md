# Review Checkpoints And Hardening Gates

## Gate A Dependency Direction Review

- Core has no EF, Razor, UI, infrastructure, concrete driver, AgentFramework runtime, Git implementation, or storage reference.
- Runtime has no UI or concrete driver reference.
- Dispatcher has no UI, no concrete domain behavior, and no direct runtime state mutation.
- Builder has no UI reference and does not execute strategies.
- Persistence implements ports but is not referenced by Core or UI.
- Git wrapper has no Process-specific behavior.
- Git UI has no Process runtime dependency.
- Process UI references application/projection contracts only.

## Gate B Domain Vocabulary Leak Review

Scan generic projects for domain-specific terms such as framework names, provider names, browser-proof concepts, project-structure-specific rules, handoff implementation names, and workspace tool names. These may appear in docs, examples, concrete drivers, adapter projects, or tests specifically scoped to those drivers.

Scenario terms such as `TetrisGame`, `Tetris`, `RecipePlannerPwa`, `IssueTriageDashboard`, `InvoiceApprovalPortal`, recipe, meal plan, shopping list, SLA badge, invoice approval, game loop, falling-piece, or score storage are allowed only in scenario packs, tests, evidence, validation docs, screenshots, and explicitly scoped fixtures. They are forbidden in generic Process Core, Runtime, Dispatcher, Builder, Manager, Artifact, Monitoring, Template, Projection, and shared API contracts.

## Gate C Old Symbol Leak Review

Search for:

```text
ProcessRunAutomationDispatchService
ProcessesService.StartRunAsync
ProcessObservationService
ProcessObservationCache
ProcessBranchOutcomeRouting
ProcessRecoveryRouter
AgentRecoveryModels
ProcessStepRun
ProcessArtifactRecord
ProcessJournalEntry
ProcessDriverVerificationGateway
current-module.import-envelope
current-module.compatibility-report
```

Allowed matches are reference archive, architecture bundle, migration input, compatibility reports, and tests intentionally named as legacy-reference tests.

## Gate D Refactoring And File-Shape Review

- Inspect newly created large files.
- Split orchestration from pure rules.
- Split strategy contracts from strategy implementations.
- Split EF entities from repositories/projectors.
- Split UI components from data-loading/presenter services.
- Avoid partial monster services.
- Avoid helper-method dumping grounds.
- Keep classes small enough to unit test.
- Add tests for failure paths.

## Gate E Runtime Integrity Review

- Runtime state changes only through validated transitions.
- Every transition emits an event or reliable outbox record.
- Dispatch result submission checks claim token.
- Idempotency keys are used.
- Budgets are consumed through runtime transitions.
- Terminal states cannot be mutated except audit annotations.
- Cancellation and lease expiration are tested.

## Gate F Manager Safety Review

- Manager decisions are events.
- Agent-backed manager output is policy-checked.
- Manager does not mutate runtime state directly.
- Automatic recovery checks approval, budget, idempotency, access, and fingerprint.
- Parent/child messages are durable.
- Raw diagnostics are restricted evidence.
- User-facing incidents are sanitized.

## Gate G UI Projection Review

- Projection contracts exist first.
- UI uses application/projection services.
- UI does not query runtime EF entities.
- UI does not compute runtime truth.
- Live Processes time filtering is tested at query/projection boundary.
- Canvas renders projection fields.
- Restricted diagnostics require authorized links.

## Gate H Template/Git Review

- JSON is canonical.
- Local overrides are patches against base hash.
- Global update uses three-way merge.
- Conflicts are explicit.
- Migrations do not skip intermediate versions.
- Markdown/Mermaid are generated/exported only.
- Git operations go through `CanDoItAll.Git`.
- Paths are authorized and logs sanitized.

## Gate I Subbundle Completion Review

Every future implementation subbundle must end with an execution report including files changed, tests run, tests skipped and why, dependency scan result, domain leak scan result, old-symbol scan result, refactoring review result, known risks, and exact handoff notes for next subbundle.

## Gate J .NET Performance Antipattern Review

- Runtime, dispatcher, manager, projector, adapter, persistence, Git, template, and UI service code is async end-to-end and cancellation-aware.
- No sync-over-async appears in production code: `.Result`, `.Wait()`, and `GetAwaiter().GetResult()` are absent from hot paths.
- No library/runtime service uses `Task.Run` as a fake async wrapper.
- Event/projector pipelines use bounded channels or equivalent bounded queues with explicit overflow/backpressure/dead-letter behavior.
- Hot-path projectors, runtime readers, live snapshot builders, artifact ledger lookups, branch route evaluators, and canvas projection builders avoid LINQ-heavy repeated allocations unless a bounded-data proof is recorded.
- Collections in hot paths are pre-sized when counts are known; read-heavy static lookup tables use frozen collections where appropriate.
- JSON serialization uses source-generated contexts and cached options for templates, events, snapshots, exchange envelopes, artifact ledgers, and Git metadata.
- External HTTP integrations use `IHttpClientFactory` or typed clients; no per-call `HttpClient` creation.
- Template migration and Git operations use async/batched/bounded I/O and checkpoint/resume where needed.
- UI projection queries are paged/windowed/server-filtered; Blazor components do not load all history/events/runs/artifacts and filter locally.
- Leaf implementation classes are sealed unless subclassing is required.
- Every subbundle that touches C# hot-path code records exact performance scan counts from `validation/05-dotnet-performance-antipattern-checklist.md`.

## Gate K Role Candidate Readiness Review

- HR candidate score is advisory and cannot mark a candidate executable.
- Role execution requirements are compiled from roles, steps, operation contracts, artifacts, selected operating mode, driver descriptors, project scope, and manager policy.
- Candidate readiness is stored as a deterministic assessment with requirement set hash, evidence snapshot hash, suitability score breakdown, readiness status, and typed findings.
- Missing required tools, rights, capabilities, provider/workflow bindings, project/resource access, approvals, or direct messaging permissions are represented as typed findings.
- Missing required tools and missing required rights block launch approval/execution by default.
- Provisioning and approval tasks are linked to specific findings.
- Provisioning completion triggers reassessment and does not clear blockers by task status alone.
- Launch UI projections show score and readiness separately.
- Sensitive right/tool evidence is redacted behind restricted evidence links.
- Runtime assignments include readiness assessment hash, requirement set hash, evidence snapshot hash, unresolved warnings, and approved override references.

## Gate L Final E2E Scenario And API Skill Review

- Final E2E scenarios are loaded through public typed APIs, not database edits or hidden test-only stores.
- The Process API surface supports definitions, templates, launch plans, candidate readiness, runs, steps, assignments, artifacts, manager directives, escalations, projections, and project-scoped process links.
- A Codex Process API skill documents route discovery, authorization, enum/ID guidance, scenario loading, run readback, artifact lineage, candidate readiness, and stop conditions.
- `TetrisGame`, `RecipePlannerPwa`, `IssueTriageDashboard`, and `InvoiceApprovalPortal` scenario replay passes or has explicit user-approved disposition.
- Domain leak scans prove scenario vocabulary does not appear in generic Process projects or broad software/.NET driver contracts.
- Final E2E report records API commands or test names, process run ids, artifact status, escalation/recovery state, browser screenshots, and leak-scan output.
