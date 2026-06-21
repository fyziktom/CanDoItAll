# SB09 Semantic Invariant Contract

## Status

Satisfied on 2026-06-15.

## Invariants

| Invariant | Evidence | Negative Proof |
| --- | --- | --- |
| SB09-INV-001: Manager decisions are explicit events and records, not hidden dispatcher behavior. | `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimeEventTypes.cs:41`, `repo://src/CanDoItAll.Processes.Runtime/ProcessManagerDecisionContracts.cs:5` | `bundle://proof/SB09/manager-safety-review.md`; no direct agent/workflow/domain-driver dispatch exists in manager source. |
| SB09-INV-002: Manager does not mutate runtime state directly. | `repo://src/CanDoItAll.Processes.Application/ProcessManagerRuntimeDependencies.cs:5` | `bundle://proof/SB09/scans/direct-runtime-mutation-scan.txt`; dependency test at `repo://tests/CanDoItAll.Tests.Unit/ProcessManagerControlLoopTests.cs:189`. |
| SB09-INV-003: Raw diagnostics are restricted evidence and normal incident content remains safe. | `repo://src/CanDoItAll.Processes.Runtime/ProcessManagerIncidentContracts.cs:6`, `repo://src/CanDoItAll.Processes.Runtime/ProcessManagerIncidentContracts.cs:17` | Raw diagnostic restriction test at `repo://tests/CanDoItAll.Tests.Unit/ProcessManagerControlLoopTests.cs:158`; `bundle://proof/SB09/scans/raw-diagnostic-projection-scan.txt`. |
| SB09-INV-004: Recovery is idempotent and policy checked before dispatch. | `repo://src/CanDoItAll.Processes.Application/ProcessManagerControlLoop.Recovery.cs:9`, `repo://src/CanDoItAll.Processes.Runtime/ProcessManagerRecoveryContracts.cs:21` | Policy denial test at `repo://tests/CanDoItAll.Tests.Unit/ProcessManagerControlLoopTests.cs:173`; denied recovery records no dispatch and consumes no loop budget. |
| SB09-INV-005: Automatic recovery and backward branch routing cannot loop without budget/fingerprint checks. | `repo://src/CanDoItAll.Processes.Runtime/ProcessManagerRecoveryContracts.cs:49`, `repo://src/CanDoItAll.Processes.Application/ProcessManagerControlLoop.Branching.Results.cs:123` | Backward branch exhaustion test at `repo://tests/CanDoItAll.Tests.Unit/ProcessManagerControlLoopTests.cs:92`; missing artifact recovery budget assertion at `repo://tests/CanDoItAll.Tests.Unit/ProcessManagerControlLoopTests.cs:21`. |
| SB09-INV-006: Branch display text never determines runtime routing. | `repo://src/CanDoItAll.Processes.Runtime/ProcessManagerBranchContracts.cs:6`, `repo://src/CanDoItAll.Processes.Application/ProcessManagerControlLoop.Branching.cs:8` | `bundle://proof/SB09/scans/branch-token-routing-production-scan.txt`; branch idempotency test at `repo://tests/CanDoItAll.Tests.Unit/ProcessManagerControlLoopTests.cs:63`. |
| SB09-INV-007: Subprocess coordination uses durable typed parent/child messages. | `repo://src/CanDoItAll.Processes.Runtime/ProcessManagerSubprocessContracts.cs:29`, `repo://src/CanDoItAll.Processes.Application/ProcessManagerControlLoop.Subprocess.cs:8` | Subprocess message test at `repo://tests/CanDoItAll.Tests.Unit/ProcessManagerControlLoopTests.cs:118`. |
| SB09-INV-008: Runtime remains persistence-free after adding manager contracts. | `repo://src/CanDoItAll.Processes.Runtime/CanDoItAll.Processes.Runtime.csproj`, CodeAnalytics snapshot `snap-20260615211126-6cb94128` | `bundle://proof/SB09/scans/runtime-forbidden-persistence-scan.txt`. |
| SB09-INV-009: Old recovery/branch symbols are not reintroduced outside archive references. | Active source/tests/tools/templates | `bundle://proof/SB09/scans/old-symbol-scan.txt`. |
| SB09-INV-010: SB09 manager code avoids the listed .NET performance antipatterns. | `repo://codex/bundles/process-module-architecture-v3/architecture/19-dotnet-performance-guardrails.md` | `bundle://proof/SB09/performance-scan-summary.json`; one LINQ match is an in-memory typed outcome selector, not a persistence/UI query hot path. |

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative Test / Scan |
| --- | --- | --- | --- | --- |
| `ProcessRestrictedDiagnosticEvidence` | Incident producers and manager incident intake | `IProcessDiagnosticEvidenceStore` | Stored as restricted evidence; only reference id is attached to normal incident content. | Raw diagnostic restriction test; `bundle://proof/SB09/scans/raw-diagnostic-projection-scan.txt`. |
| `ProcessIncident` | `RaiseIncidentAsync` | Incident store, manager queue, future incident projector | Raised with typed classification, severity, status, safe content, diagnostic reference, correlation, and causation. | Missing/stale artifact tests at `repo://tests/CanDoItAll.Tests.Unit/ProcessManagerControlLoopTests.cs:21` and `repo://tests/CanDoItAll.Tests.Unit/ProcessManagerControlLoopTests.cs:44`. |
| `ProcessRecoveryRequest` | `EvaluateRecoveryAsync` | Recovery request store, manager decision store, dispatcher handoff consumer | Idempotent; status is scheduled, denied, or escalated after policy and budget checks. | Policy denial and missing artifact recovery tests. |
| `ProcessBranchDecision` | `RecordBranchDecisionAsync` | Branch decision store and runtime route-application boundary | Idempotent typed outcome selection; route application is a handoff, not direct runtime mutation. | Branch idempotency test; direct runtime mutation scan. |
| `ProcessLoopBudgetConsumption` | Recovery and branch control-loop code | Loop budget ledger | Consumed with idempotency key before automatic repeat behavior; exhaustion escalates. | Backward branch loop escalation test; recovery budget assertions. |
| `ProcessSubprocessControlMessage` | `SendSubprocessMessageAsync` | Subprocess message store and future parent/child projectors | Durable correlated message with artifact projection refs and schema/sensitivity metadata. | Subprocess message test. |
| Manager runtime events | Manager control-loop operations | Runtime event consumers and future SB10 projectors | Emitted for incident, recovery approve/deny, branch record/reject, loop escalation, and subprocess queue outcomes. | Event assertions in `repo://tests/CanDoItAll.Tests.Unit/ProcessManagerControlLoopTests.cs:21`, `:92`, `:118`, and `:173`. |

