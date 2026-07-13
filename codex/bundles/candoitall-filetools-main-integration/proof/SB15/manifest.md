# SB15 Governed Proof Manifest

Date: 2026-07-13. Closure decision: `Pass`.

## Scope And Provenance

- Production scope is the Resources-owned authorized source catalog, current storage binding, bounded browser coordinator, provider-neutral `resource.storage-object` connector, promotion application service, persistence writer, current-authority reopen service, and focused Registry/Browse UI.
- The implementation consumes exact `CanDoItAll.FileTools.FileBrowser.Core/0.1.0`, `CanDoItAll.FileTools.FileBrowser.Components/0.1.0`, `CanDoItAll.FileTools.FileInteraction.Core/0.1.0`, and `CanDoItAll.FileTools.FileInteraction.Components/0.1.0` packages. Resources references the neutral and implementation integration projects directly; Integration has no Resources or Workbench reference.
- `source-hashes.sha256` records every affected Resources owner and its direct unit, component, and PostgreSQL integration proof from the final verified source state.

## Evidence Index

| Evidence | Purpose | Result |
| --- | --- | --- |
| `semantic-invariants.md` | Named catalog, authority, persistence, revision, reopen, lifecycle, scale, and redaction invariants | Pass |
| `behavioral-proof.md` | Architecture, transaction, hostile-case, browser, and progression review | Pass |
| `transcripts/test-results.txt` | Final unit/component/integration/build/format results | Pass: 22 unit, 3 component, 1 integration; zero-warning Web build |
| `transcripts/source-architecture-audit.txt` | Dependency direction, responsibility, anti-pattern, and tool-fallback evidence | Pass with recorded CodeAnalytics and Components transport failures |
| `transcripts/browser-proof.txt` | Managed runtime, DOM geometry, duplicate/reopen, console/network, and cleanup record | Pass at 1900x1200 and 1440x900 |
| `browser/sb15-promotion-success-1900x1200.png` | Real filesystem object persisted with post-save revision publication | Pass |
| `browser/sb15-governed-registry-1900x1200.png` | Persisted object is read-only in the ordinary editor and exposes governed source navigation | Pass |
| `browser/sb15-governed-reopen-1900x1200.png` | Duplicate promotion is idempotent and current-authority reopen returns real content | Pass |
| `browser/sb15-governed-reopen-1440x900.png` | Large-desktop responsive/scroll contract | Pass |

## Source Catalog Matrix

| Source class | Truth producer | Stable scope identity | Capability rule | Proof |
| --- | --- | --- | --- | --- |
| Project | Current project rows plus managed project storage binding | `project:{projectId}` with current project/storage fingerprint | Read/browse only | Unit catalog test and browser catalog showing four current projects |
| Filesystem | Current enabled registered storage | `storage:{storageId}` plus provider/config/credential fingerprint | Registered read+browse driver required | Unit binding tests, PostgreSQL integration, real browser promotion/reopen |
| IPFS | Current enabled registered storage | Same provider-neutral storage identity/fingerprint | Registered read+browse driver required | Unit catalog and provider-native promotion theory for CID and MFS locators |
| FTP | Current enabled registered storage | Same provider-neutral storage identity/fingerprint | Registered read+browse driver required | Unit catalog and provider-native remote-path promotion theory |

The browser renders all four named classes and explicit zero counts. It does not invent unavailable remote sources.

## Producer, Consumer, And Lifecycle Matrix

| Boundary | Producer | Consumer | Lifecycle and failure behavior |
| --- | --- | --- | --- |
| Current catalog | `ResourceFileSourceCatalog` | Browse pane, binding source, promotion, reopen | Reloaded from current database/storage registry; capped project set; missing or fingerprint-changed source fails closed |
| Storage binding | `ResourceFileToolsStorageBindingSource` | Integration binding provider | Re-resolves current catalog before driver access; host cache policy is `UseStoragePolicy`; one bounded root |
| Browse workspace | `ResourceFileBrowseCoordinator` | `ResourceFileBrowsePane` | Scoped, bounded search, FileBrowser retention Disabled; workspace disposal owns browser session |
| Promotion authority | Browser activation plus `IStorageFileAccessAuthorizationCoordinator` | `ResourceStorageObjectPromotionService` | Selected occurrence is reactivated and authorized for current actor/View; handle is revoked in `finally` |
| Persistence | `EfStorageObjectResourceWriter` | Resources registry | One awaited EF save; only stable curated config is persisted; duplicate stable identity is idempotent |
| Revision | `IFileCatalogChangePublisher` | Browse/catalog consumers | Published only after a newly created resource is durably saved; duplicate/failure/cancel publishes nothing |
| Reopen | `ResourceStorageObjectInteractionService` | Shared `FileInteraction` | Strict config parse, current source resolution, new current activation/authorization, independently owned interaction session and release |

## Persisted Schema And Authority Decision

The connector persists `SourceKey`, `StorageId`, `ProviderKind`, `LocatorKind`, canonical provider-native `Locator`, display name, media type, and content length. The serializer rejects unmapped properties. The allowed locator combinations are filesystem relative path, IPFS content address/remote path, and FTP remote path.

The following are deliberately absent from durable configuration: opaque handle, token, actor/session/runtime/profile identity, browser key, authorization revision, unsigned URL, absolute display path, and credential material. `LocationOrIdentifier` is a non-authoritative hashed display identifier. General Resources save rejects this connector so callers cannot bypass governed promotion.

## Transaction And Negative Result

The final order is activate current occurrence, obtain current context, authorize exact View operation, persist stable configuration, publish revision after created save, and revoke the temporary handle. Persistence failure, cancellation, cross-actor authorization, stale source fingerprint, forged occurrence, wrong storage, invalid connector combination, and unknown configuration fields are directly tested. These cases create no resource and publish no revision. Cleanup failure is logged by type without masking the original failure or a completed durable save.

## Build, Regression, And Tool State

- Final focused unit run: 22 passed, 0 failed.
- Final focused component run: 3 passed, 0 failed.
- Final real PostgreSQL integration run: 1 passed, 0 failed.
- Web Release build with `-warnaserror`: 0 warnings, 0 errors.
- Focused `dotnet format --verify-no-changes`: Pass. Focused `git diff --check`: Pass with line-ending notices only.
- Fresh focused CodeAnalytics snapshot failed at the installed MCP transport boundary with `Transport closed`. Closure therefore uses the checked project-reference graph, direct source assertions, focused tests, and full warning-clean Release graph; no snapshot identifier is invented.
- Components MCP exact discovery also returned `Transport closed`; accepted existing BaseLib `Tabs`, `ListDetailShell`, `Dialog`, `Stack`, `Cluster`, `SurfaceCard`, `Button`, status, and alert usage was inspected in repository source before reuse.

## Downstream And Progression

SB15 closes and unlocks SB16. An unstable persisted locator, durable handle/token authority, authorization after persistence, revision-before-save, source-class omission, silent provider fallback, ordinary-editor mutation, or browser-dependent reopen reopens SB15 and dependent closure proof.
