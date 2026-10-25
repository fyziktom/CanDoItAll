# P02c browser pending store — source review and proof plan

Status: source frozen on 2026-10-16; all 13 store cases have qualifying browser
passes across the retained runs below. Typed adapter/native integration and subset
proof are recorded in [the adapter record](P02c-native-recovery-adapter.md) and
[the native editor record](P02c-native-editor.md). This document does not turn
their unit/component passes into browser durability proof. The parent owns
serialized validation, portability enforcement, resource records and commit.

## Owned changes

- `src/App/CanDoItAll.Web/wwwroot/js/projectPendingSubmissions.js`: a standalone
  Projects IndexedDB module; existing `browserState.js` is unchanged.
- `tests/Playwright/CanDoItAll.Tests.Playwright/ProjectPendingSubmissionBrowserTests.cs`:
  thirteen browser facts, reconciled against the retained execution records below.
- `plan/P02c-native-pending-recovery.md`: integration outline and store invariants.

The compound key is namespace UUID, caller binding, canonical positive Int64 epoch
**string**, and operation UUID. Frozen request and accepted receipt JSON are opaque
strings. JavaScript never parses or reserializes the epoch number inside owner
JSON. The implemented typed C# adapter verifies the opaque payload matches its key,
request version and command kind before calling this module; browser storage is
untrusted and is not an authorization boundary.

All writes use a strict-durability transaction over submissions and one bounded
usage-metadata row. Success means transaction completion. Individual request
success does not authorize dispatch. Disposal fences late completion: a write
may have committed, but its retired caller receives failure and cannot send.
The host must inspect the retained record explicitly after that ambiguity.

The metadata row allocates monotonically increasing safe-integer record revisions
across the store. Deletion preserves that high-water mark, preventing an old CAS
clear/cancel from matching a recreated operation. Revision exhaustion fails
explicitly. The existing per-origin transaction serialization covers only these
short local updates; no transaction spans server calls or browser callbacks.

Prepared is exact compare-or-create. Dispatch requires a compare-revision claim.
Accepted bytes are monotonic; an older attempt may supply the accepted owner
receipt, but stale nonaccepted observations cannot overwrite it. A later rejection
cannot resolve a previous unknown dispatch. Only current first-attempt rejection
or authoritative closed absence can do so. Cancellation removes only Prepared;
clearing removes only a terminal record at its exact revision.

The independent contract/API reviewer found no blocking race in a read-only
inspection. They confirmed two adapter obligations: only an `applied`
BeginDispatch result authorizes sending, and opaque accepted bytes require typed
operation/kind/version/submitted-index validation before entering this store.
`unchanged`, `changed` and `missing` never authorize dispatch. This review is not
runtime proof.

Configured record, logical payload-byte and page limits are mandatory inputs;
this module invents no production values. Logical byte accounting is not a claim
about physical IndexedDB overhead or browser quota. Quota, unsupported durability,
schema/corruption and transaction errors propagate with no eviction, localStorage
fallback or memory-only dispatch. Recovery listing uses the compound-key prefix
and at most one lookahead cursor row; it returns bounded metadata, not all bodies.

The adapter supplies stable bytes from the owner's existing receipt codec. Transport-only values such
as `Replayed` are not part of the persisted accepted receipt: a later lookup of
the same owner receipt must compare identically. Current definition reads remain
separate from that historical accepted result.

## Browser source cases

Filter: `FullyQualifiedName~ProjectPendingSubmissionBrowserTests`.
Expected discovery: **13 cases**, all source facts. The execution records below
account for every case without treating the original failing run as green.

1. Two same-origin tabs preserve exact preparation/acceptance, reject a changed
   payload and contradictory accepted bytes, ignore stale observations, and refuse
   stale clears even after deleting and recreating the same operation address.
2. An unknown first attempt followed by a rejected retry remains unknown; a closed
   absence can resolve it, while a sole first-attempt rejection is terminal.
3. Competing prepared cancellation and dispatch have exactly one winner.
4. Scoped cursor pages exclude another caller's bodies, enforce count/page limits
   and never evict an existing record to admit another.
5. Closing and reopening a real persistent Chromium profile retains the exact
   frozen request and historical accepted payload.
6. Aborting the transaction after a successful put reports no commit, permits no
   effect, and rolls back both the record and capacity accounting.
7. Disposing the module after put success but before transaction completion does
   not authorize an effect; reopening exposes the retained prepared record.
8. Chromium's actual per-origin quota override blocks first dispatch and leaves
   Dispatching recoverable when persistence of an accepted receipt fails.
9. A browser reporting relaxed durability rejects initialization; no localStorage
   or memory fallback appears, and normal initialization can subsequently succeed.
