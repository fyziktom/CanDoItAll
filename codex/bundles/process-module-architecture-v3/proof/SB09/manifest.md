# SB09 Manager, Incidents, Recovery, Branch/Switch, Loop Protection, And Subprocess Control Proof Manifest

## Status

Completed on 2026-06-15.

## Implementation Summary

SB09 adds the manager control-loop contracts and application orchestration for incidents, restricted diagnostics, policy/budget-checked recovery, typed branch decisions, loop escalation, and durable subprocess manager messages. The manager emits explicit decision events and handoffs; it does not directly mutate runtime state or execute domain work.

## Changed Source

Changed source and test hashes are recorded in `bundle://proof/SB09/changed-file-hashes.txt`. Line counts are recorded in `bundle://proof/SB09/line-counts.txt`.

## Source Assertions

| Assertion | Source |
| --- | --- |
| Manager orchestration is an Application service split by concern. | `repo://src/CanDoItAll.Processes.Application/ProcessManagerControlLoop.cs:7`, `repo://src/CanDoItAll.Processes.Application/ProcessManagerControlLoop.Recovery.cs:7`, `repo://src/CanDoItAll.Processes.Application/ProcessManagerControlLoop.Branching.cs:6` |
| Manager dependencies are explicit stores, policies, queues, and decision ports, not runtime state mutation ports. | `repo://src/CanDoItAll.Processes.Application/ProcessManagerRuntimeDependencies.cs:5`, `repo://src/CanDoItAll.Processes.Runtime/ProcessManagerPorts.cs:6` |
| Incidents store restricted diagnostic evidence behind a diagnostic reference and safe incident content. | `repo://src/CanDoItAll.Processes.Runtime/ProcessManagerIncidentContracts.cs:6`, `repo://src/CanDoItAll.Processes.Runtime/ProcessManagerIncidentContracts.cs:11`, `repo://src/CanDoItAll.Processes.Runtime/ProcessManagerIncidentContracts.cs:17` |
| Recovery requests carry incident, loop fingerprint, policy, status, dispatch handoff, and denial/escalation state. | `repo://src/CanDoItAll.Processes.Runtime/ProcessManagerRecoveryContracts.cs:21`, `repo://src/CanDoItAll.Processes.Runtime/ProcessManagerRecoveryContracts.cs:26`, `repo://src/CanDoItAll.Processes.Application/ProcessManagerControlLoop.Recovery.cs:9` |
| Branch decisions select a typed `BranchOutcomeId` and produce a typed route handoff. | `repo://src/CanDoItAll.Processes.Runtime/ProcessManagerBranchContracts.cs:6`, `repo://src/CanDoItAll.Processes.Runtime/ProcessManagerBranchContracts.cs:27`, `repo://src/CanDoItAll.Processes.Runtime/ProcessManagerBranchContracts.cs:43` |
| Backward branch routes use loop fingerprints and loop-budget consumption before recording or escalating. | `repo://src/CanDoItAll.Processes.Application/ProcessManagerControlLoop.Branching.cs:8`, `repo://src/CanDoItAll.Processes.Application/ProcessManagerControlLoop.Branching.Results.cs:123`, `repo://src/CanDoItAll.Processes.Application/ProcessManagerControlLoop.Branching.Results.cs:181` |
| Subprocess manager messages are durable, correlated, schema-versioned, sensitivity-tagged, and artifact-projection aware. | `repo://src/CanDoItAll.Processes.Runtime/ProcessManagerSubprocessContracts.cs:13`, `repo://src/CanDoItAll.Processes.Runtime/ProcessManagerSubprocessContracts.cs:29`, `repo://src/CanDoItAll.Processes.Application/ProcessManagerControlLoop.Subprocess.cs:8` |
| Manager decisions are runtime events. | `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimeEventTypes.cs:41`, `repo://src/CanDoItAll.Processes.Application/ProcessManagerControlLoop.Recovery.cs:93`, `repo://src/CanDoItAll.Processes.Application/ProcessManagerControlLoop.Branching.Results.cs:21` |

