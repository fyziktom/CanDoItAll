# C# Current-State Inventory

Evidence: [CodeAnalytics](../analysis/codeanalytics-summary.json), [provider review](../analysis/provider-review.md), [performance review](../analysis/performance-review.md). Exact .csproj files for History.Persistence, ProviderManagement and SharedProviders.Http were inspected, along with their registration roots.

| Owner | Responsibility / constructor dependencies | Risk and test seam |
| --- | --- | --- |
| SharedProviderHttpRelayClient / SharedProviderRelayResponsePolicy | HTTP dispatch, buffering, response validation/rewrite; response policy shares its file | Test buffered transport independently; do not add more connector switches. |
| SharedProviderSseRelayStream | Framing, limits, terminal outcome; transport resources owned until dispose | Feed fake streams; prove public SDK failure through Web. |
| SharedProviderRuntimeHttpClientSelector | Runtime destination/client selection | Align with SourceUriPolicy; no service locator in protocol/core. |
| HistoryInvocationRecorder | Begin/end durable capture; six injected dependencies | Typed recorder seam; no inference replay after terminal-write failure. |
| HistoryTextCapture / HistoryTextProtector | Pure redaction/bounds / encryption and known-secret snapshot | Static direct unit tests plus persistence tests; no arbitrary regex growth. |
| ProviderHistoryObservation | Typed result/outcome/pricing observation; five constructor inputs | Fake driver/client + recorder; isolate timeout/cancellation without full runtime. |
| HistoryRetentionStore | Detail/metadata cleanup; factory and clock | PostgreSQL integration with retained/released retry input references. |
| SharedProviderCatalogQueryService | Catalog and routing projection; five constructor dependencies | Cross-process persisted-stamp test; avoid materializing all payloads on cache hit. |
| SharedProviderRelayRequestPolicy | 1,361-line request/subset/role/media/tool validation class | Repeated constant-set allocations; static caching first, not file-count refactor. |
| Web OpenAPI contract classes | Transport documentation metadata | Explicit type schema and positive/negative payload conformance tests. |

Direct construction: MafProviderAgentFactory creates ProviderHistoryChatClient inside EmptyCompletionRetryChatClient; HistoryProviderDriverFactory constructs eight typed decorators; ProviderManagementServiceCollectionExtensions owns registrations; SharedProviderHttpServiceCollectionExtensions owns HTTP adapters.

No new runtime partial is proposed. Generated migration partials and cohesive Razor code-behind remain allowed. File-size warnings identify inspection targets, not automatic extraction mandates.

Missing regression cases: realistic error:null envelope; oversized 429/504 body; external SDK stream failure; default loopback graph; quoted credential keys; explicit timeout vs caller cancellation; orphan shared input cleanup; schema semantics; final revision export parity. Existing classes/selectors are in the validation plan.
