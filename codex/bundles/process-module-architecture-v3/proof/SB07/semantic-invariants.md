# SB07 Semantic Invariant Contract

## Status

Satisfied on 2026-06-15.

## Invariants

| Invariant | Evidence | Negative Proof |
| --- | --- | --- |
| SB07-INV-001: Runtime state can change only through `ProcessRuntimeEngine` transition methods and the unit-of-work commit boundary. | `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.cs:13`, `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.Commit.cs:9` | Terminal activation rejection test at `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs:53`; anti-stub audit at `bundle://proof/SB07/anti-stub-audit.txt`. |
| SB07-INV-002: Runtime project stays persistence-port-only and does not reference EF, SQL, UI, Git, modules, agents, workflows, or automation. | `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimePorts.cs:6`, CodeAnalytics snapshot `snap-20260615194513-574a5aa6` | `bundle://proof/SB07/runtime-forbidden-dependency-scan.txt`, CodeAnalytics persistence query reports no DbContexts/entities/diagnostics. |
| SB07-INV-003: Scheduler emits dispatchable work only when dependencies and required artifacts are satisfied and the step has no active unexpired claim. | `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimeScheduler.cs:8` | Scheduler test at `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs:68`. |
| SB07-INV-004: Dispatcher does not decide branch, recovery, manager, artifact validity, or runtime state mutation. | `repo://src/CanDoItAll.Processes.Runtime/ProcessStrategyDispatcher.cs:9` | `bundle://proof/SB07/dispatcher-domain-decision-scan.txt`, dispatcher test at `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs:326`. |
| SB07-INV-005: Duplicate strategy results are idempotent and do not trigger a second commit. | `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.Results.cs:7` | Duplicate result test at `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs:172`. |
| SB07-INV-006: Expired or lost claims cannot apply strategy results. | `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.Claims.cs:6`, `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.Results.cs:7` | Lost-claim test at `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs:203`. |
| SB07-INV-007: Cancellation is explicit: no open claims means terminal cancellation; open claims move the run to cancel-requested until work drains. | `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.cs:100` | Cancellation tests at `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs:237` and `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs:255`. |
| SB07-INV-008: Every accepted transition emits runtime events and outbox records, and artifact-producing results emit ledger rows tied to the causal event. | `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.Commit.cs:35`, `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.ResultHelpers.cs:62` | Event/outbox/artifact test at `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs:131`. |
| SB07-INV-009: Runtime does not recreate the archived `ProcessStepRun` or `ProcessRunAutomationDispatch` model. | Active runtime source and tests | `bundle://proof/SB07/old-symbol-scan.txt`. |
| SB07-INV-010: Runtime hot-path code does not introduce the listed .NET performance antipatterns. | `repo://codex/bundles/process-module-architecture-v3/architecture/19-dotnet-performance-guardrails.md`, runtime source | `bundle://proof/SB07/performance-scan-summary.json`; `.Result` matches are typed `command.Result` property access, not blocking task result access. |

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative Test / Scan |
| --- | --- | --- | --- | --- |
| `RuntimeCommandId` and command context | Runtime command callers | Runtime engine and idempotency/persistence adapters | Created per command; UTC timestamp validation is enforced before transition logic | `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimeCommands.cs:6`, `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.Helpers.cs:133` |
| `ProcessRuntimeMutation` | Runtime transition handlers | Unit of work, event store, outbox, artifact ledger adapters | Applied mutations commit; duplicate/rejected mutations return without persistence commit | `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimeState.cs:103`, `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.Commit.cs:9` |
| `DispatchClaimState` | Claim lifecycle methods | Scheduler/runtime result application | Claimed, renewed, expired, reclaimed, completed, or cancelled with explicit owner/token state | `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs:94` |
| `StrategyResultReceipt` | Result submission | Duplicate detection | Recorded for accepted strategy results and reused for duplicate detection | `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs:172` |
| `ProcessRuntimeEventEnvelope` | Runtime mutation builder | Runtime event store and outbox projection subscribers | Validated before commit and paired with outbox messages | `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs:131` |
| `ProcessArtifactLedgerEvent` | Artifact-producing result submission | Artifact ledger persistence/projections | Created only when produced artifacts exist and linked to the result event id | `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimeEngine.ResultHelpers.cs:62` |
| `DispatchWorkItem` | Scheduler | Dispatcher | Created from immutable plan binding and runtime step state; dispatcher invokes only the selected strategy factory | `repo://tests/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs:326` |

## Validation Commands

```text
dotnet build tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --nologo
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build --nologo --filter "FullyQualifiedName~ProcessRuntimeEngineTests|FullyQualifiedName~ProcessInstancePlanCompilerTests|FullyQualifiedName~ProcessDriverAbstractionTests|FullyQualifiedName~ProcessTemplateGitFoundationTests|FullyQualifiedName~ProcessCoreKernelTests|FullyQualifiedName~ProcessModuleBoundaryTests"
dotnet build CanDoItAll.slnx --nologo
```

Results are captured in `bundle://proof/SB07/build-unit-sb07.txt`, `bundle://proof/SB07/test-unit-sb07.txt`, and `bundle://proof/SB07/build-solution-sb07.txt`.
