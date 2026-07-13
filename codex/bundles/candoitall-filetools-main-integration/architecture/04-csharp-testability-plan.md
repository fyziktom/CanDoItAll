# C# Testability Plan

## Characterization Before Movement

- Existing storage save/read/delete/access and managed-file endpoint behavior.
- Project filter/hierarchy semantics before extracting a shared projection.
- Workbench preview/open behavior before FileInteraction replacement, specifically image/PDF node double-click, dialog close/replacement, and open-local separation.
- Process managed/output/product root derivation before moving policy.
- Resource connector persistence before adding storage-object promotion.

## Isolated Unit Tests

- Browse records/settings validation, registry duplicate/unknown behavior, provider capability checks.
- Filesystem path/browse/paging/current-read/error redaction.
- IPFS mapping and immutable/mutable classification with fake HTTP transport.
- FTP browse mapping/unsupported capability with fake transport.
- Storage-to-FileTools adapter mapping, source/item identity, cancellation, and error translation.
- Authorization coordinator and handle registry with fake access/persistence/storage services.
- Cache policy/key/revision state with deterministic clock/runtime snapshots.
- Project filter/hierarchy projection and source-set fingerprint.
- Project/node/run/resource scope providers without constructing pages.
- FileInteraction host coordinator/save adapter without Razor.
- Known-file interaction tests with browser catalog/session/provider spies that must remain at zero calls.
- Provider scale tests assert inspected entries, metadata probes, retained state, bytes, cancellation, and allocations in addition to returned pages and timing.

## Required Negative Tests

- Duplicate provider registration, unknown provider, unsupported operation, oversize page, malformed/stale cursor.
- Traversal/reparse/path disclosure, CID/MFS misclassification, FTP incomplete listing.
- Forged/expired/revoked/cross-actor/cross-profile/wrong-operation handle and unsigned token.
- Cache cross-scope collision, failed/cancelled mutation revision bump, distributed mode without durable revision.
- Stale project source set, unauthorized subproject/node/run/resource source.
- Edit during save, expected-revision conflict, overwrite without policy, stale interaction replacement.

## Integration/Composition Smoke

- Storage config JSON round-trip and bootstrap compatibility.
- DI resolves each native browse driver, registry, outer provider/session/content/save service once with expected lifetime.
- Web endpoint policy denies unauthenticated/unsigned access and streams authorized handle content.
- FileTools static assets resolve from packages in the app.
- Database runtime/profile change invalidates namespaces/handles as designed.

## Component And Browser Proof

- Component tests prove callbacks, disposal, controlled interaction mode, state/close guards, and failure rendering.
- Playwright proves real search/browse/activate/open/save flows, one scroll owner, overlay open state, clipping/layering, keyboard path, loading/empty/error/retry, console/network, and screenshots at `1900x1200` and `1440x900` only.

## Separation Gate

Reject any extraction whose tests instantiate `ProjectStructurePage`, `ProjectsPage`, `LiveProcessesDashboard`, `ResourcesPage`, `RuntimeHostServiceCollectionExtensions`, or an original broad driver merely to exercise the extracted behavior. Production must call the tested seam.
