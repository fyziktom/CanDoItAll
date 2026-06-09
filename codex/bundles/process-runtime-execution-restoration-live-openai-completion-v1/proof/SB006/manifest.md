# SB006 Proof Manifest

## Status
Completed.

## Objective
Gate B: prove run lifecycle creation and duplicate guards from source-backed integration evidence.

## Owned Requirements And Notes
- Requirement IDs: REQ-001, REQ-002, REQ-003, REQ-004, REQ-015 runtime lifecycle subset.
- Critical invariant contract: `bundle://proof/SB006/semantic-invariants.md`
- Downstream dependency: SB007-SB009 may reason about outbox draining only after SB006 proves persisted run, step, project-context, and dispatch-outbox creation.

## Changed File Hashes
| File | SHA-256 |
| --- | --- |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/reviews/01-execution-report.md` | `653ab51d43b279468ad6cac0e61e9b61c2f815619462a4e98b0d1baead53e2ce` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/subbundles/SB006/README.md` | `7cec3ea4653da2bf47e3e9b09a18d9936b7d78a52ef8f2612825b8e4d4935072` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB006/transcripts/gate-b-run-lifecycle-integration-tests.txt` | `24bf2f7eb4f1365931aeb29f4976d3eeb794b8a9d554bc0131af156ca1706b56` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB006/transcripts/gate-b-source-assertions.txt` | `7102f83db2c8a326b8dda690c0ff5034c202c7a08a34cfb4f3320ff9dce86e7b` |
| `repo://codex/bundles/process-runtime-execution-restoration-live-openai-completion-v1/proof/SB006/red-team/duplicate-and-invalid-start-rejection.txt` | `4ad434b6041328a881b50bb1bd97c9bdba52022b27b4f206617d848a20b22dd7` |
| `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs` | `7d109b2009f6846037b4d50d7915613fa5daae6d34fe262a643ce5f5e409d6f2` |
| `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs` | `f9df2054418001cfaec93b75ef7dc35050cdbdd54a82bc161d4529ef05ee470e` |
| `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs` | `599540f916a2499569e791cb1b1f1a93ad6de395ac1a1470b681e768614c9ab9` |

## Command Transcripts
- Integration: `bundle://proof/SB006/transcripts/gate-b-run-lifecycle-integration-tests.txt`
- Source assertions: `bundle://proof/SB006/transcripts/gate-b-source-assertions.txt`
- No transient bundle-path scan: `bundle://proof/SB006/transcripts/no-transient-bundle-path-scan.txt`
- Anti-stub/runtime-host drift scan: `bundle://proof/SB006/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- Red-team duplicate/invalid start rejection: `bundle://proof/SB006/red-team/duplicate-and-invalid-start-rejection.txt`

## Production Behavior Artifact Matrix
| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| `ProcessRun` | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs` | Runtime readback, step orchestration, outbox dispatch, UI/API views | `StartRunAsync` creates active runs only from published and ready launch context | Invalid/premature/duplicate launch paths are rejected by `StartRunAsync_SB018_INV_002_rejects_invalid_not_ready_and_duplicate_start_attempts` |
| `ProcessStepRun` | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs` | Runtime state resolver, step update APIs, run detail UI, automation dispatch | `StartRunAsync` materializes step runs with typed `Ready`, `WaitingApproval`, or `Pending` status according to dependencies | Duplicate launch guard prevents creating a second step graph for the same launch plan |
| Project-structure runtime context | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs` | Project structure navigation, generated output readback, later UI proof | Start persists process run context and managed project structure links for downstream output projection | Published-version and not-ready launch guards prevent context creation without an executable process definition |
| `ProcessOutboxRecord` | `repo://src/CanDoItAll.Modules.Processes/Runtime/ProcessOutbox.cs` | SB007-SB009 outbox drain and hosted worker proof | Start enqueues durable dispatch work as `Pending`; later subbundles own claim/drain/finalization behavior | SB006 only proves enqueue existence; dispatch claim behavior remains blocked until SB007-SB009 |

## Closure
- Shallow-pass trap: A fake pass could assert that the launch API returns a run ID without proving persisted runtime rows, project context, dispatch outbox, and duplicate guards.
- Adversarial negative proof: `bundle://proof/SB006/red-team/duplicate-and-invalid-start-rejection.txt`
- Semantic positive proof: `bundle://proof/SB006/transcripts/gate-b-run-lifecycle-integration-tests.txt` plus `bundle://proof/SB006/transcripts/gate-b-source-assertions.txt`
- Anti-stub audit: `bundle://proof/SB006/transcripts/anti-stub-and-runtime-host-drift-scan.txt`
- Raw-note closure: Runtime lifecycle creation is source-backed; dispatch execution, live provider behavior, and UI proof remain owned by later subbundles.
