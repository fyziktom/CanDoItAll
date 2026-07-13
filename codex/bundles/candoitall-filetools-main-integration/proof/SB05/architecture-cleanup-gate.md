# SB05 Storage Foundation Architecture Cleanup Gate

Date: 2026-07-12. Result: `Pass`.

## Review Findings And Repairs

| Severity | Finding | Repair | Verification |
| --- | --- | --- | --- |
| Blocker | IPFS listing used a cumulative byte ceiling but parsed the entire JSON envelope before returning page one; work was still O(total response) below 2 MiB | Added `NestedJsonArrayReadStream`; production mapping now incrementally exposes only the target `Links`/`Entries` array and stops after offset + page + one lookahead | Real 10,000-entry HTTP response returns one item, reports two inspected, has continuation, and disposes before consuming the body |
| High | Filesystem and remote cursor codecs duplicated HMAC, base64url, JSON, and fixed-time signature verification | Extracted one internal `StorageBrowseCursorProtector`; provider codecs retain only typed state and fingerprints | SB03 stale cursor, SB04 mutable revision, 26 combined provider cases, and 100,000-entry second page pass |
| High, repaired during SB04 | FTP transport combined obsolete request isolation, URI construction, directory creation, MLSD parsing, content streaming, and browse orchestration in 363 lines | Extracted `FtpWebRequestFactory` and `FtpMachineListEntryParser` | Final files are 103, 84, and 208 lines; final CodeAnalytics has zero Storage warnings |
| Review result | No fake separation, false capability, duplicate confinement policy, service location, partial boundary, or reverse dependency remains | No additional repair | Source/dependency audit and direct tests pass |

## Responsibility And Size Map

| Concern | Before cleanup | After cleanup |
| --- | --- | --- |
| Browse contracts | one initial 674-line aggregation during SB02 | primitives 228, budgets 162, models 299, drivers 171, settings 88 |
| Filesystem provider | browse logic initially headed toward existing content driver | browse driver 329, entry mapper 136, shared path policy 99, cursor codec 32; existing content driver remains separate |
| FTP protocol | one 363-line transport | transport orchestration 208, request/URI obsolete-API boundary 103, MLSD parser 84 |
| IPFS response mapping | whole bounded JSON envelope in transport | HTTP transport 341 plus incremental nested-array stream 228; stat revision documents remain separately bounded and small |
| Cursor protection | filesystem codec 92 plus remote codec 72 duplicated crypto | provider codecs 32/28 plus one 63-line protector |

The nested-array stream exposes many members because `Stream` requires its read/seek/write contract; it has one responsibility, no provider knowledge, and is exercised through production HTTP tests. No source file in the new provider foundation triggers a CodeAnalytics large-file warning.

## Dependency And Capability Review

No project reference changed. Infrastructure still references only SharedKernel and has no FileTools, module, UI, or Web dependency. SB04 added `Microsoft.Extensions.Http` 10.0.0 solely for handler pooling; SB05 added no package edge. Final scoped dependencies/cycles are zero.

| Provider | Advertised browse facts | Deliberately not advertised |
| --- | --- | --- |
| Filesystem | browse, stat, metadata, provider order, source-version continuation | global sort/search |
| IPFS | browse, metadata, provider order, revision continuation, immutable version when CID-proven | global sort/search/stat facade/delete authority |
| FTP | browse, metadata, provider order | consistent continuation, global sort/search/stat; servers without reliable MLSD return Unsupported |

Entry capabilities remain descriptive and minimal: containers browse; files read. They do not grant write/delete authority or infer semantic authorization.

## Testability And Pattern Review

PSR-01/PSR-02 remain justified. The provider registry and adapter boundaries select explicit typed implementations without fallback. The two one-implementation transport interfaces isolate external protocols and allow direct instrumented fakes; removing them would force credentials/network into unit tests. Path policy, cursor protector, MLSD parser, JSON array stream, and provider drivers are directly testable without Web or full storage orchestration.

No new partial or nested provider boundary exists. No service locator is used. No broad manager/facade or inheritance hierarchy was introduced.

## Validation

- Infrastructure Release build, warnings as errors: Pass, 0 warnings/errors.
- Integration project Release build, warnings as errors: Pass, 0 warnings/errors.
- Combined non-scale SB03/SB04 invariant tests: Pass, 26 cases.
- Unit Storage regression excluding scale: Pass, 73 cases.
- Filesystem 100,000-entry structural scale: Pass in 29s; page-one/next-page bounds retained.
- Storage integration regression: Pass, 10 cases in 1m7s.
- Focused Infrastructure and unit-test format verification: Pass.
- Source/performance audit: no `GetFiles/GetDirectories/GetFileSystemEntries`, per-call `HttpClient`, full-body byte/memory bridge, blocking task access, `Task.Run`, FileTools/UI/Web dependency, or new partial. The only `OrderBy` hit is the pre-existing singleton registry key ordering, not provider page work.
- Final CodeAnalytics: `snap-20260713031012-d26717a4`; one project, 69 documents, 131 scoped types, 827 members, zero scoped dependency/cycle, zero Storage warnings.

Dependent smoke is present at both boundaries: current filesystem replacement content/stat flows through the shared path policy, and production IPFS HTTP uses the injected client, request-local credentials, stat/list/stat MFS revision, early-stop large listing, and lifetime-owned read stream. Existing 10 integration cases remain green.

## Progression Decision

Checkpoint A result is an unqualified `Pass`. SB06 is unlocked. A later contradiction involving path confinement, stale content, capability honesty, remote classification/revision, cursor trust, O(total-source) page one, or unbounded content/response work reopens its owning SB02-SB04 phase and SB05; all dependent phases must revalidate.
