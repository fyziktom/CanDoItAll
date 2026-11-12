# Shared allocation arithmetic boundary

Architecture correction before durable-owner implementation, after `bc6dbdbfa`.
Implemented with [28 passing unchanged cases and dependency proof](../validation/2026-10-19-shared-decision-budget.md).
The [first arithmetic checkpoint](../validation/2026-10-19-process-decision-budget.md)
proved 28 pure cases but placed its values in Process Runtime. The requested Workflow
and Agent delegation work also needs these calculations. Making those engines reference
Process Runtime, or duplicating settlement/overrun rules in each engine, is rejected.

Move only the pure balance, allowance, allocation and transition types to the existing
SharedKernel `Concurrency` area beside `DecisionDataPolicy`. Give the balance an
owner-neutral `DecisionBudgetId`, distinct from a Process semantic decision identity.
Keep Process decision scope, generations, source authority, claims, manager actions,
SQL transactions and reconciliation in their existing engine/application/persistence
owners. No generic coordinator, storage port, service registration or new project is
part of this move. A budget identity alone is never dispatch or publication authority.

This is a real shared domain rule: reserved work is unavailable to another child;
exact replay cannot double-charge/refund; unknown work remains reserved; closed owner
evidence is needed for settlement; and observed overruns remain charged. Those rules
must not diverge between the required Process, Agent and Workflow integrations. Their
actual journals and evidence-verification methods remain separate. The move does not
claim that any of those durable integrations is already implemented.

## Dependency proof and compatibility

| Project | Current path to policy primitives | Intended change |
| --- | --- | --- |
| SharedKernel | No project or package references | Own the small pure arithmetic; retain zero outward references |
| Process Contracts | Direct SharedKernel reference | No reference change |
| Process Runtime | Contracts → SharedKernel | Remove the local arithmetic definitions; no reference change |
| Agent Models | Direct SharedKernel reference | Future owner can consume the primitive without Process dependency |
| Workflow Abstractions | Agent Models → SharedKernel | Future owner can consume the primitive without Process dependency |

Source search confirms only the two new Runtime files and their new Unit test consume
the unreleased names. No stored budget row, public HTTP contract, production caller or
released package needs a compatibility alias or migration. Do not leave forwarding
types in Runtime: that would preserve the misleading ownership boundary.

Use the dependency-graph audit before and after the move, including project inventory
and cycles. Build SharedKernel, Runtime and the owning Unit assembly in standard Release.
Move the same 28 cases to the neutral namespace, confirm discovery and execute them.
Retain a source comparison showing only owner/type/identity names and namespace changed
in the calculation; any semantic change requires separate review. Full portability,
documentation and rendering-register checks remain required.

The 256-allocation cap still needs durable-owner capacity qualification. The arithmetic
does not specify retention deletion, verify child journals, choose material scopes or
linearize concurrent writers. Those remain the required subsequent owner integrations.
