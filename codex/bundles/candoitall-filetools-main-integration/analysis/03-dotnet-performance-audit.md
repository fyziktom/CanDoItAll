# .NET Performance Audit

## Scope And Method

This is a preparation-time audit, not authorization to change product code. The hot scope is the main Storage implementation, the FileTools filesystem/browser/interaction paths, and the Workbench Pages area containing the current Project Structure asset-preview entry path. The review follows the mandatory two-pass `optimizing-dotnet-performance` workflow and the standard `analyzing-dotnet-performance` scan. Exact counts are source-pattern hits in the scoped `.cs` files; they are not all defects.

Workload assumptions for design and later proof:

- a configured source may expose 100,000 or more direct children and substantially more recursive descendants;
- the user normally needs the first page or a narrow search result, not a complete in-memory catalog;
- a Project Structure double-click already identifies one authorized asset and must not initialize a browsing session;
- remote providers may be slow, partially capable, or unable to produce deterministic global ordering without an index/snapshot.

## Pass 1: Initial Performance Review

### P1-01 — Filesystem page one is currently O(total children)

`filetools://src/CanDoItAll.FileTools.Providers.FileSystem/FileSystemFileBrowserProvider.cs:224-249` enumerates every child, captures per-entry facts, creates a full consistency hash, globally orders and materializes the result, then applies `Skip/Take`. Existing pagination bounds the returned page, not provider work or memory. This is the primary scale blocker and must be removed before UI starts.

Planned correction: make ordering/paging capability explicit. Use provider-native cursor/order when it is stable; otherwise use a deliberately bounded indexed snapshot with entry/time/memory limits. Reject unsupported global ordering explicitly rather than silently scanning an unbounded directory. Page-one tests must count enumerated entries and allocations, not only returned rows.

### P1-02 — Progressive search is bounded but delays page one until its full budget is exhausted

`filetools://src/CanDoItAll.FileTools.FileBrowser.Core/Search/ProgressiveFileBrowserSearchStrategy.cs` correctly has item/container budgets, cancellation, bounded retained searches, and partial-result warnings. It nevertheless captures and sorts the complete bounded snapshot before returning page one. At large configured budgets this can create poor first-result latency.

Planned correction: retain the existing bounded, deterministic mode, but make the product pilot choose provider-native search when supported and adopt typed time/item/container/concurrency budgets. If progressive global ordering is required, its first-page latency and retained bytes must be measured and kept within the approved budget; a lower-latency streaming mode must not pretend to provide global ordering.

### P1-03 — Main IPFS read creates a client per call and buffers the whole response

`repo://src/Foundation/CanDoItAll.Infrastructure/Storage/Drivers/IpfsStorageDriver.cs:128-149` creates/disposes `HttpClient`, uses default completion behavior, reads the entire response into a byte array, then wraps it in `MemoryStream`. This risks connection churn and multiple full-content allocations.

Planned correction: inject a pooled/factory-managed client or narrow transport, use `ResponseHeadersRead`, return a lifetime-owned response stream, and apply explicit content/range limits. Never increase a content limit to hide a buffering design.

### P1-04 — Single known-file interaction is a distinct fast path

The current Project Structure flow resolves a node in `HandleNodeOpenedAsync`, calls `OpenAttachmentPreview`, and renders `ProjectStructureCanvasDialogs` directly. The replacement must preserve this double-click/open behavior while swapping the dialog body to FileInteraction. It must pass one authorized `FileReference` and `IFileContentSource` directly and must not create a FileBrowser session, source set, catalog, tree, search coordinator, or listing cache.

Planned correction: keep two typed intents: known-file interaction and collection browsing. Do not infer the distinction from strings, path shape, or item count.

### P1-05 — Content materialization is acceptable only behind the existing hard bound

FileInteraction's loader requests at most `maximumBytes + 1`, checks declared and observed length, uses a pooled 80 KiB read buffer, honors cancellation, and fails explicitly when too large. It then materializes content because render/edit contracts currently use `ReadOnlyMemory<byte>`. This is a bounded design, not a license to buffer arbitrary storage or browser results.

