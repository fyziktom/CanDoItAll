# Current State And Gaps

## Evidence Boundary

Analysis used repository commit `dec33cb5614b78266a47dfac214401d5c2bb913d`, source
inspection, existing tests, a scoped CodeAnalytics snapshot, and a literal project-reference
graph. It did not execute application tests or inspect database rows. The user-reported
[Agents surface](http://localhost:5210/agents) could not be opened: the browser runtime
failed sandbox ACL initialization before page access. The running deployment is therefore
unverified. This does not prevent source-based bundle preparation; it remains an SB08 gate.

The user's named architecture governor and both .NET performance skills were applied,
together with dependency audit, testability, architecture review, provider isolation,
bundle preparation/validation and shared standards. Reports preserve positive findings
and limits, not only potential defects.

## Existing Capabilities And Missing Connections

| Concern | Existing implementation | Missing work and decision |
|---|---|---|
| Shared pricing | Publication/import mappings retain model price metadata. Relay begin/finalize records usage and outcome. | Finalizer explicitly persists null price/unavailable pricing; its usage projection shows Unpriced. Snapshot execution-time tariffs, preserve provider-reported amounts and calculate supported observed units. Do not build another catalog. |
| Request identity | Relay has unique RequestId, trace/correlation, subject and access-context reference. | Managed-token registry ID is validated in Web but not passed to the relay audit. Capture trusted issuer/subject/credential ID separately; never store bearer material. |
| Existing histories | Simple-chat invocation rows, agent file evidence and workflow observations contain provider/model/usage and owner data. | They are aggregate/owner stores, not one bounded request search. Add scalar metadata projections and typed owner mappings; retain original bodies. |
| Duplicate use | Workflows may preserve an agent provider observation ID; retries can aggregate into one chat invocation. | Same observation can have multiple owners, while retries are separate attempts. Preserve that distinction; provider/model/time equality is not deduplication. |
| Relay audit | Canonical request state, concurrency checks, streaming finalization and recovery already exist. | Project the existing row; do not add a second standalone request. Price evidence and verified credential ID need extending. |
| Usage dashboard | Selected sources feed ProviderUsageQueryService and aggregate in memory. | Its API has no date/page query. It cannot back the new History tabs. |
| Retention | Relay DeleteAfterUtc and an expiry index exist; current 30-day value is hardcoded. | No purge consumer found. Add a policy and leased bounded cleanup with canonical-owner rules and deletion replay protection. |
| UI | Provider editor has Sharing/Prices; Agents has usage and chat surfaces; Manager Summary has explicit lazy queries. | Add one reusable query panel in two scopes. Fix the provider form boundary so Search/Enter cannot Save provider edits. |
| Authorization | Managed API-token validation and interactive principal/policies already exist. | Catalog/invoke grants must not imply metadata/content/manage. Recheck resource permission and profile generation before publishing asynchronous results. |
| Capture | MAF SDK, provider-backed buffered/streaming chat, batch, relay, image, speech and operations have different typed paths. | A generic runtime wrapper alone misses actual execution/terminal use. Instrument each observed path without changing retry or tool policies. |

## Confirmed Source-Level Pricing Defect

[SharedProviderInvocationAuditFinalizer](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderAuditedRelayStream.cs:64)
writes `Price: null` and unavailable pricing. Both buffered and streaming relay use it.
[SharedProviderRelayUsageProjectionSource](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework/Providers/SharedProviderRelayUsageProjectionSource.cs:221)
maps that evidence to Unpriced. This confirms a code path matching the symptom, not the
specific deployed row. Historical missing prices cannot be repaired from current tariffs.

See [sharing and pricing](../architecture/06-sharing-pricing-analysis.md) for dispatch,
claims, protocol, catalog, constructor and exact test evidence.

## Performance Findings

[ProviderUsageQueryService](C:/repositories/CanDoItAll/src/MAF/Common/CanDoItAll.AgentFramework.Usage/ProviderUsageQueryService.cs)
reads complete selected contribution sets and groups them in memory. Its sources include
unbounded database projections and file enumeration under workspace locking. Reusing it
would make a narrow History query proportional to retained source history.

The two-pass review covered 14 explicit hot-path files / 6,362 lines. It found no
sync-over-async, async void, Task.Run wrapping, per-call HttpClient, or case-conversion
search chains in that scope. All 21 scoped concrete declarations are sealed; two cached
serializer options are reused. Those are source observations, not measured speed claims.
The main risk is data volume and ownership, not syntax-level micro-optimization.

See [history and performance](../architecture/07-history-performance-analysis.md) for
positive findings, source scope, DB/file paths, future query-plan and scale fixtures.

## Dependency Evidence And Limits

CodeAnalytics snapshot `snap-20260828134930-4eb1620a` covers 10 projects, 308 documents,
939 types and 5,737 members. It reports 34 Warning and 196 Info findings. Counts include
generated sources. Factory DI interpretation is partial (17 diagnostics), external EF
configuration is broad (one), and some diagrams are truncated (nine). None is a proof of
runtime composition or a mandate to refactor unrelated code.

A separate literal XML scan of all 104 source projects found 534 declared references,
no missing referenced paths and no project cycles. The scoped analyzer's one cycle is
between two Infrastructure modules inside the same project, not a new project-reference
cycle. Imports/conditional MSBuild evaluation and SDK runtime behavior still require
implementation checks.

- [CodeAnalytics inventory](../inventories/02-codeanalytics-summary.json)
- [Project-reference inventory](../inventories/03-project-reference-inventory.json)
- [Responsibility and growth inventory](../architecture/00-csharp-current-state-inventory.md)
- [UI analysis](../architecture/08-ui-search-analysis.md)

## Reuse Versus Replacement

Keep provider registry/catalogs, shared wire protocol, relay state machine, token registry,
canonical histories, existing usage totals and BaseLib components. Add neutral history
contracts, bounded query/policy logic, one persistence feature, typed boundary adapters and
two UI hosts. Existing canonical stores gain metadata-only replay hooks where necessary.
No wholesale runtime, persistence, provider editor or dashboard replacement is authorized.
