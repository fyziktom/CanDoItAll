# Assumptions And Risks

## Assumptions

- A subprocess step references a process definition, not an arbitrary workflow document.
- Child runs use the subprocess definition's active published version when launched from a parent step.
- A process definition can have one manager override that applies to runs of that definition unless a future request introduces role-specific manager policies.
- Existing process outbox and dispatch loops remain the concurrency control point.
- UI routes can open process definitions by project and definition id; if not already present, the route/query contract will be added minimally.

## Risks

- Parent and child state can split if both step run and child run try to own status. The child `ProcessRun` must own child status; parent step status is a projection or synchronized result.
- Subprocess cycles can create unbounded run trees. Run start must reject ancestor cycles or impose a clear hierarchy-depth guard.
- Manager override can become stringly typed if stored as agent names. Store agent ids plus display-name snapshots.
- Template references can become brittle if process keys are not mapped to created process definitions. Template import must resolve subprocess references explicitly.
- UI can become confusing if subprocess nodes look like ordinary work nodes. The visual style must be distinct and tested.

## Critical Path Risks

- Runtime hierarchy schema is the critical path. If `ProcessRun` does not own parent-child truth, downstream reporting and UI will be unreliable.
- Idempotent child run creation is the second critical path. Duplicate children would corrupt process reporting and automation load.
- Template import must resolve subprocess references after definitions exist; otherwise seeded templates will appear valid but fail at run time.

## Validation Risks

- Compile-only proof is insufficient because the risky behavior is orchestration and UI interaction.
- PostgreSQL proof may be environment-dependent; any missing database must be recorded as a blocker and replaced with explicit integration proof.
- Browser proof can be weak if it only loads the page. The validation must exercise context menu, selector, visual style, and new-tab behavior.

## Reopen Triggers

- Any duplicate child run for the same parent step.
- Any code path that derives subprocess truth from both child run and parent step without a clear owner.
- Any subprocess canvas action that can persist a subprocess step without a target.
- Any manager override represented only by display name or prompt text.
- Any failed or skipped test that touches process runtime, canvas, templates, or manager reporting.

## Deferred Risks

- Distributed multi-node scheduling limits are not solved in this bundle beyond reusing existing outbox/lease behavior.
- Full MAF sub-workflow rendering is not required in this bundle. CanDoItAll process canvas remains the process source of truth.
