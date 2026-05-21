# Proof gates for production behavior closure

## Status

- Status: `Completed`

## Objective

Install stronger workflow and validator gates so Codex cannot close behavioral work with consumer-only code, seeded tests, or prose proof.

## Covered Inputs

- Current code review findings in `analysis/01-current-state.md`.
- Normalized requirements in `requirements/01-normalized-requirements.md`.
- Source artifact inventory in `inputs/01-source-artifacts.md`.

## Prerequisites

- Prepared bundle root and current repository checkout.

## Exact Source References

- repo://codex/skills/bundles/candoitall-bundle-workflow/SKILL.md
- repo://codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py
- repo://codex/bundles/cognitive-memory-semantic-portability-and-learning-depth-followup/reviews/01-execution-report.md
- repo://codex/bundles/cognitive-memory-semantic-portability-and-learning-depth-followup/proof/SB08/manifest.md
- repo://codex/bundles/cognitive-memory-semantic-portability-and-learning-depth-followup/proof/SB08/transcripts/source-assertions.txt
- repo://src/CanDoItAll.Modules.CognitiveMemory/Signals/CognitiveMemorySignalContracts.cs

## Deliverables

- Update `candoitall-bundle-workflow`, `candoitall-bundle-execution`, and validator references to require producer/consumer/lifecycle proof for new domain artifacts.
- Extend `validate_bundle.py` completed-stage checks to detect fake proof where a signal/state is only an enum plus consumer plus test seed.
- Add fake-proof fixtures: one must fail for consumer-only `ProfessorAnchorAcceptedUse`, another must fail for template dream synthesis meta-text.
- Document that production-only signals must not be manually seeded by positive tests except explicit migration/fixture tests.

## Dependency Impact

- This subbundle must update downstream proof, tests, and traceability rows that depend on its behavior.
- If implementation discovers a stronger or safer design, repair this README and rerun prepared-stage validation before proceeding.

## Validation Depth

- Use failing-first tests for behavioral changes.
- Use artifact-backed proof manifests for completed critical subbundles.
- Include production source assertions, not only tests or prose.
- Include anti-stub audits and red-team negative cases.

## Implementation Steps

- Add a `Production behavior artifact matrix` requirement to proof manifests and semantic invariant contracts.
- Teach the validator to require producer, consumer, lifecycle, and negative test citations when invariant text names a new signal/state/record.
- Add completed-stage fixture bundles under the validator test area and prove the bad fixtures fail.
- Run prepared and completed validators from a moved checkout path to preserve portability.

## Do Not Do

- Do not accept grep proof that shows only enum, evaluator, and tests.
- Do not let `source-assertions.txt` satisfy producer proof unless it contains a production emitter path.
- Do not mark process-only exemptions for production behavior.

## Acceptance Checklist

- A consumer-only accepted-use fake bundle fails completed validation.
- A template-only dream synthesis fake bundle fails completed validation.
- The workflow skill tells Codex to install and obey the updated skill before feature subbundles.

## Proof Required

- Completed: `bundle://proof/SB01/manifest.md` with changed-file SHA-256 hashes.
- Completed: `bundle://proof/SB01/semantic-invariants.md`.
- Completed: `bundle://proof/SB01/transcripts/failing-first.txt`.
- Completed: `bundle://proof/SB01/transcripts/passing.txt`.
- Completed: `bundle://proof/SB01/transcripts/source-assertions.txt` with producer, consumer, and lifecycle matrix assertions.
- Completed: `bundle://proof/SB01/transcripts/anti-stub.txt`.

## Browser Validation Logging

- Backend-only changes may record `N/A` with reason.
- If curator/professor review UI or routes are changed, add Playwright route, viewport, actions, screenshots, and assertions to `reviews/01-execution-report.md`.

## Progression Gate

- Passed for SB02: fake consumer-only accepted-use and template-only dream synthesis fixtures fail completed-stage validation, the positive proof-depth fixture still passes, the active skill root is synchronized, and artifact-backed proof is recorded in `bundle://proof/SB01/manifest.md`.
- Reopen this subbundle if later tests reveal a shallow pass, producer-less signal, stranded lifecycle state, or broad provenance mapping.

## Suggested Agent Prompt

Implement Proof gates for production behavior closure. Start by reading this README, then inspect every exact source reference. Create failing-first proof where required, implement the production behavior, update tests, record proof artifacts, and only mark the subbundle completed when the acceptance checklist and proof manifest are satisfied.
