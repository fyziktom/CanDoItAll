# Shared coordination failing-first entry
Frozen before execution, shared production source unchanged from accepted Providers-01:
- FullyQualifiedName~SharedProviderLifetimeRegressionTests: 3 cases, target replacement late success/failure and disposal cancellation.
- FullyQualifiedName~Opening_sharing_without_publishing_does_not_create_identity_or_block_delete: 1 PostgreSQL case.
The local mutation boundary already has green 16 Unit and 3 Components cases; real commit selection is running. No old proof is relabeled.

Result: shared lifetime 3/3 meaningful failures (stale success rendered A; both replacement and disposal did not cancel owner token); publication read 1/1 meaningful failure (unexpected new permanent publication row). In the same PostgreSQL selection the 7 corrected local commit cases passed. Logs/TRX under ignored .mcp-state/p02*. No secrets used.

Frozen added Unit selection before execution: FullyQualifiedName~ProviderSharedReconciliationTests, expected 23 (16 editable draft × operation-kind cases, 2 affected-ID cases, retirement, unknown scope, 2 missing/malformed, immutable change scope). Tests use public session and read/change contracts.

Frozen new real-adapter/API selection before execution: FullyQualifiedName~.Seam_, expected 11 cases: 9 shared runtime projection cases (including alias/retire theory), 2 local publication/API cases. Scope is exact new named methods, no other Seam_ methods existed before these additions. Uses registered PostgreSQL services, materialization, guards and actual API host. The only substituted effects are deterministic failing observers/diagnostics.

Frozen Components selection before execution: SharedProviderLifetimeRegressionTests (3), SharedProviderOwnedEffectsTests (11), SharedProviderRefreshButtonTests (2), ProviderMutationRegressionTests (3) = 19. Public rendered controls drive sync/save/test/close; callbacks only record results. The refresh button fixture now emits typed scope and expects sanitized unknown failures.

First 11-case integration run: 7 passed, 4 failed. Corrected two API lambda awaits (nested Task result had yielded 500), put authoritative source-managed ownership rejection before editor revision comparison (import revision is composite), and corrected the source-update expectation: changing URL/credential intentionally resets trust to NeverSynchronized and makes the import nonmaterializable until a successful identity test/sync. This is preserved fail-closed behavior, not a fallback to a different provider. The fixture now verifies URL plus credential changes for two imports and explicit recovery. Source/local authority changes carry their semantic kind without falsely claiming a remote-owned field edit.

Component fixture correction: bUnit rendered-wrapper Dispose() did not invoke the component's IDisposable lifecycle; use its public Dispose on the renderer dispatcher, matching existing host tests. The old baseline disposal failure is therefore excluded from meaningful failing-first evidence (the two target A→B failures remain valid). Native textarea value is represented as the Blazor value attribute in bUnit; assert that public rendered value, while real-browser acceptance will inspect DOM .value. No production behavior was weakened for these fixture corrections.

## Final disposition
Status: complete for provider-owned obligations. The historical documentation gate blocker is explicit in bundle://reviews/closure.md.
Owned invariant contract: bundle://proof/SBB/semantic-invariants.md.
Portable commands and before/after failure output: bundle://proof/SBC/transcripts/.
Case receipts and all 31 topic bindings: bundle://proof/SBC/test-receipts.json and bundle://proof/SBC/topic-map.json.
Changed-source before/after hashes: bundle://proof/SBC/file-manifest.json.
Production assertions and final adversarial review: bundle://reviews/csharp-architecture-gate.md.
Anti-stub command/result: bundle://proof/SBC/transcripts/anti-stub.txt.
Downstream real API, shared workspace and browser proof: bundle://proof/SBC/browser/acceptance.md.
Final closure, exclusions, fixture corrections and unresolved repository-wide baseline debt: bundle://reviews/closure.md.
