# 02A frozen regression entry
Baseline source unchanged at 7684f25854594f4a4b5486559890164aec382fb7. Initial component filter: FullyQualifiedName~ProviderMutationRegressionTests, expected 3 cases:
- New_commit_projection_failure_retains_identity_without_another_write (1).
- Pending_save_owns_submission_and_preserves_later_edits(firstSave false/true) (2).

Discover before execution. Expected meaningful failures: missing committed ID, aliased submission/lost later context. Correct selector/fixture failures before counting any failing-first proof. Local logs under ignored .mcp-state/p02a-*; compact final hashes/transcript/semantic invariants are written at closure. Effective SDK 10.0.303 via global.json latestPatch.

## New contract checks
Frozen before execution: FullyQualifiedName~ProviderEditorSubmissionTests|FullyQualifiedName~ProviderEditorOperationsTests; expected 16 cases (2 submission, 14 operations). Exact public test names are the current named methods with InlineData in those two sources. No counts are architecture invariants. Discovery must match before execution.

## Real adapter boundary checks
Frozen before execution: FullyQualifiedName~ProviderMutationCommitIntegrationTests; expected 7 cases (four observer/projection × save/delete cases and validation/cancellation/concurrency). Uses the registered PostgreSQL host, real registry, guards, snapshot materializer and observers; only the failing secondary observer/store effect is injected. Receipt fakes are not used for commit claims.

## Final disposition
Status: complete for provider-owned obligations. The historical documentation gate blocker is explicit in bundle://reviews/closure.md.
Owned invariant contract: bundle://proof/SBA/semantic-invariants.md.
Portable commands and before/after failure output: bundle://proof/SBC/transcripts/.
Case receipts and all 31 topic bindings: bundle://proof/SBC/test-receipts.json and bundle://proof/SBC/topic-map.json.
Changed-source before/after hashes: bundle://proof/SBC/file-manifest.json.
Production assertions and final adversarial review: bundle://reviews/csharp-architecture-gate.md.
Anti-stub command/result: bundle://proof/SBC/transcripts/anti-stub.txt.
Downstream real API, shared workspace and browser proof: bundle://proof/SBC/browser/acceptance.md.
Final closure, exclusions, fixture corrections and unresolved repository-wide baseline debt: bundle://reviews/closure.md.
