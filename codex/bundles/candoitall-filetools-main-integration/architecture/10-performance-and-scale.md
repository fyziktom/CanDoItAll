# Performance And Scale Contract

## Design Principle

Returned page size is not a performance bound. Each provider and adapter must bound the work performed, metadata calls, bytes buffered, state retained, concurrency, and UI items rendered before a page or interaction is usable.

## Typed Intent Split

The integration must expose separate typed entry contracts; names may be repaired during execution, but semantics may not be merged:

- `KnownFileInteractionRequest`: one server-authorized `FileReference`, one `IFileContentSource`, requested `FileInteractionMode`, media/name hints, revision, and allowed host effects. It initializes FileInteraction directly.
- `BrowseFileCollectionRequest`: authorized source/scope, initial container, filters/search/sort, browsing mode, and budgets. It initializes FileBrowser and only later produces a known-file request after activation and reauthorization.

Do not use a boolean, magic string, route shape, extension, or `items.Count == 1` to choose the path. A known file is an interaction even when siblings exist. A collection request is browsing even when the first page happens to contain one item.

## Project Structure Preservation

`ProjectStructurePage.HandleNodeOpenedAsync` is the current open/double-click path. For a renderable asset node it opens the existing preview dialog state through `OpenAttachmentPreview`. Migration must preserve the user-visible behavior and overlay lifecycle:

1. double-click/open resolves the same node and closes the quick-action dialog;
2. current host policy re-resolves and authorizes the asset;
3. the existing focused dialog opens;
4. its body renders FileInteraction directly for the one known file;
5. no FileBrowser component/session/source set/tree/search/list/cache is constructed or invoked;
6. close/replacement/disposal cancel and dispose only the interaction lifetime.

Images and PDFs are mandatory characterization cases. Text/Markdown, media, Mermaid, edit/save, and unsupported formats migrate per SB16 only after their existing behavior is captured. Toolbar/context actions that semantically browse project/node collections remain FileBrowser actions and must not be conflated with double-click.

## Browse Bounds

The native Storage contract and FileTools adapter must carry typed, validated limits. Defaults are finalized from measurement in SB02/SB03; they may not be omitted or set to an unbounded sentinel.

- maximum returned items per page;
- maximum provider entries inspected per request before returning partial/unsupported;
- maximum metadata probes and maximum concurrent probes;
- maximum request duration plus cancellation;
- maximum progressive-search containers, items, duration, concurrency, matches, and retained snapshot bytes;
- maximum retained browser items/pages/continuations per session and host cache entry count/bytes;
- maximum FileInteraction content bytes, with explicit oversize behavior.

`Directory.GetFiles`, `GetDirectories`, `GetFileSystemEntries`, unbounded `Enumerate*().OrderBy(...).ToArray()`, or an equivalent complete materialization before page one is forbidden for unbounded sources. Per-entry `FileInfo`/`stat` N+1 work is forbidden unless the metadata field was requested and the probes remain within the declared budget.

## Ordering And Continuations

Ordering capability is truthful and typed:

- provider-native stable cursor/order may page directly;
- an indexed snapshot may provide deterministic global ordering only when its build/refresh, maximum entries, duration, memory, consistency, and eviction are explicit;
- a provider unable to satisfy requested global ordering within budget returns typed `Unsupported`/`BudgetExceeded`/partial completeness as defined by the contract.

Never hide an offset cursor over a full unbounded in-memory sort. Continuations bind source, container, filter, sort, authorization scope, and consistency/version. Stale or mismatched continuation use fails predictably.

## Search

Provider-native bounded search is preferred. Progressive search remains an explicit breadth-first fallback only for providers advertising it. It must honor cancellation and all declared budgets, return partial status with useful counts, cap retained snapshots, and expose the tradeoff between deterministic global ordering and first-result latency. There is no silent recursive fallback.

## Content And Network I/O

- Stay async end-to-end; no `.Result`, `.Wait()`, `Task.Run` wrapping synchronous I/O, or multiple awaits of the same `ValueTask`.
- Reuse HTTP connections through `IHttpClientFactory` or an equivalent injected pooled transport. Use `HttpCompletionOption.ResponseHeadersRead` for streamed content.
- Return lifetime-owned streams/leases. Do not convert remote content to `byte[]` and then `MemoryStream` before the bounded interaction layer.
- Honor range/length requests where supported and reject false range capability.
- Keep FileInteraction's hard observed-byte limit and oversize state. A larger/streaming renderer is a separate design, not a limit increase.
- Cache serializer options/source-generated metadata where serialization is hot, but require measurement before broad source-generation work.

## Blazor Runtime Bounds

Only the current bounded page and deliberate accumulated pages may enter component state. Keys must be stable. Search keystrokes are debounced/cancelled with latest-request-wins behavior. Source/location replacement cancels previous work and releases leases/object URLs. Browser UI must not render hidden thousands of rows; if accumulated results can exceed the measured desktop threshold, use an existing component-library virtualization wrapper or keep accumulation capped.

## Diagnostics

Each browse/search/content operation emits structured duration and count data at the owning boundary: provider kind/source alias, operation, returned items, inspected items, metadata probes, bytes read/buffered, cache outcome, partial/completeness, cancellation, and typed failure. Logs mask credentials, absolute configured roots, handles, tokens, and sensitive file names/paths. Metrics must not create high-cardinality labels from raw locators.

## Performance Proof Gates

SB03 creates deterministic generated directory fixtures at small and large cardinalities, including at least 100,000 direct entries where the execution environment supports it. The test harness records first-page elapsed time, provider entries inspected, metadata calls, managed allocation delta, retained continuation state, cancellation latency, and second-page correctness. Pass is based primarily on structural counters:

- page-one inspected entries and metadata probes stay within declared request/provider budgets rather than total directory cardinality;
- returned and rendered items do not exceed page/session limits;
- cancellation terminates within the declared timeout and does not publish success or retain orphan state;
- no full response/file buffering occurs outside a declared content bound;
- repeated runs establish a median and worst-case envelope; absolute latency thresholds are environment-calibrated in SB01 and stored with machine/runtime facts.

SB04 applies equivalent bounded transport counters to IPFS/FTP fakes. SB10 measures real pilot search/open, and verifies one direct interaction content read after activation. SB13/SB16 prove Project Structure direct asset open makes zero FileBrowser browse/search/session calls. SB18 reruns representative scale tests and compares them with the accepted baseline; a material regression reopens the owning foundation.

Performance changes require measurement and human review. Do not replace clear bounded code with pools, spans, frozen collections, custom loops, or source generation unless the affected path is hot and the before/after proof shows material benefit.
