# Concrete incident hypothesis: `prepare-solution-skeleton`

## Observed blocker

The process blocked on:

- process title: `.NET implementation slice with atomic validation`
- run prefix: `1e2facd4`
- step key: `prepare-solution-skeleton`
- strategy outcome: `NeedsManager`
- applied runtime state: `Blocked`
- missing AgentFramework result summary in operator projection

## What the runtime expected

From `Templates/Processes/processes/dotnet-development-slice/definition.json:269-345`:

- Step key: `prepare-solution-skeleton`
- Step kind: `Subprocess`
- Child process: `dotnet-solution-setup`
- Produced artifact expectation: `solution-skeleton-evidence`
- Child mapping: `setup-handoff` / `Setup handoff packet`
- Allowed operations: `ReadProcessContext`, `ReadProjectStructure`, `ReadUpstreamArtifacts`, `WriteManagedProcessArtifacts`, `ExecuteExternalAction`

From `steps/prepare-solution-skeleton.md:3-9`:

- launch child setup subprocess when solution is missing/incomplete,
- submit `ParentDeferredOutcomeJson` exactly while child is active,
- accept `setup-handoff` or `setup-handoff-after-repair`,
- treat `setup-repair-escalation` as blocker evidence.

## What the attached output proves and does not prove

The attached `calculator-output.zip` proves that a product skeleton exists. It does not prove that the process step completed its contract.

It is missing process evidence such as:

```text
artifacts/process-runs/<parent-run-id>/steps/prepare-solution-skeleton.md
artifacts/process-runs/<child-run-id>/steps/setup-handoff.md
artifacts/process-runs/<child-run-id>/steps/setup-handoff-after-repair.md
```

Therefore the likely sequence is:

1. Child or parent created the product skeleton.
2. Runtime expected `solution-skeleton-evidence` in the parent produced slot.
3. The accepted child handoff was not deterministically bridged into the parent artifact.
4. Adapter/finalizer returned or transformed the outcome to `NeedsManager`.
5. Projection could not find/parse AgentFramework summary for the exact step, so it displayed the generic tool/access/slot hint.
6. Rework repeated the same ambiguous parent step and hit the same missing contract.

## Immediate manual recovery path for the current run

For the concrete run, before another blind rework:

1. Locate the exact parent `ProcessRunId` and `StepInstanceId` for `prepare-solution-skeleton`.
2. Query AgentFramework execution runs by both process run and step id, not just process run.
3. Locate child run launched with launch variables matching the parent run/step.
4. Inspect child terminal state:
   - accepted: `setup-handoff` with `setup-handoff-packet`, or
   - accepted after repair: `setup-handoff-after-repair` with `setup-handoff-packet-after-repair`, or
   - no-go: `setup-repair-escalation` with `setup-repair-escalation-packet`.
5. If accepted child proof exists, write/repair the parent artifact `prepare-solution-skeleton.md` with exact child run id, child step key, child artifact ref, created solution/project paths, and validation result.
6. If no-go exists, propagate it as a concrete parent blocker instead of retrying setup blindly.
7. If no child run exists and runtime owns subprocess launch after the fix, reset the parent step to Ready once and let runtime launch the child deterministically.

## Long-term fix

Do not rely on agent-written parent evidence for controlled subprocesses. The runtime should synthesize parent evidence from accepted child artifacts through `ParentSubprocessArtifactBridge` and then finalize the parent slot.
