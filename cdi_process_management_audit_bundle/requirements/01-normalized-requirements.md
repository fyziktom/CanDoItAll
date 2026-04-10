# Normalized Requirements

## BRQ-001 Bundle Repair

- Convert the flat audit pack into a validator-compliant initiative bundle with real subbundles, dependency mapping, execution report sections, and gate definitions.

## BRQ-002 Stale-Audit Reconciliation

- Preserve the legacy audit artifacts as source evidence and explicitly reconcile which backlog claims are still live, which are already implemented, and which are deferred with follow-up paths.

## BRQ-003 Typed Branch Outcomes

- A process step must be able to define multiple explicit branch outcomes for switch-style routing.
- Branch routing must be strongly typed and driven by persisted identifiers, not free-text comparisons.

## BRQ-004 Decision-Maker Role Ownership

- A branching source step must store an explicit role requirement that owns the routing decision.
- That decision-maker role can later resolve to human, AI, algorithmic, or other executor kinds through the existing role-to-assignment/runtime path.

## BRQ-005 Outcome-Bound Downstream Routing

- A downstream step must be able to depend on a source step and optionally on one specific branch outcome from that source step.
- The model must support more than two outcomes and must not be limited to yes or no branching.

## BRQ-006 Publish And Definition Validation

- Publish validation must reject invalid branch references, missing decision-maker ownership for branching steps, and branching definitions that cannot be routed deterministically.

## BRQ-007 Runtime Branch Execution

- When a branching step completes, runtime must require an explicit selected outcome when needed.
- Runtime must activate the correct next step or steps for the selected outcome.
- Runtime must resolve non-selected mutually exclusive branch steps deterministically so run completion stays trustworthy.

## BRQ-008 MCP And Read Model Support

- Runtime and MCP contracts must expose enough branch metadata for external or UI callers to choose a valid outcome safely.

## BRQ-009 Workspace Authoring Support

- The definition workspace must allow authoring branch outcomes, selecting the decision-maker role, and binding downstream steps to a dependency outcome.

## BRQ-010 Runtime Workspace Support

- The runtime workspace must allow an operator to choose a branch outcome before completing a branching step and must reflect the resulting path clearly.

## BRQ-011 Canvas Consistency

- Definition and runtime canvas views must reflect the real dependency graph instead of a fake purely sequential flow when branching is present.

## BRQ-012 Real Validation

- Completion requires prepared-stage and completed-stage bundle validation, targeted .NET validation, and real browser proof for the UI work.
