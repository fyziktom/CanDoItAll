# SB15 Behavioral Proof

## Decision

- Status: `Pass`.
- Scope: authorized Resources catalog over project/filesystem/IPFS/FTP, governed stable storage-object promotion, post-save revision, strict persistence, current-authority reopen, and focused Registry/Browse desktop UI.
- Progression: SB16 is unlocked. Any durable handle/token authority, stale binding acceptance, revision-before-save, ordinary-editor bypass, provider-class lie, or browser-dependent reopen reopens SB15.

## Architecture And Responsibility Result

| Owner | Responsibility | Result |
| --- | --- | --- |
| `ResourceFileSourceCatalog.cs` | Current project/storage truth and typed source identities/fingerprints | 276 lines; bounded query; no UI/session behavior |
| `ResourceFileToolsStorageBindingSource.cs` | Current source revalidation and one bounded integration root | 70 lines |
| `ResourceFileBrowseCoordinator.cs` | Bounded browser construction and workspace ownership | 75 lines; retention Disabled |
| `StorageObjectResourceConnectorPlugin.cs` | Strict provider-neutral connector schema/validation/display projection | 215 lines; unmapped members rejected |
| `ResourceStorageObjectPromotion.cs` | Current activation/authorization, EF persistence, post-save revision, cleanup | 318 lines split between typed service and real persistence boundary |
| `ResourceStorageObjectInteraction.cs` | Strict stable-config reopen through current authorization | 107 lines |
| `ResourceFileBrowsePane.razor` | Catalog/source/workspace/interaction rendering and orchestration | 409 lines; no persistence or authorization decision |
| `ResourceStorageObjectPromotionDialog.razor` | Focused form and service invocation | 215 lines |
| `ResourcesPage.razor(.cs)` | Registry/Browse selection and registry refresh only | Existing editor remains; governed resources are read-only |

No new partial class, command hierarchy, service locator, broad facade, Workbench reference, reverse Integration edge, or new Components layer was introduced. The existing DI factory that aliases the connector registry is composition-root wiring, not runtime service location. Broad exception catches are limited to typed UI redaction, persistence translation, and best-effort authority cleanup.

## Persistence And Authority Behavior

`ResourceFileSourceCatalog` reads current projects and current enabled read/browse storages. Project source keys are `project:{id}` and native storage keys are `storage:{id}`. Storage fingerprints include the authority-relevant provider, enabled/read state, endpoint, connection/configuration, and credential reference while deliberately excluding `UpdatedAtUtc`, because catalog bootstrap reads can update that operational timestamp without changing authority.

Promotion does not trust a display row or old handle. It reactivates the current browser occurrence, loads the current actor context, authorizes exact View access against the current storage/scope, constructs strict stable configuration, awaits the EF save, publishes a semantic revision only for a newly created resource, and revokes temporary authority. Duplicate canonical configuration in the same project returns the existing resource and publishes no revision.

The stable connector allows filesystem relative path, IPFS content address or remote path, and FTP remote path. It never persists opaque authority. The ordinary Resources save path explicitly rejects the storage-object connector so only governed Browse promotion can create it. Reopen starts from the durable stable identity, resolves the current catalog again, creates a new known-file authorization session, and gives independently owned content to shared FileInteraction.

## Hostile And Failure Proof

- Forged/stale occurrence, changed source fingerprint, missing source, wrong storage, and cross-actor authorization fail before persistence.
- Persistence exception and cancellation create no resource and publish no revision.
- Unknown configuration properties such as `Handle` and invalid provider/locator combinations fail strict validation.
- General resource editing cannot create or mutate the governed connector.
- IPFS CID, IPFS MFS, and FTP native locators pass provider-specific promotion theories without display-path authority.
- A missing source on reopen performs zero activation; interaction disposal releases the exact current session.
- Unknown UI/runtime failures receive generic text. Logs contain source/storage/project/resource IDs, provider kind, created flag, and revision—not raw locator, path, token, credential, content, or unmasked actor.

## Automated Proof

