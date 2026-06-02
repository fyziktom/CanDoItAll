# SB09 - Final Red-Team QA And Release Gate

## Status

Ready for implementation. Classification: **Critical foundation**.

## Objective

Perform final independent QA/red-team review of the entire refactor before marking it complete. Try to falsify proof quality, token accounting, stale lineage rejection, browser proof, workflow side-effect safety, active skill sync, and generic app generation.

## Covered Inputs

Covers senior QA inspection requirement, fake-proof resistance, final closure gate, and release readiness after all hardening subbundles.

## Prerequisites

SB01-SB08 completed with proof manifests. No pending critical blockers.

## Exact Source References

- `bundle://reviews/01-execution-report.md`
- `bundle://proof/SB01/manifest.md`
- `bundle://proof/SB02/manifest.md`
- `bundle://proof/SB03/manifest.md`
- `bundle://proof/SB04/manifest.md`
- `bundle://proof/SB05/manifest.md`
- `bundle://proof/SB06/manifest.md`
- `bundle://proof/SB08/manifest.md`
- `repo://codex/skills/bundles/candoitall-bundle-validator/SKILL.md`
- `repo://codex/skills/bundles/candoitall-bundle-execution/references/artifact-backed-proof-manifest.md`
- `repo://codex/skills/bundles/candoitall-bundle-execution/references/semantic-adequacy-proof.md`

## Deliverables

- Final red-team report.
- Fake-proof resistance artifact.
- Token/cost reconciliation review.
- Stale-lineage replay result.
- Side-effect duplicate/retry review.
- E2E genericity final review.
- Final bundle validator output.
- Release/no-release decision.

## Dependency Impact

Final gate. If SB09 fails, reopen the earliest subbundle whose proof or implementation is invalid.

## Validation Depth

Deep final closure validation. Must include adversarial tests/reviews across all critical subbundles.

## Implementation Steps

1. Read all proof manifests and semantic invariants.
2. Run structural completed-stage validation.
3. Verify proof artifact paths exist.
4. Recompute changed-file hashes.
5. Red-team stale-run proof by attempting to substitute `49fd...` or unrelated artifacts.
6. Red-team browser proof by checking screenshot/run binding.
7. Red-team token accounting by checking finalizer/failure/repair/background cases.
8. Red-team workflow idempotency by replaying controlled duplicate input.
9. Red-team genericity by scanning for scenario-specific branches.
10. Run final relevant builds/tests/Playwright checks.
11. Record final decision.

## Scope Exceptions

No implementation except small fixes to proof/reporting or reopening earlier subbundles. If production code changes are required, reopen the owning subbundle.

## Do Not Do

- Do not close with missing proof manifests.
- Do not accept prose-only proof.
- Do not accept pending browser analytics rows.
- Do not accept usage unknowns hidden in summaries.
- Do not accept reduced E2E scenario count without explicit user acceptance.

## Acceptance Checklist

- [ ] Completed-stage validation passes.
- [ ] All critical proof manifests exist and paths resolve.
- [ ] Red-team fake-proof checks pass.
- [ ] Token accounting reconciliation is acceptable.
- [ ] Workflow side effects are idempotent.
- [ ] Five-scenario genericity audit passes.
- [ ] Final release decision recorded.

## Proof Required


Because this is a critical subbundle, the Semantic Adequacy Gate proof must include:

- `proof/SBxx/manifest.md`
- `proof/SBxx/semantic-invariants.md` or `.json`
- changed-file hashes
- command transcript paths
- source assertions
- shallow-pass trap
- adversarial negative proof
- semantic positive proof
- anti-stub audit
- raw-note literal closure
- dependency smoke proof where stated

Final closure must include `proof/SB09/final-red-team-report.md`, `proof/SB09/fake-proof-resistance.md`, and `proof/SB09/final-validator-output.txt`.


## Browser Validation Logging

Required if any UI/browser proof is revalidated. Summarize replayed routes, screenshots, console evidence, and result.

## Progression Gate

SB09 passes only when a skeptical reviewer can rerun/inspect proof and find no fake-proof, stale-lineage, usage-undercount, side-effect, or genericity blocker.

## Suggested Agent Prompt

Execute SB09 as an independent verifier. Try to break the proof. Reopen earlier subbundles instead of smoothing over defects.
