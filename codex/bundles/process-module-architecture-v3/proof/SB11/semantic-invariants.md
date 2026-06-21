# SB11 Semantic Invariant Contract

## Status

Satisfied on 2026-06-15.

## Invariants

| Invariant | Evidence | Negative Proof |
| --- | --- | --- |
| SB11-INV-001: Generic Core and Runtime do not reference concrete execution adapter APIs. | `bundle://proof/SB11/codeanalytics-snapshot-summary.txt` | `bundle://proof/SB11/scans/adapter-specific-leak-scan.txt`, `repo://tests/CanDoItAll.Tests.Unit/ProcessExecutionAdapterBoundaryTests.cs:104`. |
| SB11-INV-002: Execution adapter APIs are strongly typed and do not use stringly typed integration selection. | `repo://src/CanDoItAll.Processes.Drivers.Abstractions/ProcessExecutionAdapterContracts.cs:14`, `repo://src/CanDoItAll.Processes.Drivers.Abstractions/ProcessExecutionAdapterContracts.cs:68`, `repo://src/CanDoItAll.Processes.Drivers.Abstractions/ProcessDriverTokens.cs:63` | Failing-first proof at `bundle://proof/SB11/failing-first-process-execution-adapter-tests.txt`. |
| SB11-INV-003: Adapter execution returns strategy envelopes, not adapter-specific result types. | `repo://src/CanDoItAll.Processes.Drivers.Standard/StandardProcessAdapterStrategyFactory.cs:47` | Adapter envelope test at `repo://tests/CanDoItAll.Tests.Unit/ProcessExecutionAdapterBoundaryTests.cs:9`. |
| SB11-INV-004: Diagnostics carry restricted evidence references and explicit retry/idempotency classifications. | `repo://src/CanDoItAll.Processes.Drivers.Abstractions/ProcessStrategyContracts.cs:66`, `repo://src/CanDoItAll.Processes.Drivers.Abstractions/ProcessExecutionAdapterContracts.cs:59` | Diagnostic test at `repo://tests/CanDoItAll.Tests.Unit/ProcessExecutionAdapterBoundaryTests.cs:49`; `bundle://proof/SB11/scans/raw-diagnostic-normal-text-scan.txt` reviewed in `bundle://proof/SB11/adapter-security-review.md`. |
| SB11-INV-005: Standard layered driver slice composes foundation before adapter package. | `repo://src/CanDoItAll.Processes.Drivers.Standard/StandardProcessAdapterDriverPackageFactory.cs:8`, `repo://src/CanDoItAll.Processes.Drivers.Standard/StandardProcessAdapterDriverPackageFactory.cs:29` | Layering test at `repo://tests/CanDoItAll.Tests.Unit/ProcessExecutionAdapterBoundaryTests.cs:56`. |
| SB11-INV-006: Adapter code does not mutate runtime state directly. | `repo://src/CanDoItAll.Processes.Drivers.Standard/CanDoItAll.Processes.Drivers.Standard.csproj` | `bundle://proof/SB11/scans/adapter-runtime-mutation-scan.txt`. |
| SB11-INV-007: Unauthorized adapter file mutations are audited through the typed Git wrapper. | `repo://src/CanDoItAll.Processes.Application/ProcessAdapterMutationAudit.cs:10`, `repo://src/CanDoItAll.Processes.Application/ProcessAdapterMutationAudit.cs:57` | Mutation audit test at `repo://tests/CanDoItAll.Tests.Unit/ProcessExecutionAdapterBoundaryTests.cs:79`; `bundle://proof/SB11/scans/ad-hoc-git-scan.txt`. |
| SB11-INV-008: The concrete Standard driver project is the only approved active concrete driver slice. | `repo://src/CanDoItAll.Processes.Drivers.Standard/CanDoItAll.Processes.Drivers.Standard.csproj`, `bundle://proof/SB11/codeanalytics-snapshot-summary.txt` | Boundary test at `repo://tests/CanDoItAll.Tests.Unit/ProcessModuleBoundaryTests.cs:190`. |
| SB11-INV-009: Runtime has no direct workflow, agent, handoff, scheduler, project/workbench, HTTP, Git, or audit service integration path. | `repo://src/CanDoItAll.Processes.Runtime/CanDoItAll.Processes.Runtime.csproj` | `bundle://proof/SB11/scans/runtime-direct-external-api-scan.txt`. |
| SB11-INV-010: SB11 adapter/audit code avoids the listed .NET performance antipatterns. | `repo://codex/bundles/process-module-architecture-v3/architecture/19-dotnet-performance-guardrails.md` | `bundle://proof/SB11/performance-scan-summary.json`. |

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative Test / Scan |
| --- | --- | --- | --- | --- |
| `IProcessExecutionAdapter` | Concrete integration packages | Standard adapter strategy factory | Adapter implementations remain behind an abstraction and are invoked by strategy wrapper only. | Adapter leak scan and Core/Runtime leakage test. |
| `ProcessExecutionAdapterRequest` | Standard strategy wrapper | Adapter implementation | Built per strategy execution with typed ids, operation key, kind, input, and facets. | Failing-first proof and envelope test. |
| `ProcessExecutionAdapterResult` | Adapter implementation | Standard strategy wrapper and runtime strategy result handling | Normalized to `StrategyResultEnvelope` before returning to generic runtime flow. | Envelope test and runtime direct external API scan. |
| `StrategyDiagnosticRef.RestrictedEvidenceReference` | Adapter result normalization | Runtime/manager/projection diagnostic consumers | Carries a restricted reference instead of raw diagnostic detail. | Raw diagnostic scan and security review. |
| Standard layered driver packages | Standard driver package factory | Driver catalog and downstream composition | Foundation package precedes adapter package; concrete package depends on foundation. | Layering test and CodeAnalytics project graph. |
| `ProcessAdapterMutationAuditReport` | Application mutation audit service | Future adapter governance and manager/security surfaces | Git wrapper status/diff inspection produces typed audit outcomes and restricted diff hashes. | Mutation audit test and ad hoc Git scan. |

