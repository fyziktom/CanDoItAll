# ADR: Recoverable Finalizer Validation Feedback

## Status

Accepted for the July 10 follow-up incident.

## Incident evidence

- Process run: `c636460f-43cf-4a2a-bfbb-e90039e4545d`
- Execution run: `74965c18-3d94-4a34-b9dc-750df750d822`
- Step: `peer-review`
- Failure: `process.step_outcome.branch_key_required`
- The agent read both required upstream artifacts and wrote the required current-run `peer-review.md` before finalizing.
- The initial governed brief was approximately 8,950 tokens. Process-run compaction was disabled, so the step contract remained in the session.
- The finalizer instructions required both branch fields but did not state that a title requires a stable key or that both fields must be empty when the step selects no branch.
- Semantic validation threw inside the finalizer tool. The provider turn therefore failed before the existing missing-finalizer and typed-JSON repair paths could run.

## Responsibility inventory

| Current owner | Responsibility | Problem | Target owner | Test seam |
| --- | --- | --- | --- | --- |
| `MafRuntimeAgentFactory` | Constructs every finalizer tool and its schema | Contract-specific tool metadata remains inside the runtime agent factory | `MafFinalizerToolFactory` | Build a finalizer tool without constructing the runtime agent |
| `FinalizerCapture` | Validates and commits finalizer submissions | Invalid semantic input throws and aborts the provider turn | `FinalizerCapture` with typed submission acknowledgement | Submit invalid then corrected input directly |
| `MafFinalizerDriver` | Supplies bounded finalizer instructions | Process outcome branch invariant is absent | Existing finalizer driver contract instructions | Assert branch pairing and no-branch instructions |
| `MafRuntimeToolInvocationResultClassifier` | Classifies tool acknowledgement | JSON acknowledgement failures lose their concrete message | Existing generic classifier | Classify serialized accepted/rejected results |

## Decision

1. Extract finalizer tool construction into a top-level `MafFinalizerToolFactory` in the MAF adapter project.
2. Keep `ProcessStepOutcomeResult` tolerant of object or JSON-string arguments at the adapter boundary, but expose a precise parameter description containing the semantic invariants.
3. Return a typed acknowledgement with `Succeeded` and `Message` from finalizer capture.
4. Reject invalid candidates without committing them and without throwing. Include stable validation codes in the acknowledgement so the model can correct the call in the same session.
5. Atomically commit the first valid candidate. Later duplicates remain harmless and do not create extra invocations.
6. Preserve the existing bounded missing-finalizer and typed-JSON repair paths as provider-independent fallback behavior.

## Boundary and dependency direction

```text
CanDoItAll.Modules.Processes
  -> AgentFramework.Core
  -> AgentFramework.Models

AgentFramework.Maf
  -> AgentFramework.Core
  -> AgentFramework.Models
```

The repair changes no process-core, process-runtime, dispatcher, or project references. `ProcessStepOutcomeResult` is an enterprise-process contract; no software-delivery, Tetris, Calculator, QA, or .NET rule is introduced.

CodeAnalytics snapshot `snap-20260710102007-17f0a9c5` loaded the four affected projects with no dependency cycles. The only snapshot diagnostics were the pre-existing `Microsoft.OpenApi` package warning.

## Rejected options

- Add Tetris or peer-review prompt exceptions: this would hide the contract defect and leak a sample application into reusable process infrastructure.
- Silently delete `branchOutcomeTitle`: this would mutate authoritative machine output without telling the agent or operator.
- Add dispatcher retry logic keyed to `branch_key_required`: the dispatcher must remain contract-agnostic.
- Add a new manager-agent or LLM recovery driver immediately: the existing same-session and bounded JSON repair mechanisms already cover this responsibility once invalid tool input is recoverable. A new provider call is justified only if later evidence shows cross-provider repair cannot use the existing seam.

## Acceptance criteria

- Invalid finalizer input returns structured rejection feedback and does not commit an invocation.
- A corrected submission in the same capture succeeds and is the only committed invocation.
- Branch instructions state the key/title pairing and empty-field no-branch rule.
- The tool schema exposes the same invariant close to the `result` argument.
- The runtime classifier preserves rejection status and message after JSON marshaling.
- Required finalizer calls still short-circuit only after a valid capture.
- No partial class, project reference, service locator, domain-specific dispatcher branch, or software-delivery token is added.

## Live E2E proof

- Fresh root run: `9cad8f14-c0be-4d08-9421-05af2725fd9c`
- Definition version: `d9a50003-3492-4b0b-f6e7-8808c769951f`
- Plan hash: `sha256:d5b49944c8e6e522bfb47309488e8ff67c860398f4579b2075ea8d5bef3b4d64`
- The previously failing `peer-review` step completed on its first attempt with a valid no-branch finalizer and no diagnostics.
- The root run and all six child/subprocess runs completed with zero terminal diagnostics and zero projection backlog.
- No escalation, attention, blocked, failed, cancelled, rejected, or fault event occurred in the 121-event root history.
- QA deterministically routed remaining Blazor scaffold content to `repair-required`. The first repair execution falsely claimed completion without product mutation or current-run proof; generic completion gates rejected it and `process.current-step-safe-retry` immediately retried the same step without manager-agent or human intervention. The second execution mutated the product, restored, built, tested, ran, captured browser proof, and stopped the host before completing.
- The delivered output contains `TetrisGame.slnx`, the runnable app project, and the test project. Process receipts report a clean build and 3/3 passing tests.