Additional source assertions are captured in `bundle://proof/SB09/source-assertions.txt`.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Proof |
| --- | --- | --- | --- | --- |
| `ProcessRestrictedDiagnosticEvidence` | Incident signal producer and manager incident intake | Diagnostic evidence store | Stored as restricted evidence; normal incident content receives only a `ProcessDiagnosticReference`. | `repo://src/CanDoItAll.Processes.Runtime/ProcessManagerIncidentContracts.cs:6`, `repo://tests/CanDoItAll.Tests.Unit/ProcessManagerControlLoopTests.cs:158` |
| `ProcessIncident` | `ProcessManagerControlLoop.RaiseIncidentAsync` | Incident store, manager queue, future incident projector | Raised with classification/severity/status, safe content, diagnostic reference, correlation, and causation. | `repo://src/CanDoItAll.Processes.Application/ProcessManagerControlLoop.Incidents.cs:7`, `repo://tests/CanDoItAll.Tests.Unit/ProcessManagerControlLoopTests.cs:21` |
| `ProcessManagerWorkItem` | Incident/recovery/branch/subprocess manager flows | Manager work queue | Queued for manager processing with kind, run, correlation, and idempotency key. | `repo://src/CanDoItAll.Processes.Runtime/ProcessManagerIncidentContracts.cs:57`, `repo://tests/CanDoItAll.Tests.Unit/ProcessManagerControlLoopTests.cs:21` |
| `ProcessRecoveryRequest` | `EvaluateRecoveryAsync` | Recovery request store and dispatcher handoff consumer | Idempotent request transitions to Scheduled, Denied, or Escalated after policy and budget checks. | `repo://src/CanDoItAll.Processes.Application/ProcessManagerControlLoop.Recovery.cs:9`, `repo://tests/CanDoItAll.Tests.Unit/ProcessManagerControlLoopTests.cs:173` |
| `ProcessLoopBudgetConsumption` | Recovery and backward branch handling | Loop budget ledger | Consumed before automatic recovery dispatch and backward branch route recording; exhaustion produces escalation. | `repo://src/CanDoItAll.Processes.Runtime/ProcessManagerRecoveryContracts.cs:49`, `repo://tests/CanDoItAll.Tests.Unit/ProcessManagerControlLoopTests.cs:92` |
| `ProcessBranchDecision` | `RecordBranchDecisionAsync` | Branch decision store and runtime route-application boundary | Idempotently records selected typed outcome and returns a typed route handoff; no display-text routing. | `repo://src/CanDoItAll.Processes.Application/ProcessManagerControlLoop.Branching.cs:8`, `repo://tests/CanDoItAll.Tests.Unit/ProcessManagerControlLoopTests.cs:63` |
| `ProcessSubprocessControlMessage` | `SendSubprocessMessageAsync` | Subprocess message store and future parent/child process projectors | Durable parent/child control message with correlation, causation, schema, sensitivity, and artifact projection refs. | `repo://src/CanDoItAll.Processes.Application/ProcessManagerControlLoop.Subprocess.cs:8`, `repo://tests/CanDoItAll.Tests.Unit/ProcessManagerControlLoopTests.cs:118` |
| Manager runtime events | Manager control-loop operations | Runtime event consumers, future projectors, operator UI | Incident, recovery, branch, loop escalation, and subprocess outcomes are emitted as typed runtime events. | `repo://src/CanDoItAll.Processes.Runtime/ProcessRuntimeEventTypes.cs:41`, `repo://tests/CanDoItAll.Tests.Unit/ProcessManagerControlLoopTests.cs:21` |

## Tests And Command Proof

