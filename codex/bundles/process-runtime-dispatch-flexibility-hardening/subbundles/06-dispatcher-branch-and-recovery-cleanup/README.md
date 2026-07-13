# Dispatcher Driver Dispatch Branch And Recovery Cleanup

## Status

- `Completed`

## Objective

- Clean `ProcessRuntimeDispatchApplicationService` and related runtime services so generic dispatch claim lifecycle, branch routing, retry budgets, recovery, and cancellation are explicit and injectable while step execution dispatch policy is delegated to the selected driver.

## Covered Inputs

- `R001` Preserve behavior.
- `R002` Split integration/runtime hotspots.
- `R009` Clean dispatcher branch and recovery responsibilities.
- `R010` Keep diagnostics explicit and actionable.
- `R013` Driver ports own completion evidence, prompt composition, and step execution dispatch behavior.
- `R014` Maintain one-way dependency direction from MAF/AgentFramework to Processes contracts.
- `R015` Keep generic runtime dispatch orchestration separate from driver-owned step execution dispatch.
- `N007` Completion evidence, runtime process dispatching, and prompt fragment composition must be driver-owned.
- `N008` Processes must not depend on the MAF wrapper.

## Prerequisites

- SB01 boundary and project placement gate passed.
- SB01 must identify target owners for branch routing, claim lifecycle helpers, recovery coordinator/reconciler/worker, and cancellation observer.
- SB02-SB04 must have created or mapped the driver-owned step execution, prompt composition, and completion evidence collaborators that dispatcher integration will invoke.
- SB01 dependency scan must prove dispatcher integration can call driver abstractions without a `src/Processes/*` reference to MAF or `CanDoItAll.Modules.AgentFramework`.

## Exact Source References

- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeDispatchApplicationService.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeBranchSignalApplicationService.cs`
- `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeDispatchQueueContracts.cs`
- `repo://src/Processes/CanDoItAll.Processes.Runtime/ProcessStrategyDispatcher.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessExecutionAdapterContracts.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessStrategyContracts.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions/ProcessDriverPackage.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/ProcessRuntimeDispatchQueue.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/ProcessRuntimeDispatchQueueServices.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/ProcessRuntimeDispatchRecoveryRunQuery.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeDispatchApplicationServiceTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeDispatchQueueTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessRuntimeEngineTests.cs`

## Deliverables

- Dispatcher branch routing uses injected service dependencies rather than direct construction where practical.
- Runtime dispatch invokes selected driver step execution through SB01-approved abstractions; provider/tool invocation, prompt policy, completion evidence policy, and driver-specific recovery are not implemented as dispatcher private methods.
- Stale duplicated private branch signal methods are removed or reconciled with `ProcessRuntimeBranchSignalApplicationService`.
- Claim lifecycle helpers, stale pre-running claim cleanup, retry budget handling, automatic retry instruction, claim release/defer best-effort paths, and projection catchup are grouped into focused helpers or services.
- Recovery coordinator, recovery observer, cancellation observer, reconciler, and worker are moved out of the mega-file into coherent files/services.
- Tests prove branch gate propagation, skipped branch propagation, claim cleanup, deferred child dispatch, retry limits, transient retry suppression, and recovery observer behavior.
- Tests prove a fake selected driver can handle step execution dispatch without changing generic dispatcher code.

## Dependency Impact

- SB07 depends on this phase for end-to-end dispatch liveness, driver-dispatch delegation, dependency-direction, and recovery proof.
- SB02 may depend on dispatcher deferred-exception behavior when subprocesses pause parent steps.

## Validation Depth

- Critical foundation.
- Requires Semantic Adequacy Gate proof and artifact-backed proof manifest.

## Implementation Steps

1. Verify references to existing branch signal methods and identify stale private duplicates in `ProcessRuntimeDispatchApplicationService`.
2. Verify dispatcher responsibilities against SB01: claims, scheduling, branch application, retry budget, queue lifecycle, projection catchup, and selected-driver invocation stay generic; actual step execution policy stays in drivers.
3. Inject or otherwise isolate `ProcessRuntimeBranchSignalApplicationService` so dispatcher tests can substitute it when needed.
4. Remove duplicated branch signal logic after tests prove the extracted service is the active path.
5. Extract claim lifecycle/retry helpers only where it reduces real complexity and preserves runtime semantics.
6. Move recovery/cancellation classes from the integration mega-file into focused files with DI registration unchanged or clarified.
7. Add tests for branch propagation, skipped branches, claim cleanup, retry budget, defer/release best-effort behavior, recovery/cancellation observers, and fake selected-driver dispatch replacement.
8. Update proof artifacts and execution report.

## Scope Exceptions

- Do not redesign the runtime engine state machine in this phase.
- Do not change dispatch timing defaults unless tests prove the existing value is a bug and the change is explicitly documented.
- Do not move AgentFramework/MAF provider calls, prompt fragments, completion evidence parsing, or driver-specific recovery heuristics into `ProcessRuntimeDispatchApplicationService`.

## Do Not Do

- Do not leave two active branch routing implementations.
- Do not hide optimistic concurrency failures behind silent retries without diagnostics.
- Do not swallow claim release/defer failures; preserve actionable diagnostic behavior.
- Do not make recovery workers depend on UI/module state.
- Do not add any MAF/AgentFramework project reference to `src/Processes/*`.

## Acceptance Checklist

- Dispatcher no longer contains stale duplicated branch signal implementation.
- Dispatcher source assertions show generic dispatch owns scheduling/claims/lifecycle while selected driver owns step execution policy.
- Branch routing is testable without invoking the full dispatch loop.
- Claim lifecycle behavior remains compatible with existing tests.
- Recovery and cancellation services are in coherent files with explicit DI.
- Diagnostic messages still include run/step context where applicable.
- Fake-driver tests prove runtime dispatch can switch step execution behavior through the driver boundary.
- Static scans prove dispatcher cleanup did not introduce Processes-to-MAF dependencies.

## Proof Required

- `proof/SB06/manifest.md` with changed-file hashes, command transcripts, source assertions, and anti-stub audit output.
- `proof/SB06/semantic-invariants.md` covering branch routing single-path behavior, claim lifecycle preservation, deferred dispatch, retry budget, and recovery/cancellation observers.
- Failing-first proof for stale duplicate branch logic or missing branch propagation.
- Failing-first or source-assertion proof that dispatcher cannot hardcode AgentFramework/MAF prompt/evidence/step execution policy.
- Dependency-direction scan transcript proving no Processes-to-MAF reference after dispatcher cleanup.
- Passing dispatch, queue, runtime engine, and recovery test transcripts.

## Browser Validation Logging

- N/A - no browser-visible behavior should change in SB06.

## Progression Gate

- SB07 may start only after dispatch branch routing has one active implementation, tests prove claim/recovery behavior remains stable, fake-driver dispatch replacement is proven, and dependency scans show Processes still does not reference MAF.

## Suggested Agent Prompt

```text
Implement SB06 only. Clean dispatcher branch and recovery responsibilities without changing runtime state semantics. Keep generic dispatcher ownership to scheduling, claims, branch application, retry budgets, queue lifecycle, projection catchup, and selected-driver invocation. Do not move AgentFramework/MAF prompt, evidence, provider/tool, or recovery policy into Processes. Remove or reconcile stale branch methods, isolate branch routing, move recovery/cancellation classes to coherent files, and add focused tests for branch, claim, retry, defer, queue, fake selected-driver dispatch, recovery, and cancellation behavior. Capture proof/SB06/manifest.md and proof/SB06/semantic-invariants.md before closure.
```