## Semantic Adequacy Gate

| Gate item | Evidence |
| --- | --- |
| Shallow-pass trap | A fake adapter slice could compile by adding a concrete driver while leaking workflow/agent APIs into Runtime or exposing raw adapter error text. |
| Adversarial negative proof | Adapter-specific leak scan, runtime external API scan, ad hoc Git scan, runtime mutation scan, and raw diagnostic scan are recorded under `bundle://proof/SB11/scans/`. |
| Semantic positive proof | Focused tests prove envelope normalization, layered package ordering, mutation audit behavior, and Core/Runtime boundary protection. |
| Anti-stub audit | `bundle://proof/SB11/scans/anti-stub-scan.txt` reports no TODO, placeholder, stub, or `NotImplementedException` markers in SB11 source/tests. |
| Source assertions | `bundle://proof/SB11/source-assertions.txt`. |
| Failing-first proof | `bundle://proof/SB11/failing-first-process-execution-adapter-tests.txt`. |
| Passing proof | `bundle://proof/SB11/test-unit-sb11.txt`, `bundle://proof/SB11/test-unit-sb11-process-slice.txt`, `bundle://proof/SB11/build-unit-sb11.txt`, and `bundle://proof/SB11/build-solution-sb11.txt`. |
| CodeAnalytics proof | `bundle://proof/SB11/codeanalytics-snapshot-summary.txt`. |

## Validation Commands

```text
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "ProcessExecutionAdapterBoundaryTests" --nologo
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter "ProcessExecutionAdapterBoundaryTests|ProcessProjectionPipelineTests|ProcessManagerControlLoopTests|ProcessPersistenceStoreTests|ProcessRuntimeEngineTests|ProcessInstancePlanCompilerTests|ProcessDriverAbstractionTests|ProcessTemplateGitFoundationTests|ProcessCoreKernelTests|ProcessModuleBoundaryTests" --nologo
dotnet build tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --nologo
dotnet build CanDoItAll.slnx --nologo
```

Results are captured in `bundle://proof/SB11/test-unit-sb11.txt`, `bundle://proof/SB11/test-unit-sb11-process-slice.txt`, `bundle://proof/SB11/build-unit-sb11.txt`, and `bundle://proof/SB11/build-solution-sb11.txt`.