Planned correction: keep the host-configured interaction limit and oversize error. Large binary/video/remote scenarios require a separately designed streaming renderer/authorized endpoint, not a larger general-purpose in-memory limit.

## Pass 2: Deep Pattern Scan

### Scan execution checklist

| Recipe | Hits | Interpretation |
| --- | ---: | --- |
| `.IndexOf("literal")` without comparison | 0 | No scoped candidate. |
| `.Substring(` | 0 | No scoped candidate. |
| literal `StartsWith`/`EndsWith` without comparison | 0 | No scoped candidate. |
| literal `Contains` candidates | 0 | No scoped candidate. |
| parameterless `.ToLower()`/`.ToUpper()` | 0 | No scoped candidate. |
| three chained `.Replace` calls on one line | 0 | No scoped candidate. |
| `params` signatures | 3 | Not elevated without a measured hot call path. |
| LINQ `char.All`/`char.Any` | 0 | No scoped candidate. |
| static `Dictionary` / static `FrozenDictionary` | 0 / 0 | No inverse candidate. |
| `new List` / `new Dictionary` | 24 / 11 | Most are bounded state/snapshot construction; unbounded provider/search sites are covered by P1-01/P1-02. |
| `StringComparer.CurrentCulture` | 0 | No scoped candidate. |
| LINQ `Select/Where/Cast/Take/Aggregate` | 181 | Raw candidates; only directory/search/content hot paths are actionable without profiling. |
| `new HttpClient` | 1 | Actionable P1-03. |
| `new JsonSerializerOptions` | 1 | False positive: created once by static `StorageJson.SerializerOptions`. |
| `RegexOptions.Compiled` / `[GeneratedRegex]` / `new Regex` | 0 / 0 / 0 | No construction/startup-budget candidate. |
| static `Regex.Match`/`Regex.Replace` calls | 4 | Literal patterns occur in unrelated Project Structure process-context redaction/extraction, not the file browse/search/content hot path; no bundle expansion or speculative rewrite. |
| raw `async void` signatures | 2 | Both are event bridges; exceptions/lifetime still require tests, but the signature is appropriate to the event boundary. |
| `.Result` property hits | 2 | Both are domain result records, not `Task.Result`; no sync-over-async hit. |
| `.Wait(` | 0 | No scoped candidate. |
| unsealed / sealed class declarations | 23 / 110 | 110 of 133 scoped concrete declarations are sealed; do not seal the remainder without hierarchy/type review. |

### New findings after deduplication

None. The deep scan confirmed P1-01, P1-02, and P1-03 and added exact occurrence counts, inverse checks, and false-positive review. They are not repeated as new findings.

### Positive findings

- ✅ Async flow — no scoped `Task.Result`, `.Wait()`, or `Task.Run` wrapper was found.
- ✅ Bounded interaction reads — FileInteraction enforces declared and observed maximum bytes and returns pooled buffers.
- ✅ Search controls — progressive search already enforces item/container budgets, cancellation, partial warnings, and bounded continuation retention.
- ✅ String comparison — the scoped literal comparison recipes found no missing `StringComparison` candidate.
- ✅ Serializer options — `StorageJson` caches its options statically.

| Severity | Count | Top issue |
| --- | ---: | --- |
| 🔴 Critical | 1 | Filesystem paging performs work proportional to all children before returning one bounded page. |
| 🟡 Moderate | 2 | IPFS buffering/client construction and full-budget progressive search. |
| ℹ️ Info | 0 | Non-hot raw candidates are intentionally not promoted. |

> ⚠️ **Disclaimer:** These results are generated by an AI assistant and are non-deterministic. Findings may include false positives, miss real issues, or suggest changes that are incorrect for your specific context. Always verify recommendations with benchmarks and human review before applying changes to production code.