| Surface | Command scope | Result |
| --- | --- | --- |
| Unit | catalog/binding, promotion/red-team, connector/persistence/reopen | `22/22 Pass` |
| Components | source-class truth, promotion dialog activation, success/revision/open callback | `3/3 Pass` |
| Integration | real PostgreSQL plus actual bootstrap filesystem browse, persist, revision, current authorized reopen/content | `1/1 Pass` |
| Build | Release Web graph with `-warnaserror` | `Pass`, 0 warnings, 0 errors |
| Format | Resources project `--verify-no-changes --no-restore` | `Pass` |
| Diff hygiene | focused `git diff --check` | `Pass`; line-ending notices only |

## Performance And Scale Review

The standard .NET performance scan found no sync-over-async, `Task.Run`, per-call `HttpClient`, unbounded browser retention, or provider item loop in the new Resources owners. Project enumeration is structurally capped at 512 plus one for fail-fast detection. Each binding contains one root. Browser search is capped at 32 containers, 2,000 items, five seconds, concurrency one, 200 results, and 2 MiB retained state; browser retention is Disabled. Reopened content is capped at 16 MiB.

The remaining LINQ and allocations materialize bounded current catalog/configuration results around EF/storage calls. Persistence uses one canonical-config query and one awaited save; duplicate promotion avoids a second row and revision publication.

## Managed Browser Proof

The final warning-clean Release DLL ran through the managed `PublishedDll` lane as `app_7918e62ad1d24750830dd3f6adad5dec` at `http://127.0.0.1:5505`. The direct managed project lane remains unsuitable on this Windows checkout because generated artifact paths exceed the legacy path limit; no product workaround was introduced.

The real Resources page showed four current projects, one filesystem source, and explicit zero IPFS/FTP groups. A real `body-text.txt` occurrence from the bootstrap filesystem was promoted to `TetrisGame`. The UI reported post-save source revision one, registry counters became one, and the persisted resource rendered as a governed read-only storage object. After runtime restart, promoting the same stable occurrence returned “already registered” with revision zero; `Open stored object` re-resolved current source/actor and returned actual content through shared FileInteraction.

The browser pass caught and repaired an unsupported `Class` parameter on the packaged FileInteraction component. Final scoped CSS targets its rendered `.cdi-ft-interaction` root. At 1900x1200 the interaction measured 1262x756 with `flex: 1 1 0`; at 1440x900 it measured 810x456. The browse pane remained overflow-hidden, the source list owned vertical scrolling (`521/1142`, `overflow-y:auto` at 1440x900), and the detail/interaction remained bounded with no lateral overflow.

On the clean final navigation, browser console reported zero errors and zero warnings; Blazor initializer and negotiate requests returned 200. Managed logs showed current source open, bounded filesystem browse, idempotent promotion with `Created=False` and `ScopeRevision=0`, and current authorized content open. The controlled proof resource was deleted through the Resources registry; final counters and matching rows returned to zero. No project, storage, file, credential, or profile was changed.

### Browser Artifacts

- `browser/sb15-promotion-success-1900x1200.png`
- `browser/sb15-governed-registry-1900x1200.png`
- `browser/sb15-governed-reopen-1900x1200.png`
- `browser/sb15-governed-reopen-1440x900.png`

## Dependency And C# Gate

Fresh focused CodeAnalytics again failed because the installed server transport closed. Closure uses deterministic evidence instead of inventing a snapshot identifier:

- Resources references Projects, Integration, Integration.Abstractions, Infrastructure, and existing module/foundation dependencies.
- Integration source/project scans contain no Resources or Workbench reference.
- The full warning-clean Release Web graph builds, so no project-reference cycle was introduced.
- New business owners are top-level sealed services/records; no new partial or page-owned authority/persistence logic exists.
- Focused source scans and direct tests cover service wiring, stale current binding, strict persistence, cleanup, bounded work, and redaction.

## Closure

All five SB15 acceptance checks pass. The catalog is truthful, promotion uses current authority, durable configuration is stable and strict, failure/cancellation are atomic with revision publication, persisted objects reopen through current authorization, and desktop/dependency/persistence/C# gates pass. SB16 is unlocked.
