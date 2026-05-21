# Assumptions And Risks

## Working Assumptions

- The current repository snapshot is the source of truth for this follow-up bundle.
- The bundle workflow skill should be considered part of the product quality system, not documentation only.
- Provider-backed semantic functionality must have deterministic fallbacks and fake providers for tests.
- Czech/diacritic support is required for natural professor learning because the user frequently teaches and reviews in Czech.
- Existing DB entities may be extended, but migrations and backward compatibility must be handled explicitly.

## Critical Path Risks

- Codex may again rename lexical heuristics as semantic/embedding behavior without actual provider integration.
- Codex may add tests that call services directly while no application workflow ever calls them.
- Codex may keep dream text non-empty and structured while still producing meta-statements about sources rather than domain knowledge.
- Refactoring may split files but preserve tangled responsibilities unless service boundaries are explicitly asserted.
- Proof artifacts may pass locally but fail in a moved checkout or CI due to absolute machine-specific paths.

## Validation Risks

- Current validator checks proof shape more than behavior semantics.
- Existing tests can pass with English keywords embedded in otherwise Czech messages.
- Claim source-map tests may count source maps but not verify that unrelated evidence anchors are excluded.
- Approximate clustering tests may use shared lexical aliases and therefore fail to prove embedding discovery.

## Reopen Triggers

- Any proof manifest contains `C:\`, `/home/`, `/mnt/`, active skill root paths, or other machine-specific paths.
- Any execution report uses labels such as `embedding-backed`, `Czech/diacritic`, `provider-backed`, `automatic`, `claim-specific`, or `line-level` without code-level source assertions that prove the literal behavior.
- Any dream aggregate text contains `source claims`, `mapped source claims`, `consistently described`, `supported by N`, or similar diagnostic wording.
- Accepted-use events are produced only by direct tests, manual seed helpers, or service calls with no application outcome-event integration.