| Proof | Result |
| --- | --- |
| `bundle://proof/SB09/failing-first-process-manager-tests.txt` | Failing-first proof captured before implementation; manager contracts/control loop were missing. |
| `bundle://proof/SB09/build-unit-sb09.txt` | Unit test project build passed with 0 warnings and 0 errors after SB09 implementation/refactor. |
| `bundle://proof/SB09/test-unit-sb09.txt` | Focused SB03-SB09 process tests passed: 67/67. |
| `bundle://proof/SB09/build-solution-sb09.txt` | Full solution build passed with 0 warnings and 0 errors. |
| `bundle://proof/SB09/scans/old-symbol-scan.txt` | Active source/tests/tools/templates contain no old recovery/branch/runtime symbols. |
| `bundle://proof/SB09/scans/branch-token-routing-production-scan.txt` | Production manager code contains no token/string branch routing indicators. |
| `bundle://proof/SB09/scans/branch-typed-routing-source-scan.txt` | Typed branch outcome, route target, and loop fingerprint source anchors captured. |
| `bundle://proof/SB09/scans/direct-runtime-mutation-scan.txt` | Manager Application code has no runtime state store, runtime unit of work, DbContext, SaveChanges, or runtime state snapshot references. |
| `bundle://proof/SB09/scans/raw-diagnostic-projection-scan.txt` | Normal projection/Application paths do not expose restricted diagnostic detail. |
| `bundle://proof/SB09/scans/runtime-forbidden-persistence-scan.txt` | Runtime still has no EF, Npgsql, DbContext, or Persistence project dependency. |
| `bundle://proof/SB09/scans/anti-stub-scan.txt` | No TODO, placeholder, stub, or `NotImplementedException` markers in SB09 manager source/tests. |
| `bundle://proof/SB09/performance-scan-summary.json` | No sync waits, Thread.Sleep, Task.Run, per-call HTTP/JSON options, unbounded queue, or load-all query patterns; one in-memory typed outcome selector. |
| `bundle://proof/SB09/manager-safety-review.md` | Manager safety review passed. |
| `bundle://proof/SB09/codeanalytics-snapshot-summary.txt` | CodeAnalytics snapshot `snap-20260615211126-6cb94128` loaded 6 scoped projects and 68 documents, found 0 diagnostics and no blocking errors. |
| `bundle://proof/SB09/bundle-validator-prepared-sb09.txt` | Prepared-stage bundle validator passed after SB09 proof/status synchronization. |
| `bundle://proof/SB09/changed-file-hashes.txt` | Portable SHA-256 hash proof for changed SB09 source/test files. |

## Test Coverage Anchors

| Behavior | Test |
| --- | --- |
| Missing artifact recovery stores sanitized incident evidence and creates recovery dispatch handoff. | `repo://tests/CanDoItAll.Tests.Unit/ProcessManagerControlLoopTests.cs:21` |
| Stale artifact incidents use stale classification and restricted diagnostic references. | `repo://tests/CanDoItAll.Tests.Unit/ProcessManagerControlLoopTests.cs:44` |
| Branch decisions are idempotent and do not consume loop budget twice. | `repo://tests/CanDoItAll.Tests.Unit/ProcessManagerControlLoopTests.cs:63` |
| Backward branch loop budget exhaustion escalates instead of repeating. | `repo://tests/CanDoItAll.Tests.Unit/ProcessManagerControlLoopTests.cs:92` |
| Subprocess artifact projection messages are durable and correlated. | `repo://tests/CanDoItAll.Tests.Unit/ProcessManagerControlLoopTests.cs:118` |
| Incident projection content never contains restricted diagnostic detail. | `repo://tests/CanDoItAll.Tests.Unit/ProcessManagerControlLoopTests.cs:158` |
| Recovery policy denial records explicit denial without dispatch or budget consumption. | `repo://tests/CanDoItAll.Tests.Unit/ProcessManagerControlLoopTests.cs:173` |
| Control-loop dependencies do not expose runtime mutation ports. | `repo://tests/CanDoItAll.Tests.Unit/ProcessManagerControlLoopTests.cs:189` |

## Red-Team Evidence

The shallow-pass trap is a manager that only records generic rows or dispatches by string labels. SB09 rejects that through production scans and tests: raw details are not visible in normal incident content, branch routing is selected by typed `BranchOutcomeId`, duplicate branch decisions return the original decision without double-consuming loop budget, denied recovery does not dispatch or consume budget, and exhausted backward routing escalates.

## Browser Validation

Not required. SB09 changes backend contracts, application orchestration, and unit tests only.

## Downstream Handoff

SB10 can consume manager event types, incident/recovery/branch/subprocess contracts, safe incident content, diagnostic references, loop escalation events, and subprocess artifact projection references as projection inputs. Concrete store implementations, composition wiring, and UI surfaces remain downstream work.
