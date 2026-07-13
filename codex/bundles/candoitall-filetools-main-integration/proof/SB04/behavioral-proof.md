# SB04 Behavioral Proof

Date: 2026-07-12. Closure decision: `Pass`.

## Shipped Behavior

- IPFS browsing uses explicit typed `cid:` and `mfs:` container identities. CID/DAG pages carry the immutable CID as consistency; mutable MFS pages use a server-reported hash checked before and after listing and bound into continuations.
- FTP browsing requests standardized `MLSD` machine facts. Only `file` and `dir` facts are mapped; current/parent directory facts are skipped, and malformed, duplicate, path-bearing, unknown, or server-unsupported facts produce explicit `UnsupportedOperation` rather than guessed classification.
- Both remote adapters expose only provider-native ordering. IPFS advertises immutable-version/consistent-continuation support; FTP deliberately does not advertise consistent continuation because the protocol listing has no source revision.
- IPFS HTTP uses `IHttpClientFactory` through `Microsoft.Extensions.Http` 10.0.0, aligned with the existing Foundation package set. Drivers reuse the injected transport/client; bearer headers are request-local.
- IPFS uses headers-first responses. IPFS and FTP content streams own their response lifetime and enforce a 256 MiB stream/upload limit without a `byte[]`/`MemoryStream` download bridge. Browse responses are limited to 2 MiB and request duration; FTP parses listing lines incrementally.
- Errors and logs expose storage ID, provider/address kind, bounded counters, request count, and failure type only. Endpoint, credential, cursor, raw response, and raw transport message are masked.

## Transport And Capability Matrix

| Provider | Browse identity | Reliable facts | Continuation/revision | Content lifetime | Unsupported behavior |
| --- | --- | --- | --- | --- | --- |
| IPFS CID/DAG | `cid:<content-id>` | Kubo `ls` name/hash/type/size | signed query-bound cursor; immutable CID consistency | factory-managed headers-first response stream owned by returned bounded stream | non-provider ordering, malformed/over-budget response |
| IPFS MFS | `mfs:/path`; root maps to configured MFS base | `files/ls` plus `files/stat` hash | hash before/after listing; change during or between pages is typed `SourceChanged` | same as CID | traversal-like MFS path, missing revision, stale cursor |
| FTP | normalized relative container | RFC machine-list `MLSD` `type`, optional `size`/`modify` | signed query-bound offset only; no false consistent-continuation claim | response-owned bounded stream; MLSD read is incremental | MLSD unavailable, ambiguous/malformed facts, global ordering |

## Behavioral Tests

Focused `FullyQualifiedName~SB04_`: Pass, 17 cases.

- CID/MFS typed address and consistency distinction.
- mutable revision change invalidates continuation.
- over-budget/inconsistent fake transport facts are rejected.
- cancellation publishes no completed diagnostic.
- reliable FTP facts map without write authority.
- ambiguous FTP classification is typed Unsupported.
- standardized FTP machine facts parse file/directory/current-directory correctly.
- four malformed/ambiguous FTP fact cases are rejected.
- existing remote content drivers return unread owned streams without bridge buffering.
- endpoint, credential, and raw transport error redaction.
- injected HTTP client reuse with three request-local bearer tokens.
- production MFS transport performs stat/list/stat and reports three requests.
- a 10,000-entry production CID JSON response returns page one after inspecting two entries and disposes the response before consuming the whole body.
- oversized IPFS content is rejected from headers before body read.

Realistic positive: an MFS production-transport test executes stat/list/stat JSON responses through the actual HTTP adapter, maps a container, and proves the same injected client handles all three requests. Meaningful negatives include mutable revision change, ambiguous FTP facts, transport counters outside budget, cancellation, oversized content, and secret-bearing raw failure.

## Build, Regression, Format, And Live Status

- Infrastructure Release build with warnings as errors: Pass, 0 warnings/errors.
- Unit tests filtered by `FullyQualifiedName~Storage&Category!=Scale`: Pass, 73 tests.
- Integration tests filtered by `FullyQualifiedName~Storage`: Pass, 10 tests in 1m7s.
- Focused Infrastructure and unit-test `dotnet format --verify-no-changes`: Pass.
- Optional live IPFS/FTP smoke: `Skipped`, because no endpoint/credential environment variables were present. Fake transport and production HTTP-handler proof are mandatory and passed; live setup is documented in `live-integration.md`.

## Source, Performance, And Dependency Audit

The changed remote source contains no per-call `new HttpClient`, `ReadAsByteArrayAsync`, `MemoryStream` bridge, blocking task result/wait, `Task.Run`, `async void`, default authorization headers, FileTools/Web dependency, partial class, placeholder, or empty catch. The only page-entry `ToArray` calls materialize already transport-bounded lists. IPFS listing uses an incremental nested-array stream and stops after offset plus page plus one lookahead; the 2 MiB byte ceiling remains a hard secondary bound. FTP listing is line-streamed and stops at inspection/page/byte/time bounds.

`Microsoft.Extensions.Http` is the only new package and exists elsewhere in this solution. It is required to satisfy handler pooling and client lifetime management; no external provider SDK or new project/reference was added.

Fresh Checkpoint A CodeAnalytics snapshot `snap-20260713031012-d26717a4`: one Infrastructure project, 69 documents, 131 scoped types, 827 members, zero scoped dependencies/cycles, and zero Storage warnings. An initial warning on the 363-line FTP transport was repaired by extracting the obsolete-request factory and machine-list parser. SB05 also replaced whole-envelope IPFS listing materialization with an incremental nested-array reader and factored duplicate cursor cryptography into one protector.

## Architecture And Progression

The one-implementation transport interfaces are justified external-protocol/test seams under PSR-02: production adapters are directly replaced by instrumented fakes without Web, credentials, or a server. Protocol construction/parsing remains inside Infrastructure. No FileTools, cache, authorization inference, provider fallback, or UI concern was introduced.

SB04 closes. SB05 may now review the complete Storage foundation. Later false FTP classification, MFS immutable treatment, raw-secret leakage, per-call client construction, unbounded response/content buffering, or false capability evidence reopens SB04 and affected downstream phases.
