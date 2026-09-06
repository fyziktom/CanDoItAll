# Providers-02D execution

Source: components-decoupling, local and remote e5a8d5c6b7ad19c99c805a76cde84b99d08d9eee rediscovered at entry. Implementation is an uncommitted working-tree diff. No merge, history rewrite or cleanup. Components c3e6aa03a878994c0ba8aed6af017d0be75f3796 and FileTools 7c7453c6583365ae5bd63f8fc6efc4a776e15818 remain unchanged.

## Adjudication and changes

Finding 1 accepted; findings 2–9 and 11–12 confirmed. Finding 10 confirmed with the qualification that list rereads prove only identified canonical postconditions. The pre-edit adjudication records each separately.

Local receipts identify operation, candidate/target, immutable fingerprint, expected revision and intended revision where available. New submissions propose one ID before awaiting while the draft remains New. Create-only expected-absence concurrency prevents overwriting an existing candidate. Scoped recovery retains the original submission and identity.

Verification reads canonical persistence by exact ID without Save/Delete. A found create or absent delete resolves authoritative state; matching intended/expected revisions classify updates, while intervening revisions and unavailable reads remain unknown. Absence permits only controlled same-identity/submission retry. A racing create conflict returns to verification. Known identity binds before existing reconciliation; later edits, EditContext and current section survive.

Shared management retains an unresolved descriptor across target recreation. Successful authoritative Retry replaces tokens/action state and obsolete warnings; failed, canceled, stale or wrong-target reads do not unlock. Source recovery uses proposed ID or existing source plus before-revision/intended postcondition. Serialized same-ID creation returns matching canonical state and rejects mismatch. Overlay recreation preserves recovery. Typed callback delivery is at most once per attempt; the refresh button respects the same source lock.

HTTP unknown writes return 409, code agents.provider-write-unconfirmed, sanitized receipt/ProviderId, AutomaticReplaySafe=false and verification path. CDA-Provider-Outcome: unconfirmed-verification-required; no-store, no Retry-After. POST /api/agents/providers/mutations/verify returns a typed classification serialized as a stable string. Known commit success and committed-reconciliation-pending remain intact. Deletion remediation distinguishes permanent publication, import audit identity, both, and source references. Unpublish does not enable deletion.

## Actual validation

Direct Release builds passed for ProviderManagement, AgentFramework module and Web; Unit/Components/Integration assemblies were freshly built. Exact owning selections: 426 Unit, 96 Components, 275 Integration cases passed, zero skipped. Final touched-source follow-ups: 69 local state/reconciliation cases and 25 recovery/API cases passed; final Components is the 96-case rerun. Counts overlap. The repository realistic-key test passed its one discovered case.

Initial failing-first: two Unit failures for missing candidate/selection replay and one Component failure for locked Sharing Retry. Additional real regressions were captured before correction: imported alias stale after token refresh; create conflict losing candidate protection; refresh button bypassing unresolved source; browser/Unit Runtime reset during Verify. Browser Add/Edit source title was corrected using authoritative revision, with public component assertion. Initial owning Components failures (fixture registration and stale tab event) were repaired without weakening behavior; final 96 passed.

Complete proposed-source portability passed: 14,251 reviewed executable findings unchanged, no baseline write. Scanner self-tests passed 6 + 4. Full source secret comparison against committed HEAD found no new matches; historical fixture matches are not new credentials. Original outputs and TRX are retained.

No broad stable solution rerun was warranted: provider receipt/verification composition changed, not shared schema/root build graph/unrelated modules. Exact provider API/database/ownership consumers cover the invalidation. Historical broad proof is not relabeled.

## Browser and limits

Actual Web Program/Kestrel, isolated PostgreSQL, production adapters, 1600×1000. Task-only ignored fixture interceptors fail EF save/transaction responses; no production failure toggle. Unknown first Save produced one canonical row. Verify bound its ID without another Save and preserved later name, raw suggested text, notes and Runtime. Normal subsequent Save updated that same row.

Unknown post-commit Publish recovered to Published/public identity and enabled Unpublish. Unknown source create survived inner/outer overlay close and reopen, verified the same sole row, cleared warning and unlocked actions. Normal Discover/import still materialized a read-only imported provider. Fresh final runtime also verified Add title for a proposed source ID.

Six normal/warning/overlay/import screenshots were inspected: readable existing desktop split layout and bounded scroll owners, no blocking overlap or clipped primary actions. Final console errors began at explicit fixture shutdown 23:17:41 UTC; both owned fixtures stopped gracefully. Manager readiness did not recognize the test wrapper although actual HTTP/browser worked; this is behavioral evidence, not watch timing.

Scoped recovery survives component/overlay recreation within the circuit, not server restart/new circuit. API callers retain receipts independently. No durable journal/outbox. Intervening revisions remain unknown; source create compares immutable original request to current canonical configuration, conservatively retaining unknown/conflict on mismatch.

Whole-branch documentation gate still reports 118 pre-existing tracked logs. No new log is proposed; historical proof/cleanup remains untouched. This does not invalidate bounded provider gates but repository documentation is not green. Catalog/Sandbox/watch performance are unimplemented; next is SB00 only after artifact closure.