## Semantic Adequacy Gate

| Gate item | Evidence |
| --- | --- |
| Shallow-pass trap | A fake manager could record incident rows, route by branch label text, and dispatch recovery repeatedly without policy, budget, or idempotency checks. |
| Adversarial negative proof | Policy denial produces `ManagerRecoveryDenied`, no dispatch, and zero budget consumption; backward branch budget exhaustion produces `ManagerLoopBudgetEscalated`; raw diagnostic detail is not visible in safe content. |
| Semantic positive proof | Missing artifact recovery creates sanitized incident evidence, manager work item, recovery request, approved event, and dispatch handoff; subprocess message includes parent/child IDs, correlation/causation, schema, sensitivity, and artifact projection refs. |
| Anti-stub audit | `bundle://proof/SB09/scans/anti-stub-scan.txt` reports no TODO, placeholder, stub, or `NotImplementedException` markers in SB09 source/tests. |
| Source assertions | `bundle://proof/SB09/source-assertions.txt`. |
| Failing-first proof | `bundle://proof/SB09/failing-first-process-manager-tests.txt`. |
| Passing proof | `bundle://proof/SB09/test-unit-sb09.txt`, `bundle://proof/SB09/build-unit-sb09.txt`, and `bundle://proof/SB09/build-solution-sb09.txt`. |
| CodeAnalytics proof | `bundle://proof/SB09/codeanalytics-snapshot-summary.txt`. |

## Validation Commands

```text
dotnet build tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --nologo
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "ProcessManagerControlLoopTests|ProcessPersistenceStoreTests|ProcessRuntimeEngineTests|ProcessInstancePlanCompilerTests|ProcessDriverAbstractionTests|ProcessTemplateGitFoundationTests|ProcessCoreKernelTests|ProcessModuleBoundaryTests" --nologo
dotnet build CanDoItAll.slnx --nologo
```

Results are captured in `bundle://proof/SB09/build-unit-sb09.txt`, `bundle://proof/SB09/test-unit-sb09.txt`, and `bundle://proof/SB09/build-solution-sb09.txt`.
