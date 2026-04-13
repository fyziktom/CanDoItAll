# Architecture review gate C

## Purpose

Stop after lifecycle and side-effect hardening, then decide whether the remaining work is only structural follow-up rather than unresolved correctness.

## Required deliverables
- A written Gate C memo with explicit pass/fail decision.
- A statement of whether all red correctness/invariant gaps are now closed.
- A corrective subbundle if lifecycle or side-effect hardening is still incomplete.

## Repository touchpoints
- `02-open-findings.md`
- `templates/review-gate-memo-template.md`
- `subbundles/07-definition-lifecycle-invariant-hardening/README.md`
- `subbundles/08-transactional-side-effects-and-outbox-alignment/README.md`

## Validation commands
- `Review the live repository and proof after subbundles 07-08 before continuing.`

## Review questions
1. Are single-draft, single-published, active-version safety, and version allocation now hard invariants rather than service assumptions?
2. Are search/activity side effects now durable enough that command semantics are no longer post-commit fragile?
3. Can the remaining work honestly be treated as structural follow-up instead of unresolved correctness?

## Corrective trigger

If any answer is no, stop and execute the lifecycle or side-effect corrective playbook before continuing.

## Corrective template

- `subbundles/_corrective-lifecycle-reset`

## Gate notes

This gate is the decision point between “still correctness work” and “now only architecture shaping remains”.