10. Global reference inventory exposes metadata only across bindings; exact
    disposal releases bounded capacity while preserving revision fencing after
    address recreation.
11. Two same-origin tabs race acceptance against explicit evidence disposal;
    exactly one revision decision wins, with no accepted body in admin results.
12. Aborted disposal preserves records/accounting; a sound header with an
    unsupported body can be discarded without decoding that body.
13. Final LF/CRLF suffixes in every operation-key field fail before any storage
    transaction, leave an empty inventory and do not prevent a valid maximum
    Int64 epoch key. This is added regression proof; the existing JavaScript
    expressions already enforce absolute end without the multiline flag, so no
    production regex change was made.

Tests serve the exact production module through Playwright routing at an isolated
secure test origin. They require Chromium but no application host, SQL lease,
Docker stack, port or real network request. Viewport is 1920x1080. The persistent
profile is allocated under the existing `TestFileSystem` helper and deleted after
its contexts close, including setup failures. Browser/context disposal uses the
existing fixture cleanup helper. No runtime resources were created while writing
these sources.

Completed typed codec/key and session checks are recorded separately. Remaining
runtime proof includes current caller/dataset authorization before body display, same-target blocking,
no automatic restore dispatch, accepted-before-acknowledgement orchestration,
sticky uncertainty across owner lookup variants, and honest distinction between
frozen submitted data and later live typing. Thirteen browser-store facts do not
substitute for those native/owner/API tests or final portability enforcement.

## Browser fixture repair, 2026-10-16

The parent's `p02bc-browser-repair.trx` contains 14 executed cases: 11 passed,
three failed, none skipped. Native recovery passed. The three remaining failures
were two continuation-key reads and the quota fixture; this source repair changes
only those existing store cases and does not change the production JavaScript.

Playwright .NET 1.55 converts an evaluation result to `JsonElement` through
`ReferenceHandler.Preserve`. The store correctly returns the last item's key as
its continuation, sharing that object reference. The test consequently observed
`$ref` metadata instead of the key's fields. The two affected first-page responses
now cross the test bridge as browser-produced JSON text and are parsed locally.
They still require the exact continuation key, bounded pages and existing
capacity/disclosure assertions. See the pinned
[Playwright result converter](https://github.com/microsoft/playwright-dotnet/blob/v1.55.0/src/Playwright/Transport/Converters/EvaluateArgumentValueConverter.cs#L189).

The previous quota fixture initialized IndexedDB before reducing its quota.
Chromium can approve writes from cached bucket space without querying the new
quota. The pinned browser's
[bucket quota implementation](https://github.com/chromium/chromium/blob/140.0.7339.16/content/browser/indexed_db/instance/bucket_context.cc#L327)
therefore explains why an immediate one-byte override did not reject the next
write. The repaired fixture sets a finite 256 KiB quota before initialization,
asserts CDP's actual quota and `overrideActive` response, and retains that quota
through both cases. A 1 MiB base64 payload generated from random bytes avoids the
repeated-character compression assumption and exceeds the initial cached grant.
Both oversized writes must raise the browser's actual `QuotaExceededError`; no
storage exception is injected. Failed preparation must leave no record or logical
usage. A small preparation and dispatch must succeed under the same quota, and
failed acceptance persistence must preserve the key, revision, frozen request,
attempt, null accepted payload and logical accounting. The override is removed
only in cleanup. The CDP fields are defined by
[Storage.getUsageAndQuota](https://chromedevtools.github.io/devtools-protocol/tot/Storage/#method-getUsageAndQuota).

The affected filter contains **three** existing facts. The complete store class
remains **13** facts; both native browser cases make **15** combined.

The parent rebuilt and discovered the exact final four-case selection, then
executed it successfully: **4 passed, 0 failed, 0 skipped**, approximately
**30 seconds**, retained in `p02bc-browser-final-four.trx`. It contains the three
repaired store cases and the refreshed native recovery case:

- `Scoped_paging_and_capacity_reject_without_evicting_other_records`
- `Foreign_reference_inventory_disposal_releases_capacity_without_returning_payloads_or_losing_revision_fencing`
- `Real_browser_quota_failure_blocks_first_dispatch_and_preserves_pending_acceptance_recovery`
- `ProjectsPendingRecoveryBrowserTests.Native_editor_reopens_the_same_durable_operation_after_browser_acceptance_persistence_fails`

The earlier 14-case run remains **11 passed / 3 failed / 0 skipped**. Its ten
passing store cases plus these three repaired store passes qualify all **13**
store cases; this is reconciled evidence across runs, not a claim of one green
13-case run. Native proof and final portability/resource closure remain in their
own parent-maintained records. This update changed documentation only.
