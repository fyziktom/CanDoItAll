# SB10 Red-Team Verdict

## Verdict

- Result: `Pass`
- Scope: SB01-SB09 proof integrity plus end-to-end cognitive-memory behavior.
- Closure condition: bundle may be marked completed after completed-stage validator passes.

## Process-Proof Attacks

| Attack | Expected defense | Evidence | Result |
|---|---|---|---|
| Prose-only completed subbundle | Completed validator rejects missing proof manifest | `proof/SB10/transcripts/fake-proof-fixtures-still-fail.txt` | Pass |
| Missing transcript artifact | Completed validator rejects missing transcript path | `proof/SB10/transcripts/fake-proof-fixtures-still-fail.txt` | Pass |
| Fake cited test name | Completed validator checks test names appear in transcript output | `proof/SB10/transcripts/fake-proof-fixtures-still-fail.txt` | Pass |
| Missing changed-file hash | Completed validator requires SHA-256 changed-file evidence | `proof/SB10/transcripts/fake-proof-fixtures-still-fail.txt` | Pass |
| Missing failing-first evidence | Completed validator requires failing-first proof or explicit process exemption | `proof/SB10/transcripts/fake-proof-fixtures-still-fail.txt` | Pass |

## Cognitive Behavior Attacks

| Attack | Expected defense | Evidence | Result |
|---|---|---|---|
| Cluster paraphrases only when exact keys match | Composite candidate pair selection and semantic fallback cluster paraphrases | `passing-targeted-end-to-end-quality-tests.txt` | Pass |
| Bridge chain over-merge | Cohesion guard splits unrelated endpoints | `passing-targeted-end-to-end-quality-tests.txt` | Pass |
| Contradiction-only relation lost | Planner routes contradiction-only evidence to review cluster | `passing-targeted-end-to-end-quality-tests.txt` | Pass |
| Dream text copies representative source | Dream synthesis integrates complementary claims | `passing-targeted-end-to-end-quality-tests.txt` | Pass |
| Token-overlap validation accepts negation | Entailment validator rejects reversed/bypass claims | `passing-targeted-end-to-end-quality-tests.txt` | Pass |
| Natural professor teaching ignored | Curator captures structured temporary professor anchor without explicit command | `passing-targeted-end-to-end-quality-tests.txt` | Pass |
| Direct quote assimilates itself | Professor evaluator rejects direct capture proof | Covered by broad unit transcript and SB07 proof | Pass |
| Mastery skipped before fading | Natural E2E uses repeated recall use plus dream integration before scan-driven fade | `passing-targeted-end-to-end-quality-tests.txt` | Pass |
| Recall exposes references by default | E2E asserts `ReferencesShownByDefault` is false and brief has no curator locator | `passing-targeted-end-to-end-quality-tests.txt` | Pass |
| On-demand references over-expand unrelated memory | E2E resolver excludes unrelated coffee-machine memory and includes professor anchor lineage | `passing-targeted-end-to-end-quality-tests.txt` | Pass |

## Scope Guard

- Economic-governance exclusion: `proof/SB10/transcripts/economic-governance-scope-guard.txt`
- Result: no forbidden economic-governance scope terms found in changed cognitive-memory source/test files.

## Residual Risk

- The deterministic dream and entailment logic is still heuristic and intentionally local; future semantic provider integration must stay behind explicit interfaces and keep the existing adversarial tests.
- UI/browser proof was not run because SB06-SB10 changed backend services/tests and bundle docs only, not Blazor bindings.
