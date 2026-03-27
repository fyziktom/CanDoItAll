---
name: candoitall-bundle-preparation
description: Prepare structured, implementation-ready CanDoItAll bundles from raw requests, testing feedback, docx notes, screenshots, or mixed artifacts. Use when work is large, multi-phase, UI-heavy, cross-project, risky, or ambiguous enough that Codex should create a bundle with normalized requirements, phased subbundles, prompts, traceability, proof rules, and self-review before implementation starts.
---

# CanDoItAll Bundle Preparation

Prepare the bundle first. Do not implement feature code while preparing the bundle.

Turn messy inputs into a bundle that an implementation agent can execute without rediscovering the problem. Save the raw inputs, structure them, split the work into explicit subbundles, and validate that the bundle is complete before handing it off.

## Choose The Bundle Profile

- Use the `feedback` profile for testing notes, QA findings, screenshots, docx review docs, or a short list of concrete issues.
- Use the `initiative` profile for migrations, refactors, new features, architecture work, cross-repo consolidation, or anything that needs inventories and templates in addition to the baseline bundle structure.
- Start from `feedback` unless the task clearly needs architectural decomposition across multiple workstreams.

## Required Flow

1. Save the raw request and every source artifact under `inputs`.
2. If the source includes `.docx`, use `scripts/extract_docx_feedback.py` to extract text before summarizing it.
3. Normalize the task into explicit objectives, hard constraints, assumptions, risks, and validation expectations.
4. Build the current-state analysis from the real repo, not from the user’s memory of the repo.
5. Create the bundle structure with `scripts/scaffold_bundle.py` or mirror that structure manually if the bundle already exists.
6. Split execution into numbered subbundles. Every subbundle must be independently actionable and should own a coherent slice of work.
7. Write reusable implementation and QA prompts under `shared-prompts`.
8. Complete traceability so every requirement points to at least one concrete bundle file.
9. Run `scripts/validate_bundle.py` before declaring the bundle ready.
10. Finish the self-review from QA, architect, and manager perspectives. Do not mark the bundle ready while any of those three reviews is incomplete or inconclusive.

## Bundle Contract

Always create these root sections:

- `README.md`
- `inputs/`
- `analysis/`
- `requirements/`
- `architecture/`
- `plan/`
- `traceability/`
- `shared-prompts/`
- `subbundles/`
- `reviews/`

Create these additional sections when the task needs them:

- `inventories/`
- `templates/`
- `evidence/`

The `initiative` profile usually needs `inventories` and `templates`. The `feedback` profile usually does not.

## Subbundle Contract

Every subbundle README must include:

- `## Objective`
- `## Covered Inputs` or `## Covered Notes`
- `## Exact Source References`
- `## Deliverables` or `## Scope`
- `## Implementation Steps`
- `## Do Not Do`
- `## Acceptance Checklist`
- `## Proof Required`
- `## Suggested Agent Prompt`

Do not create vague “misc cleanup” or “remaining fixes” buckets. If the work cannot be named precisely, the bundle is not ready yet.

## Quality Bar

- Use the smallest complete bundle that still removes ambiguity for the implementation agent.
- Keep source references exact. A future agent should be able to open the right files immediately.
- Make acceptance criteria observable. Prefer concrete browser, test, build, and artifact proof over subjective claims.
- Expand UI validation beyond “does it work.” Require readability, spacing, hierarchy, alignment, affordance, and shared-component usage checks.
- Preserve the rule from the successful packs: the bundle is a coordination artifact first, not a place to sneak in implementation work.

## References

- Read [references/bundle-profiles.md](references/bundle-profiles.md) before choosing the structure.
- Read [references/subbundle-contract.md](references/subbundle-contract.md) while splitting work.
- Read [references/bundle-validation-rubric.md](references/bundle-validation-rubric.md) before the final self-review.
- Use `scripts/scaffold_bundle.py` to create the initial folder skeleton.
- Use `scripts/extract_docx_feedback.py` when the source artifact is a `.docx`.
- Use `scripts/validate_bundle.py` before declaring the bundle implementation-ready.

## Output Rule

The bundle is only ready when a different implementation agent could execute it phase by phase without guessing what to change, how to prove it, or how to record completion.
