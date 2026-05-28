# SB17 Semantic Invariants

## Invariants

- Invariant ID: `SB17-INV-001`
- Source raw note: RN12 - documentation, templates, skills, and API examples must stay source-aligned before final closure.
- Expected behavior: Template README and process API skill guidance name the current source enum values for operation contracts, target scopes, block causes, recovery options, and artifact expectation statuses, and template governance tests still pass.
- Disallowed shallow implementation: updating prose without comparing source enums/DTOs, leaving active skill root stale after repo skill changes, or treating template README guidance as current while governance tests fail.
- Failing-first test: bundle://proof/SB17/transcripts/failing-first.txt records that the pre-change docs did not contain exact enum parity markers for `ProcessStepTargetScope`, `ProcessStepRecoveryOption`, or `ProcessArtifactExpectationSatisfactionStatus`.
- Passing test: bundle://proof/SB17/transcripts/passing.txt records entry gate, source-enum parity script, isolated integration build, 10 passing `ProcessTemplateGovernanceTests`, and active skill hash sync.
- Changed source files: repo://Templates/Processes/README.md and repo://codex/skills/candoitall-api-processes/SKILL.md; active skill copy synchronized at `C:\Users\lucys\.codex\skills\candoitall-api-processes\SKILL.md`.
- Production assertions: Docs and skill include all `ProcessStepOperation`, `ProcessStepTargetScope`, `ProcessStepBlockCause`, `ProcessStepRecoveryOption`, and `ProcessArtifactExpectationSatisfactionStatus` values parsed from current source.
- Red-team negative case: SB18 cannot claim docs/template/API readiness if a source enum value is missing from the operator-facing README/skill guidance or the active skill hash diverges from the repo skill.
- Downstream dependency check: SB18 may start with docs/template parity and active skill sync proved.

## Production Behavior Artifact Matrix

| artifact | producer | consumer | lifecycle | negative |
| --- | --- | --- | --- | --- |
| Template README enum parity guidance | `repo://Templates/Processes/README.md`. | Template authors and governance tests. | Updated when process operation, target scope, block cause, recovery option, or artifact status enums change. | Pre-change marker absence in `bundle://proof/SB17/transcripts/failing-first.txt`. |
| Process API skill enum parity guidance | `repo://codex/skills/candoitall-api-processes/SKILL.md` plus active skill root copy. | Codex operators using HTTP process routes. | Repo copy is synchronized to active skill root after edits. | Hash parity proof in `bundle://proof/SB17/transcripts/passing.txt`. |
| Template governance proof | `ProcessTemplateGovernanceTests`. | Template maintainers and final release readiness. | Validates manifest contracts, live-run profiles, baseline scenarios, mappings, and template vocabulary. | Passing 10/10 proof in `bundle://proof/SB17/transcripts/passing.txt`. |

## Validation

- Failing-first/adversarial proof: bundle://proof/SB17/transcripts/failing-first.txt.
- Passing proof: bundle://proof/SB17/transcripts/passing.txt.
- Source assertions: bundle://proof/SB17/transcripts/source-assertions.txt.
- Anti-stub audit: bundle://proof/SB17/transcripts/anti-stub-audit.txt.
- Changed-file hashes: bundle://proof/SB17/transcripts/changed-file-hashes.txt.
