# W1b explicit Workspace head: source checkpoint

Status: production/model and test source authored in the managed continuation checkout.
No W1b build, test, migration generation, database work, browser session or static gate
has been run by this author. Parent owns serialized validation. This is not release
closure or a claim that Workspace has durable receipts.

## Exact boundary

The ordinary writer, coordinated provider/defaults bootstrap and default-provider
transfer use the explicit `IsCurrent` row. `ConcurrencyToken` is application managed;
ordinary acceptance advances it even when normalized values are unchanged. Ordinary
saves require the acquired dataset namespace plus explicit Empty or Existing head.
No tokenless overload remains for Workspace defaults. Bootstrap waits on lifecycle
before its first provider read; transfer stays inside its existing enlisted transaction.

The native adapter keeps the exact accepted snapshot before acknowledgement or refresh,
preserves per-field edits made after dispatch, and separates a comparison read from
explicit reload/current-based reconciliation. One previous draft remains reachable until
explicit discard. Unknown outcomes block the original draft; reopening is not durable
recovery and is not claimed as such. `OwnerRetired` is distinct from DatasetChanged;
`CleanupPending` requires known commit acknowledgement.

HTTP returns the historical accepted snapshot directly, never a post-save current GET.
Input is bounded to 65536 UTF-8 bytes, requires all six values and the original head,
rejects duplicate case-insensitive command/value members, and exposes typed known
rejection versus Unconfirmed. Generic 413/415 bodies remain separate. The provider
availability check is preflight only; it is not an atomic cross-owner invariant.

## Source-derived focused cases

| Source class | Discovery/runtime rows intended | Scope |
| --- | ---: | --- |
| `WorkspaceSettingsConcurrencyTests` | 10 / 10 Facts | Independent owner contexts, Empty race, actual SQL original revision, normalized no-op, activation change, inactive-row selection, real committed lost acknowledgement, acknowledged cleanup failure, rollback/cleanup and read/lock coexistence, provider bootstrap lifecycle-first observation. |
| `WorkspaceDefaultsConcurrencyTests` | 5 / 5 Facts | Explicit conflict recovery, sticky unknown, presentation failure after acceptance, malformed acknowledgement at existing owner seam, retired read completion. |
| `WorkspaceSettingsHttpOutcomeTests` | 5 / 5 Facts | Existing activity fault plus stale head, malformed/bounded request variants, historical acknowledgement despite later write, emitted closed schemas. Variants inside a Fact are not additional cases. |
| `WorkspaceSettingsBrowserTests` | 3 / 3 Facts | Existing production/sandbox cases plus two native browser contexts with a stale draft, current comparison and explicit reconciliation. New proof uses 1920×1080 and retains multiline notes for copying. |

Existing classes edited without adding cases: Workspace UI draft 19; Workspace owner
persistence 2; Workspace preference transfer 4; business bootstrap 12; Workspace core
owner outcomes 10; API section permissions 3. Mechanical fixture updates are listed in
the exact source manifest; their broader source counts and validation filters remain
parent-owned. The repeat transfer assertion uses the existing first Fact and verifies
zero copied records with unchanged target revision/data.

The browser source reuses the existing `WorkspaceSettingsBrowserCollection` host and
database lease. It creates two browser contexts inside `await using`; no new host or
database stack is proposed. Planned evidence is under the existing
`output/playwright/workspace-settings-core-ui` owner: `production-defaults-conflict.png`
and `production-defaults-reconciled.png`. These files have not been produced by W1b yet.

## Renderer review

Only `WorkspaceDefaultsSurface` and new `WorkspaceDefaultsComparison` register entries
belong to W1b. The comparison accepts immutable six-field values, uses existing
FormSection/Stack/TextBlock/TextArea contracts, and has no service, storage, transaction
or authority dependency. The controller owns all reads, writes, draft retirement and
recovery choices. No new CSS or package was added. Component metadata was inspected
from existing source; no Components MCP proof is claimed. The source manifest contains
the register hash, while the register stores the two normalized-LF renderer hashes.

## Migration and remaining gates

Parent must generate/review the canonical migration together with the frozen C2 model.
For each existing dataset, choose at most one current row using `UpdatedAtUtc DESC,
Id ASC`; preserve every row, its values and timestamp. Backfill revisions without empty
GUIDs, and create the unique filtered `IsCurrent` index only after head selection. An
empty table stays empty until its first owner/bootstrap write. Stopped-host deployment
is required: old timestamp-selection binaries cannot run concurrently with this model.
Required migration proof remains empty/single/multiple/tied rows and repeat readiness.

The prospective deliberately overlapped bootstrap/human-save and transfer/ordinary-save
arrangements are not implemented by the authored test file. Existing bootstrap tests
prove sequential retained customization/repeat and coordinated rollback; they are not
evidence for those new races. Performance 1/8/32 hot writers, bounded refusal/wait counts,
SQL/allocations and unrelated-owner coexistence remain unmeasured.

The sibling SPA settings writer still sends the old tokenless payload and is not cut
over. It must acquire the typed expectation, preserve untouched provider/default values
and later edits, forward cancellation through the reviewed generator change, and use
the dormant single-attempt transport. Actual emitted schema/TypeScript roundtrip and
native/UI/HTTP runtime proof are enablement gates. W1c durable receipt/restart recovery
remains explicitly open; no Projects epoch/receipt is reused for Workspace.

## Files and shared ownership

`P05-W1b-source-inventory.json` records the explicit authored file set with SHA-256 and
byte length. It does not certify unrelated managed changes. The two composition/API
registration files are shared: preserve C2 readiness/registrations and the separately
frozen Projects OpenAPI document transformer hunk. Parent must transfer the combined
reviewed file or apply the W1b hunks, never overwrite a sibling's later change blindly.
The frozen Projects documentation repair and its corrected 3.1.1 evidence are separate.
