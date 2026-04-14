# Verdict and scope

## Verdict

The Process module is **closer to acceptable architecture** than in the previous two reviews, but I would still **not** sign off the architecture as closed.

### What is genuinely better now

The repository now contains real improvements that are worth keeping:

- canonical dependency truth is no longer split across core entity/editor/runtime step types;
- the dependency-compatibility bridge is now pushed to import/export boundaries instead of polluting core models;
- definition lifecycle invariants are materially stronger than before;
- save/publish/start-run flows are transactional;
- durable Process outbox behavior is now present and covered by integration tests;
- query seams have been extracted so `ProcessesService` is not carrying every read projection directly.

### Why closure is still premature

The remaining gaps are no longer the old ones. They are now narrower, but still important:

1. The Process graph is still allowed to be **illegal**. Cycles and self-loops are not rejected strongly enough.
2. Runtime services assume singular rows that the schema still does not fully enforce.
3. `ProcessWorkspace` still has a real pending-autosave ordering problem around publish/delete/export.
4. The published-only editor path still has a stale-write hole.
5. Query cohesion, helper isolation, and targeted performance cleanup are not finished.

## Scope of this follow-up

This bundle is intentionally narrower than the earlier hardening bundles. It focuses only on the still-open gaps that matter for correctness, long-term maintainability, and realistic scale.

Out of scope unless directly required by a stop rule:

- broad renaming or stylistic rewrites;
- module-wide architectural reinvention;
- unrelated feature work;
- “cleanup for cleanup’s sake” outside the named findings.
