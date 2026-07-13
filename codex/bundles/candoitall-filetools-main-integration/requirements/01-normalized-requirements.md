# Normalized Requirements

| ID | Requirement | Observable acceptance |
| --- | --- | --- |
| R001 | Preparation only in this run. | Git diff is limited to `bundle://` plus the requested repository `.gitignore` removal; no product/test/project/package implementation file changes. |
| R002 | First product phase improves Storage browsing before UI. | SB02-SB05 close before any UI subbundle enters. |
| R003 | Storage browse contracts are Infrastructure-native and capability-honest. | Infrastructure has bounded list/path/stat request/page records and a dedicated registry; it has no FileTools reference. |
| R004 | Browse registration and selection are typed and deterministic. | Duplicate provider registration and unknown provider fail explicitly; no last-wins or default fallback. |
| R005 | Filesystem browsing is confined, fresh, bounded, cancellable, and safe against traversal/reparse disclosure. | Positive/negative provider tests and live mutation proof pass. |
| R006 | IPFS distinguishes immutable CID/DAG from mutable MFS and FTP advertises only proven operations. | Provider-specific behavior and unsupported-operation tests pass; secrets/raw endpoints are absent from UI-safe errors. |
| R007 | Browse/cache settings are typed, backward-compatible, validated, and stored in `ConfigJson`. | Missing config becomes Disabled; invalid mode/TTL/page/item/immutable combinations fail on load/save/startup as designed. |
| R008 | Main consumes validated FileTools packages, not sibling project references. | Exact nupkg/snupkg IDs, version, SHA-256, dependency graph, feed intake, restore, and asset load pass. |
| R009 | Integration boundaries preserve dependency direction. | Infrastructure -> no FileTools; modules -> Integration.Abstractions/FileTools as needed; Composition owns concrete wiring; CodeAnalytics shows no new cycle. |
| R010 | Browser intent is re-resolved and authorized server-side. | Stale, forged, cross-actor, cross-runtime, expired, wrong-operation, and revoked handles fail without storage invocation. |
| R011 | Existing unsigned managed-file endpoints are not authority for the new flow and are hardened. | Endpoints have explicit authorization/handle policy or are replaced/deprecated with compatibility tests; unsigned reference alone cannot read protected content. |
| R012 | FileInteraction content/save are browser-session independent and revision-aware. | Authorized content loads after browser disposal; save reauthorizes and enforces expected revision; conflicts remain dirty. |
| R013 | Cache is optional, bounded, runtime/source/authorization-aware, and Disabled is true pass-through. | No lookup/store/coalescing in Disabled; no cross-scope leak; failed/cancelled mutations do not advance revision. |
| R014 | Distributed secondary remains disabled until durable shared revision proof exists. | Enabling Hybrid/distributed mode without the future gate fails explicitly. |
| R015 | Project file search is the sole UI pilot. | One project's authorized files browse/search; activating one known Markdown/text file opens read-only FileInteraction; negative access and stale-content cases pass. |
| R016 | Broader UI cannot start until the pilot cleanup gate passes. | SB11 records architecture, dependency, component, browser, screenshot, console, and progression Pass. |
| R017 | Project portfolio/files filters have one source of truth. | Cards/files panes consume the same directly tested filter/hierarchy projection and deterministic source-set fingerprint. |
| R018 | Project card files open in a focused dialog without enlarging `ProjectModalHost`. | Card action, dialog lifecycle, disposal, browse/search/open behavior, and desktop proof pass. |
| R019 | Project Structure browsing uses focused scope/window/coordinator types. | No new `ProjectStructurePage` partial; floating window has one scroll owner, correct overlay layering, and authorized node/project scopes. |
| R020 | Process-run artifacts are always-current by default. | Managed/output/product roots are resolved by Processes-owned policy, authorized, and use host/session Disabled cache; next read observes mutation. |
| R021 | Resources browses authorized project/filesystem/IPFS/FTP sources and promotion re-resolves authority. | Registry/Browse flow and storage-object connector persist stable configuration only after current authorization. |
| R022 | FileInteraction migration is incremental and package-selective. | Known viewers/editors are registered explicitly; unsupported files remain explicit; old preview path is removed only after replacement proof. |
| R023 | Editing never writes directly from UI/FileTools. | Awaited host save adapter persists, logs masked identifiers, bumps revision only after success, and handles conflict/cancel/failure. |
| R024 | UI uses shared CanDoItAll components before custom structure/CSS. | Components MCP discovery and chosen component usage/examples are recorded; any custom wrapper/CSS has a documented gap. |
| R025 | UI validation targets large desktop only. | `1900x1200` primary and `1440x900` minimum checks pass; no small/medium/tablet/mobile implementation or proof is required. |
| R026 | Every phase ends with architecture/proof cleanup before dependent work. | SB05, SB09, SB11, SB17 pass; each architecture-relevant subbundle also passes the C# review gate. |
| R027 | Large owners do not gain final responsibility. | Source assertions show focused top-level services/components; old pages/dashboard/composition own thin state/wiring only and no new partial is added. |
| R028 | Failures and logs are actionable and do not leak secrets or absolute authorization roots. | Negative tests and log capture verify typed failures, correlation/source/binding context, and masking. |
| R029 | Validation is layered and affected-scope complete. | Unit, integration, component, host, Playwright, build/format, package, dependency/cycle, anti-stub, and source assertions run as assigned. |
| R030 | Closure is honest and traceable. | Every raw note maps to owner/proof and closes Solved/Partially solved/Not solved; no missing proof is hidden as residual risk. |
| R031 | Large-source browsing is bounded in provider work and memory, not only returned page size. | A 100,000-entry filesystem fixture returns page one within typed inspection/metadata/state budgets; source audit finds no full unbounded enumeration/sort/hash before page one. |
| R032 | Ordering and continuation capabilities are truthful. | Provider-native cursor/order or explicitly bounded indexed snapshot is used; unsupported global sort, stale cursor, and budget exhaustion fail/return partial through typed outcomes. |
| R033 | Search work and retained state are bounded and cancellable. | Item/container/time/concurrency/match/snapshot limits, partial status, latest-request cancellation, and expiration/eviction tests pass. |
| R034 | Storage/network content I/O is connection-efficient and streaming until the bounded interaction layer. | No per-call `HttpClient`; remote reads use response streaming/leases and range/length limits; no unbounded `byte[]` to `MemoryStream` bridge. |
| R035 | Performance-critical .NET anti-patterns do not enter browse/search/content hot paths. | Scoped scan plus source review finds no sync-over-async, `Task.Run` I/O wrappers, unbounded LINQ materialization, N+1 metadata outside budget, or repeated hot serializer/client construction. |
| R036 | Performance behavior is observable without leaking sensitive paths. | Tests/telemetry record inspected/returned items, metadata calls, duration, bytes, retained state, cache/partial/cancel outcome with masked low-cardinality dimensions. |
| R037 | FileBrowser is used only for a semantic collection/container browsing request. | Typed browse intent initializes browser/session; no browser is initialized merely because a known file has siblings or an interaction dialog is open. |
| R038 | A known file opens FileInteraction directly. | Authorized `FileReference` and content source reach FileInteraction with zero FileBrowser session/catalog/list/search/cache calls. |
| R039 | Project Structure asset double-click behavior is preserved. | Characterization and browser tests prove image/PDF nodes still open the existing dialog lifecycle, now with direct FileInteraction, and close/replacement/disposal work. |
| R040 | Performance proof is regression-gated and measurement-led. | SB03/SB04/SB10/SB13/SB16 baselines and structural counters pass; SB05/SB11/SB17/SB18 review them and reject unmeasured micro-optimization or material regression. |
