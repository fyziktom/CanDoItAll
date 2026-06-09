# SB021 Proof Manifest

Status: Passed.

## Scope

Gate G covers `P07: Dispatch/claim/route/finalizer runtime restoration`.

No production runtime, driver, Core, UI, scheduler, workflow, shell, Office, Graph, workspace/storage, or process mutation implementation code was changed in SB019-SB021. The gate uses existing deterministic integration coverage to prove the dispatcher can claim work, execute claimed routes, finalize completed steps, project artifacts, and settle the run through durable outbox dispatch.

## Command Transcripts

- `bundle://proof/SB019/transcripts/dispatch-claim-source-assertions.txt`
- `bundle://proof/SB020/transcripts/route-finalizer-artifact-source-assertions.txt`
- `bundle://proof/SB021/transcripts/focused-process-mock-durable-dispatch-e2e.txt`
- `bundle://proof/SB021/transcripts/anti-stub-deterministic-dispatch-e2e.txt`
- `bundle://proof/SB021/transcripts/forbidden-drift-scan.txt`
- `bundle://proof/SB021/transcripts/prepared-validator-after-sb021.txt`
- `bundle://proof/SB021/transcripts/changed-file-hashes.txt`

## Source Assertions

- `Process_mock_workflow_process_completes_end_to_end_through_durable_outbox_dispatch` starts a real application, creates and approves a launch plan, executes it through `ProcessesService.ExecuteLaunchPlanAsync`, and drains the real `ProcessOutboxService`.
- SB019 source assertions prove dispatcher eligibility and claim acquisition paths are present through `ProcessOutbox.ProcessPendingAsync`, PostgreSQL pending-record claims, `TryClaimRecordAsync`, `ProcessClaimedAsync`, `LoadDispatchCandidateHeadersAsync`, `ProcessDispatchGuardLease.WaitAsync`, `TryClaimStepDispatchAsync`, and `ProcessDispatchClaimRequest`.
- SB020 source assertions prove claimed route execution and completion paths are present through `ExecuteClaimedDispatchRouteAsync`, `CreateClaimedDispatchRouteHandlerPipeline().ExecuteAsync`, `TransitionStepWithClaimAsync`, and claim-scoped artifact projection/recovery.
- The deterministic E2E asserts completed run state, completed/skipped step state, branch outcomes, artifact records, decision records, backing execution runs, and completed outbox records with no dead letters.

## Test Proof

`dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~Process_mock_workflow_process_completes_end_to_end_through_durable_outbox_dispatch"` passed with 1 test:

- `Process_mock_workflow_process_completes_end_to_end_through_durable_outbox_dispatch`

## Anti-Stub And Adversarial Proof

- The anti-stub audit confirms the E2E uses `CreateEnabledApplicationAsync`, a real DI scope, `ProcessesService`, `ProcessOutboxService`, `ICanDoItAllAgentWorkspaceFactory`, the deterministic `ProcessMockAgentCatalogService`, real launch execution, and real outbox processing.
- The audit rejects bundle-path references, sleeps, `WebApplicationFactory`, `TestServer`, direct update shortcuts, and raw SQL mutation inside the scoped E2E method/helper.
- The negative proof is semantic rather than a separate failure test: the E2E fails if dispatch cannot claim work, if claimed route execution does not run, if finalization cannot transition steps, if required artifacts/decision records are missing, if the process run does not complete, or if any run outbox record dead-letters.

## Forbidden Drift

`bundle://proof/SB021/transcripts/forbidden-drift-scan.txt` confirms:

- no production source files under `repo://src` changed in SB019-SB021;
- no transient `codex/bundles` path references exist in source/test code;
- no runtime host, registry, selector, driver DI registration, manager command, scheduler/workflow hook, shell execution, Office/Graph call, workspace/storage write, or read-only driver mutation path was introduced by this gate.

## Changed-File Hashes

See `bundle://proof/SB021/transcripts/changed-file-hashes.txt`.

## Downstream Dependency Check

SB022-SB027 can rely on a durable dispatch/claim/finalizer baseline. Remaining scenario phases still need representative `.NET app` process proof, business-analysis process proof, driver boundary proof, large-screen runtime UI proof, and runtime host/registry/selector/DI/manager/scheduler/workflow roadmap evidence.
