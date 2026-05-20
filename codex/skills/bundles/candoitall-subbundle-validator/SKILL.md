---
name: candoitall-subbundle-validator
description: Validate a CanDoItAll subbundle before implementation starts and after proof is captured. Use when Codex must confirm prerequisites, dependency impact, and progression-gate quality so downstream work does not proceed on weak foundations.
---

# CanDoItAll Subbundle Validator

Use this skill before and after each subbundle. It exists to stop dependency mistakes early, when they are still cheap to fix.

## GPT-5.5 Gate Posture

- Validate the current subbundle against the actual bundle files and repo state, not memory from earlier conversation turns.
- Keep the decision compact: `Pass`, `Fail`, or `Blocked`, followed by the exact prerequisite, proof, or downstream gate issue that drives the result.
- Treat the progression gate as the durable state handoff for the next agent or resumed session.
- If proof is weak but the work may still continue, record the explicit risk and the dependent subbundle that must re-check it. Do not silently lend trust to downstream work.

## Required Flow

1. Read the root `README.md`, `plan/01-phase-plan.md`, the selected subbundle README, and the relevant traceability rows.
2. Run the entry gate before editing:
   - confirm the current subbundle still owns the intended inputs
   - confirm every listed prerequisite is complete and still trusted
   - confirm the exact source references still match the repo
   - confirm any critical foundation it depends on has strong enough proof for downstream work
3. If the entry gate fails, stop. Repair the bundle or reopen the prerequisite phase before implementing.
4. After implementation, run the closure gate:
   - acceptance checklist and proof required are complete
   - tests, builds, Playwright proof, screenshots, and host proof ran when required
   - screenshot review questions were actually answered, not only captured
   - `## Browser Validation Analytics` and `## Subbundle Gate Results` were updated while the proof was fresh
   - critical subbundles include Semantic Adequacy Gate evidence for shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, and raw-note literal closure
   - critical subbundles include `proof/SBxx/manifest.md`, and every transcript, hash, source assertion, browser, host, smoke, or red-team path cited by that manifest exists
5. If the subbundle is a critical foundation, run one dependent-flow smoke or dependent-surface validation before allowing the next subbundle to start.
6. If later work exposes a defect in the current subbundle, reopen it immediately and rerun the closure gate after repair.

## Rules

- Do not start work because the next file looks easy when the prerequisite proof is weak.
- Do not pass the closure gate when browser proof is missing or visually wrong.
- Do not let a later subbundle bury evidence that an earlier foundation was incomplete.
- Treat `Progression Gate` as a real stop sign, not as bundle decoration.
- Do not pass a critical closure gate when proof only checks structure, counts, status flags, non-empty output, or template markers instead of domain behavior.
- Do not pass a critical closure gate when the proof manifest is missing, cites missing files, omits changed-file hashes, or lacks failing-first and passing transcripts for behavior-changing work.

## Semantic Adequacy Closure Rule

Closure for a critical subbundle is `Fail` unless the proof names the shallow-pass trap and demonstrates both:

- an adversarial negative case that the shallow implementation would mishandle
- a semantic positive case that represents the intended realistic behavior

The closure gate also fails when the anti-stub audit is missing or when raw-note closure silently narrows literal request language. Use `../candoitall-bundle-execution/references/semantic-adequacy-proof.md` as the checklist.

Artifact-backed proof is part of the closure gate. A critical subbundle with only execution-report prose, table rows, or uncited command names is `Fail`; repair the proof manifest before downstream work starts.

## References

- Read [references/prerequisite-and-closure-gates.md](references/prerequisite-and-closure-gates.md) for the phase checklist.
- Read [../candoitall-bundle-execution/references/semantic-adequacy-proof.md](../candoitall-bundle-execution/references/semantic-adequacy-proof.md) when validating a critical subbundle.
- Read [../candoitall-bundle-execution/references/artifact-backed-proof-manifest.md](../candoitall-bundle-execution/references/artifact-backed-proof-manifest.md) when validating critical proof manifests.
- Use `candoitall-watch-playwright-loop` when the proof depends on fast nearby browser validation.
- Use `candoitall-bundle-validator` for bundle-level readiness and final closure.

## Exit Condition

The subbundle passes only when its prerequisites, proof, and downstream progression decision are explicit enough that the next phase can proceed without borrowing trust from wishful thinking.
