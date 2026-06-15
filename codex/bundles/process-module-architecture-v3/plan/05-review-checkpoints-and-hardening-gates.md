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
