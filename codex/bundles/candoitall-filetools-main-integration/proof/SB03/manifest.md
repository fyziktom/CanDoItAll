# SB03 Governed Proof Manifest

Date: 2026-07-12. Closure decision: `Pass`.

## Scope And Provenance

- Production scope is the native Infrastructure filesystem browse/stat adapter, signed cursor codec, entry mapper, shared confinement policy, existing filesystem content driver, placement consumer, and DI registration.
- Tests use real temporary filesystem roots, including a real symbolic link and a real 100,000-entry directory. An internal enumeration delegate exists only to count or cancel production iteration deterministically.
- Before hashes for critical modified owners: `FileSystemStorageDriver.cs` `2db713bdb251579f78ba24cacc1cb30b01587948d63b47e002f9d12c52937580`; `StoragePlacementService.cs` `ff83e0216af1bd154aebb22f16f591c829cab8df5c978b78b59e949783e45f00`; DI extensions `f14d0075b5363673a8ccefcd1dffba49eac6af44799ea5c523f532b60227ab36`; browse primitives `427affcf578125e36c915dc06423f57c25be117e4bea9ebf7b0cd79bca775d2f`.
- After hashes for every changed SB03 source/test owner are in `source-hashes.sha256`.
- SB05 later extracted the common HMAC/base64 protection into `StorageBrowseCursorProtector`; the hash list now reflects that accepted cleanup. The SB03 cursor and 100,000-entry second-page tests were rerun afterward and passed.

## Evidence Index

| Evidence | Purpose | Result |
| --- | --- | --- |
| `semantic-invariants.md` | Named security, freshness, scale, budget, cancellation, ordering, and redaction contract | Pass |
| `transcripts/failing-first-bounded-page-one.txt` | Failing-first shallow full-enumeration implementation | Correctly failed: 1,000 inspected/enumerated for 25 returned |
| `transcripts/passing-bounded-page-one.txt` | Same test after lazy bounded implementation | Pass: 25 returned, 26 inspected/enumerated |
| `transcripts/passing-security-freshness-cancellation.txt` | Traversal, reparse, stale cursor, live content/stat, cancellation, and ordering | Pass: 7 tests |
| `transcripts/scale-100000.txt` | Real 100,000-direct-entry structural scale proof | Pass: 51 first-page inspections, 101 second-page inspections, 238,000 worst allocated bytes |
| `transcripts/build-regression-format.txt` | Release build plus affected unit/integration regression | Pass: zero warnings/errors, 54 unit, 10 integration |
| `transcripts/focused-format-and-adversarial-rerun.txt` | Final format and added budget/redaction negatives | Pass: format unchanged, 9 tests |
| `transcripts/source-anti-stub-audit.txt` | Prohibited API, cache/effect, partial, dependency, and whitespace assertions | Pass |
| CodeAnalytics `snap-20260713022023-d26717a4` | Fresh scoped architecture/dependency/findings review | Pass: one project, no project references/cycles, no new large-file finding |

## Production Artifact Matrix

| Artifact | Production producer | Production consumer | Lifecycle/retention | Negative proof |
| --- | --- | --- | --- | --- |
| `StorageBrowseOperationMetrics` | `FileSystemStorageBrowseDriver.BrowseAsync` counts returned, inspected, requested metadata probes, signed-token bytes, and elapsed duration during the actual operation | `StorageBrowsePage` and structured completion log | Created per operation; no server-side page snapshot or cache is retained | Failing-first counter transcript detects full enumeration; cancellation test proves no completed artifact is published |
| `StorageBrowseCursor` | HMAC cursor codec binds storage, hashed container, offset, page size, sort, metadata, and directory version | Next `BrowseAsync` call decodes and validates it | Client-carried opaque token; server retains no listing; process-key rotation invalidates old tokens predictably | Mutation and request mismatch produce typed `SourceChanged`/`InvalidCursor` |
| Structured browse diagnostic | Driver logs provider kind, storage ID, bounded counts, duration, completeness, cancellation, or exception type | Host logging pipeline | One event per completed/cancelled/failed operation; no absolute root, entry path, endpoint, cursor, or raw exception text | Injected I/O exception contains a root and secret marker; captured error/logs contain neither |
| Live stat/content facts | Entry mapper reads requested current metadata; existing filesystem content driver opens current file through the same path policy | Outer adapter-ready native contract and current content stream caller | No browser/session/cache dependency | Replacement test observes new size and content without reusing browse state |

## Architecture And Performance Decisions

- Adapter plus one shared path-policy collaborator removes duplicate containment logic without creating a broad hierarchy. Cursor and mapping responsibilities are separate focused types; the driver is 329 lines, below the large-file threshold.
- No partial class, new project/package/reference, service locator, FileTools/UI/Web dependency, provider cache, or shell effect was introduced.
- Provider-native forward order is explicit. Unsupported global ordering fails rather than materializing/sorting all children.
- Hot-path scan found no literal string-search allocation pattern, parameterless casing, chained replacement, char LINQ, blocking task access, `Task.Run`, per-call `HttpClient`, or per-call JSON options. The page list is pre-sized and bounded; breadcrumb collection is bounded by path depth. Existing save-path filename sanitization is outside this browse hot path and was not changed without measurement.
- CodeAnalytics informational member-count findings are accepted because the methods belong to one provider operation and the cursor state is one serialized contract. No new warning or dependency cycle exists.

## Downstream And Progression

- The live replacement test is the required adapter-level mutation/read smoke: browse observes current stat and the existing content driver reads replacement bytes through shared confinement policy.
- Existing filesystem save/read/delete/placement characterization remains green in the 54 affected unit and 10 Storage integration tests.
- SB03 closes. SB05 may trust filesystem only after SB04 also closes; later traversal, stale-read, path-leak, cache, link-following, or O(total-children) page-one evidence reopens SB03 and downstream filesystem proof.
