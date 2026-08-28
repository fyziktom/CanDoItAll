# C# Testability Plan

## Contract Before Implementation

SB01 names typed contracts, identity invariants and test seams before adapters or UI are
added. Existing test homes are extended where they own the changed behavior. New history
tests are justified only for new neutral query/lifecycle/store behavior with no current
home. Do not create tests that merely restate DI configuration or assert a class exists.

[Validation strategy](../plan/02-validation-strategy.md) specifies future commands,
source-verified existing cases, proposed cases, discovery and invalidation. None has been
executed as feature proof during preparation.

## Responsibility Matrix

| Behavior | Smallest useful fixture / seam | Positive proof | Adversarial proof |
|---|---|---|---|
| Typed identity and versions | Pure records + state transition fixture | Update/replay retains EntryId and SortAtUtc; valid attempt per actual dispatch. | Equal prompts/correlation do not merge calls; version is not uniqueness; conflicting owner facts reject. |
| Price provenance | Existing ProviderPricingTests + relay finalizer fixtures | Buffered/streaming configured tariffs, cache categories, long context per attempt, ProviderReported zero/currency. | Missing usage/tariff/unsupported unit/overflow remain explicit; no legacy reprice or reasoning double charge. |
| Durable capture | Fake provider, recorder write port and TimeProvider | Exactly one provider send after durable start; terminal observation persists. | Start-write failure sends zero; terminal-write retry sends no second call; cancellation does not erase richer usage. |
| Actual runtime wiring | Existing production factory/backend/MAF tests | Decorator ordering inside retry and typed stream terminal events. | Generic TResult=true does not count as usage; MAF bypass/duplicate decorator fails coverage. |
| Canonical linkage | Existing chat/workflow persistence fixtures + exact owner adapter | One attempt with several owner links, canonical bodies stay at source. | Early/late commit, stale version, mismatched provider/model and body-copy fallback fail. |
| EF intent atomicity | Disposable PostgreSQL, actual owner DbContext | Source and outbox commit together; projector replay converges. | Fault between writes/commit rolls both back; second independent context is not accepted as proof. |
| File journal | Temporary owner workspace + injected faults at each handoff | Prepared intent/source commit/ack recovers exact IDs. | Crash after first canonical commit beyond orphan expiry, later source mutation, concurrent updates and delete-before-ack do not lose/change history or resurrect data. |
| Query bound | Scalar reader spy + generated SQL/PostgreSQL EXPLAIN | Where/order/Take server-side; bounded page, stable ties and coverage. | No file scan, body/config columns, per-row owner/token reads, automatic count or forbidden partition. |
| Authorization | Fake principal/resource policy + Web host tests | Separate metadata/content/manage grants; verified caller jti. | Invoke-only, forged access-context, owner denial, missing local authority and permission/profile change before publish deny. |
| Retention/detail | TimeProvider + transactional quota + small text/media fixtures | Canonical-owner lifetime, 30-day direct/relay default, 7-day detail, bounded shared retry input. | Expired content unreadable before GC; quota races, invalid settings, stale replay and missing keys never expose content. |
| Recovery/transfer | Existing profile lease/test host + disposable DB/files | Same stable partition after restart/switch back; owned data/intents transfer. | Old finalizer/worker cannot write new DB; interrupted usage stays unknown; cleanup does not erase active attempts. |
| UI orchestration | Existing component fixtures with service spies | Both scopes share state; explicit Search, correct applied filter/cursor. | Mount/tab/typing/Enter causes no unwanted query/save; stale completion/denial clears restricted state. |
| Desktop interaction | Deterministic two-host fake-upstream fixture at 1920x1080 | Normal and open detail/policy overlays; visible load/error/coverage and keyboard flow. | Clipped overlays, wrong scroll owner, leaked body/key in DOM/network/console, eager requests and Save-on-Search fail. |

## Production-Path Requirement

A test-only capture implementation or manually constructed query service is insufficient
for integration closure. Prove the production host registers new mappings and adapters,
that actual provider factories use them, and that actual canonical save/delete paths
publish intents. Include the real migration model in disposable PostgreSQL tests.

Use deterministic fake upstreams for buffered chat, SSE terminal usage, cancellation,
image/speech metadata and retries. No paid provider request is needed to prove this design.
Any later real upstream smoke test needs explicit user authorization and budget.

## Observable Non-Goals

SDK-internal HTTP transmissions are not claimed as separate attempts without a trustworthy
observer. Legacy aggregate evidence has nullable AttemptId/StartedAtUtc and explicit
TimeBasis. Arbitrary relay transcripts can have UnsupportedDetailShape. Neither missing
historic credentials nor unknown tariffs may be fabricated for a cleaner screen.

## Proof Tiers And Invalidation

- SB01, SB02 and SB07: Behavioral. Focused discovery/tests plus boundary/source and
  component/browser proof as appropriate.
- SB03–SB06 and SB08: Governed. Capture exact commands, discovered cases, exit codes,
  artifacts/hashes and positive/negative production-path evidence, not only green summaries.
- SB09: Standard closure audit of already valid evidence. It does not rerun every test.
- A public DTO/project edge, EF schema/model, DI factory, source ownership, profile fence
  or query/security contract change invalidates its owner phase and named dependents.
  A docs-only correction does not invalidate unrelated product suites.
- One affected-project broad checkpoint at frozen SB08 is justified by actual public
  contract/schema/DI changes. Reuse evidence unless its named invalidation key changes;
  never run every repository suite after every small edit.

## Architecture Review Checklist

- Old behavior removed when extracted, no runtime partial split as fake separation.
- No EF/ASP.NET/SDK/outer-feature types in neutral signatures.
- No hidden captured service bags, all-sources content traversal or default allow policy.
- Real same-context transaction or explicit file recovery protocol, never presumed atomicity.
- New classes measured with 250/400-line thresholds and constructor responsibility review.
- Time, cancellation, query size and storage quotas controlled in tests; no wall-clock
  sleeps or large live datasets required for correctness.
- Existing model, provider secret, retry, approval, tool and transcript ownership unchanged.
