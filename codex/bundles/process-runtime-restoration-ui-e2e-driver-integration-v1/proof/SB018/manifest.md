# SB018 Proof Manifest

Status: Passed.

## Scope

Gate F covers process run creation reliability and generic persistence for `P06: Process run creation and persistence`.

No production runtime, driver, Core, UI, scheduler, workflow, shell, Office, Graph, workspace/storage, or process mutation implementation code was changed in SB016-SB018. The only source change for this gate is focused integration test coverage in `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`.

## Command Transcripts

- `bundle://proof/SB016/transcripts/process-run-persistence-source-assertions.txt`
- `bundle://proof/SB017/transcripts/process-run-start-guard-source-assertions.txt`
- `bundle://proof/SB018/transcripts/focused-process-run-creation-persistence-tests.txt`
- `bundle://proof/SB018/transcripts/anti-stub-process-run-creation-tests.txt`
- `bundle://proof/SB018/transcripts/forbidden-drift-scan.txt`
- `bundle://proof/SB018/transcripts/prepared-validator-after-sb018.txt`
- `bundle://proof/SB018/transcripts/changed-file-hashes.txt`

## Source Assertions

- `StartRunAsync_SB018_INV_001_persists_project_context_runtime_rows_and_dispatch_outbox` starts a real test application, saves and publishes a project-scoped definition, starts a run with explicit `ProcessProjectStructureContext`, and asserts the persisted run, step runs, work briefs, journal entry, outbox records, and read model.
- The positive test verifies `ProcessProjectStructureContextFormatter.TryParse` can round-trip the persisted input context from `ProcessRun.TriggerReason`.
- The positive test verifies step creation statuses: first root step `Ready`, downstream dependent step `Pending`.
- The positive test verifies dispatch eligibility is not just in-memory by asserting both `start-run` and `dispatch-run-automation` outbox records exist for the run.
- `StartRunAsync_SB018_INV_002_rejects_invalid_not_ready_and_duplicate_start_attempts` rejects missing/unpublished process definitions, draft launch execution, and duplicate execution of an already-generated launch plan.

## Test Proof

`dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~StartRunAsync_SB018_INV"` passed with 2 tests:

- `StartRunAsync_SB018_INV_001_persists_project_context_runtime_rows_and_dispatch_outbox`
- `StartRunAsync_SB018_INV_002_rejects_invalid_not_ready_and_duplicate_start_attempts`

## Anti-Stub And Adversarial Proof

- The anti-stub audit confirms the new tests use `TestApplication.CreateAsync`, real DI services, `ProcessesService`, `IDbContextFactory<AppDbContext>`, service calls, persisted database reads, and explicit success/failure assertions.
- The audit rejects mock, substitute, test-server, fake, stub, bundle-path, and sleep patterns inside the new methods.
- The negative test rejects shallow happy-path proof by asserting exact failure codes for unpublished definitions, not-ready launch plans, and duplicate launch execution.

## Forbidden Drift

`bundle://proof/SB018/transcripts/forbidden-drift-scan.txt` confirms:

- no production source files under `repo://src` changed in SB016-SB018;
- the only source implementation change for this gate is integration test coverage;
- no runtime host, registry, selector, driver DI registration, manager command, scheduler/workflow hook, shell execution, Office/Graph call, workspace/storage write, or read-only driver mutation path was introduced by this gate.

## Changed-File Hashes

See `bundle://proof/SB018/transcripts/changed-file-hashes.txt`.

## Downstream Dependency Check

SB019-SB021 can rely on durable process run creation, persisted step statuses, project-context trigger persistence, work-brief context propagation, journal creation, and dispatch outbox eligibility. Scenario phases can rely on duplicate launch execution being blocked explicitly.
