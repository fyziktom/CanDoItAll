# Closure decisions and evidence semantics

Use three separate decisions:

1. **Workspace rendering complete:** the existing structural separation/census remains valid.
   This is already supported by the reviewed source and maintained records, but must be
   refreshed when final code is changed. It does not mean all application tests pass.
2. **Product fixes closed:** known WCL-DEL1 and WCL-NAV1 have controlling, cause-specific proof
   and the previous fixes remain valid. A documentation map alone cannot close either.
3. **Full application readiness:** current required campaign groups and intended dependency
   delivery are actually verified on the accepted source pair. Missing live permission or an
   external fixture is a separate blocker, not a product defect or an implied waiver.

The inherited `ready_for_next_module` field remains the strict complete-readiness decision.
Do not set it merely because local fixes are good, a branch was pushed, or a targeted test
passed. Report the narrower product decision even when full readiness is blocked only by
external prerequisites. At that point do not keep modifying healthy code to solve permission
or publication gaps. A future explicit operator scope decision can be recorded separately;
this handoff does not manufacture that decision.

All 35 prior groups remain in the new plan. FIX-DEL, FIX-NAV and DOC-MAP add three obligations.
`product_fix_groups` includes the four retained fix families plus DEL/NAV; product_fixes_closed
cannot be true while one lacks a pass or an open product finding remains. New templates contain
NOT_RUN implicitly through empty attempts. Do not edit sealed input templates in place; use
an external working copy and append attempts.

## Attempt record

Each executed attempt names its command, UTC range, actual source checkpoint (app, Components,
FileTools and source-manifest digest), mode, execution kind, raw counts when applicable,
safe owner/UI evidence and artifact hashes. Keep discovery/aborted/not-run attempts distinct.
A live pass also needs separate explicit authorization, durable request reservations, real
provider journal activity, and original owner effects. Synthetic local proxy success or
HTTP 200 alone is insufficient.

A selected attempt may not hide a later failure. A complete failed run plus a targeted repair
is not a newly executed all-green full run. Changes after a freeze require an explicit scope
comparison and appropriate revalidation, not relabelled timestamps. Prefer a clean final
frozen run after all repairs have settled.

## Required output records

Update the existing DEL/NAV finding documents and maintained critical-closure record with
append-only histories, actual causes, diffs and test evidence. Update current modules.md and
the module census. Keep generic helper scripts out of production projects. Produce safe
source/asset/dependency manifests, full-case inventory and prerequisite list, exact local
versus remote delivery status and a concise operator decision. No new module is implemented.

Retained original private evidence may be referenced by hash and path when actually available.
When missing, say so; do not reconstruct original logs, token counts or rollback facts.
The included validators check structure/hashes and constraints only. They do not certify
that an operator authorized work, that a screenshot proves behavior or that a test ran.
