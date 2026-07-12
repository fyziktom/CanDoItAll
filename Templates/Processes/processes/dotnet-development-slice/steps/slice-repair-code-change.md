# Repair slice findings through subprocess

Launch a feature/function implementation subprocess for the concrete validation findings from `slice-repair-required`.

First call `project_structure_process_subprocess_launch` with `definitionKey` set to `dotnet-feature-function-implementation`. Do not wait silently or return `Blocked` before attempting that child launch unless mandatory repair-launch inputs are missing. If the launch response includes `ParentDeferredOutcomeJson`, submit that parent outcome exactly: active child runs defer the parent, completed child runs complete the parent from child evidence, and stopped child runs propagate their concrete blocker.

Carry forward:

- The chosen slice behavior and exclusions.
- Product root, app archetype, setup handoff, and architecture constraints.
- The exact repair target packet from `add-tests-and-proof`, including failed acceptance criteria, failing command/browser metrics, child run id, child step artifact refs, and the smallest proof that would close the defect.
- The smallest repair request that can be validated by one focused proof loop.

The required `slice-scope-packet` remains authoritative across repair. The local failing assertion or inner child escalation is an additional repair target, not permission to narrow away other core behaviors already assigned to the slice. Pass the scope packet to the child and require its intake, implementation, and validation artifacts to account for every still-unproved acceptance-critical behavior.

If the completed child returns `feature-repair-escalation` or a targeted-recheck escalation, materialize it as reviewable repair-attempt evidence and complete this coordinator step so `add-tests-recheck` can independently evaluate the product. This does not accept the repair. It prevents a typed child no-go from bypassing the manager review lane before the parent has compared the current product with the authoritative slice scope.

Do not ask the child subprocess to select a fresh MVP behavior. This is a repair launch: the child feature scope must inherit the repair target as mandatory acceptance criteria and must not exclude the failing requirement that triggered repair.

Accepted child repair evidence can come from `feature-handoff` or `feature-handoff-after-repair`. A `feature-repair-escalation` packet is blocker evidence, not accepted repair proof.
