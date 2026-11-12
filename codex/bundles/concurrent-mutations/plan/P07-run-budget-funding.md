# Run accounting and atomic child funding

Design before implementation, following shared neutral content. A local policy ceiling is a per-step limit; it cannot independently recreate a shared run allowance.

## Chosen mechanism

Use the existing bounded balance/allocation arithmetic and root-fenced unit of work. Generalize a budget's real step owner to nullable: null denotes the real root runtime owner, never an empty/synthetic step. Keep legacy step budgets and receipts unchanged when no funding is admitted.

Retain a separate immutable root admission with its explicit budget ID, selected database profile, real run ID, workspace namespace/head, definition version, original workspace/definition/invocation policy inputs, capabilities, enclosing ceilings and resolved policy. Initial admission is part of the prepared launch fingerprint and only creates a root budget with the initial real runtime state. It is not inferred from the first step.

A new versioned step admission binds the original root budget. Its step ceiling remains the admitted policy ceiling. Each local work reservation also reserves exactly that grant from the root in the same transaction; settlement updates both with the same closed owner evidence. This dynamic funding avoids arbitrary initial partitions and preserves concurrency across siblings within the one finite total. Parent capture and orchestration use explicit root reservations. Parent-shared refresh charges the root's replan once; local run-snapshot children cannot independently reserve replans.

The parent allocation ID is a deterministic, domain-separated function of the real child budget ID and local allocation ID. The local allocation retains its exact parent allocation FK; a unique reverse index prevents reuse. An existing unrelated parent allocation cannot be adopted. A direct root command cannot settle/reserve an allocation owned by a funded child. Unknown child outcomes remain reserved; overruns are recorded in both balances and stop further reservations.

The receipt retains both local and parent results. New root/funded receipts use an explicit new wire version; exact legacy step bytes remain unchanged. Duplicate runtime commands return their original receipt even after siblings change the root balance.

## Boundaries, concurrency and bounds

Root policy, funding references and identity derivation belong to Runtime. EF entities, canonical codecs and staging belong to Persistence. No provider, journal, permission callback or child scan occurs inside SQL. Existing source/project locks precede the root fence. Funding loads are exact indexed budget/allocation lookups. Root serialization is necessary for the shared finite total; unrelated roots remain independent.

The root has the existing 256-allocation total bound. It includes captures, orchestration and all descendant local work; per-step 256-allocation limits do not multiply the root capacity. Exhaustion is an explicit refusal. Settled allocations are never removed/reused to reset this bound.

Nested assignments may bind the same root budget only after the existing real parent-step launch precondition has validated lineage. They do not create another root allowance. Their immutable policy records retain the original root policy ceiling and workspace identity. No legacy running Process is silently upgraded.

## Versioning and validation

Old step admission/receipt bytes remain exact; old readers reject the new funded admission and root receipt versions. New SQL null-step, root-admission and funding FKs/checks preserve original rows. Downgrade refuses new admissions, root budgets, funding and receipts, including prepared launches before execution.

Test pure strict codecs/identity, original prepared admission, missing/wrong profile/root/scope, sibling overspend races, atomic rollback, reservation/settlement replay after restart, root capture accounting, unknown outcomes, recorded overrun, direct-parent bypass attempts, child allocation substitution, terminal/cancelled root refusals, migration round trips and downgrade refusal. Preserve existing generation/material/publication regression. This accounting prerequisite does not itself enable a governed executor.

## Compatibility and implementation seams

Prepared launch serialization currently ignores unknown members. A new optional root-admission field therefore also requires a distinct preparation hash domain; merely adding that property would let an old reader miss it. Funded step policies require a new admission schema, and their old reader must reject it. The root/funded receipt version is likewise explicit.

Keep cross-ledger funding in a cohesive internal Persistence collaborator. The existing budget store orchestrates it through the same DbContext/transaction; shared row mapping stays in ProcessDecisionRows. Do not add another partial runtime manager, service locator, generic transaction abstraction or project.

Initial root admission is allowed only for RunId == RootRunId. Nested launches reuse that root budget after the existing typed parent-step precondition validates real runtime lineage. Original command replay must also compare initial root policy, so an ordinary duplicate command cannot omit/add/replace the admission outside the prepared-launch path.

Root cancellation/terminal state refuses new funded reservations even when a descendant's local state has not caught up. Closed settlements remain allowed. New direct root allocation commands cannot touch parent allocations already owned by a child; deterministic identity alone is not proof of ownership.

The root budget retains its original admission command ID with a restrictive reference to the runtime command receipt. Duplicate handling compares that original admission (including omission/addition) before returning the old result. This avoids copying the entire root policy into every later command receipt or weakening legacy absent admission.

## Implementation review

The implementation keeps a single internal funding collaborator and shared row mappings;
it adds no project reference or runtime partial. Root admissions are bounded to 32 KiB,
funded receipts to 8 KiB, provenance to 32 enclosing budgets, and root allocations to 256.
Shared RunSnapshot children must retain the root read, publication and basis-change policy.
Local StepSnapshot policy resolution remains explicit within the original workspace and
enclosing limits.

The first database candidate exposed a legacy SQL projection defect: an absent optional
parent could not populate non-nullable metadata fields. The correction makes the projected
parent IDs and overrun flag nullable; a funded row still requires the exact complete parent
binding. All selected tests are repeated after this production correction. The other
failure was a test expecting a serialization exception where the existing strongly typed
run identifier rejects an empty GUID with its argument exception. Neither correction
changes funding amounts or accepts incomplete owner evidence.

The next prerequisite is [active run material scope ownership](P07-active-run-material-scope.md).

The [repaired implementation checkpoint](../validation/2026-10-19-process-run-budget-funding.md)
passes all 361 selected cases with direct builds, migration/model checks, final portability,
architecture review and complete-command resource observation. Producer grant sizing and
fair scheduling remain explicit follow-up work before actual parallel runtime activation.
