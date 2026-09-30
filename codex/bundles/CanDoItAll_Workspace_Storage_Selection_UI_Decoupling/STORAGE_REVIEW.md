# Review of Storage catalog administration and the API prerequisite

## Decision

Preserve the implemented architecture and proceed with one bounded prerequisite followed by the next small Workspace slice. No additional backend/routing blocker was established in the inspected paths. This is a connected-source review, **not** an independent product build, runtime reproduction or full-repository certification. See [source coverage](SOURCES.md) and [proof status](PROOF_STATUS.md).

Reviewed HEAD: `cbb135c7c8d76ff50a624c142f12faf8c9b55f91`. Its two-commit delta from the preceding API implementation includes archived assignment input and the Storage/API implementation. Do not mistake the archive for product code or repeat its tasks. [EV01, EV03, EV04]

## Previous findings

| Finding | Source review outcome | Preserve / verify |
|---|---|---|
| AP-R1: disposed read source retained after authority retirement | ApiPageController now detaches only its own CTS by reference in finally independently of publication eligibility; Dispose is idempotent | Keep exact-source cleanup, noncooperative completion and successor-token negative controls |
| AP-R2: current list denial retired only a child | A separate current-denial callback is used; the account page passes shared authority.Dispose. Added rendered tests cover both account and token lists, sibling bearer/password retirement, correction reads and a fresh retry | Keep shared current-denial retirement distinct from ordinary child closure and obsolete denial |
| Prior Files destination identity | Latest maintained record reports preserved regression coverage; this iteration did not independently reread its complete controller | Do not redesign Files; retain current focused identity tests |

The AP tests are source evidence that the relevant cases exist, not proof they passed in this review. Their actual result is reported separately by the implementer. [AP01–AP03, EV03]

## Storage implementation to retain

Contracts have no project implementation dependency. The RCL references its own contracts and BaseLib; the sandbox links parity CSS as content rather than referencing Web. Production retains a thin host with captured Recovery identity and profile/caller retirement. [ST11–ST14]

The session has independent catalog/secrets/routes/exact-editor read lanes, stable drafts, bounded operation receipts, captured commands and a mutation revision for read-back. Draft merge preserves newer raw fields and only merges routing that was acknowledged complete. These are useful feature-local protections, not a new framework to export to the picker. [ST01–ST04, ST09]

The adapter captures the original profile/generation and uses the existing runtime write fence. Strict editor Save/Delete checks live at real persistence acquisition while legacy methods retain their existing default behavior. Catalog, per-purpose routing, driver completion and Activity are separate facts. Confirmed IDs are retained; an observed row does not prove which unacknowledged request created it. [ST05–ST08]

Owner tests inspect actual EF records and routing, lost acknowledgements, deletion between acquisition and flush, credential purpose and legacy/new draft-test parity. The reviewed first part of the owner test file is not a substitute for executing its complete fixture. [ST10]

The filesystem draft Test refusal is explicitly preserved: the original draft input lacks the host binding required by the real driver. Do not loosen binding or substitute a different saved-record test service to turn this green. This is an inherited owner behavior, not a new requested feature. [EV03, ST10]

## SCAT-R1 — an acquired same-ID selection discards the current draft

Severity: bounded user-input loss / editing regression risk. It is not evidence of corrupt persisted Storage data or an authorization bypass. The behavior may reflect the old panel's reload convention; the finding concerns the current extracted editor and its missing ordinary-selection no-op.

### Source chain

1. `CatalogSurface` always connects a list row to `Session.SelectAsync(row.Id)`, including the selected row.
2. `CatalogSession.SelectAsync` unconditionally sets `SelectedId`, sets `Draft = null`, then publishes `new CatalogDraft(editor)` on successful exact read.
3. BaseLib `SelectionListItem.HandleSelectAsync` only checks Disabled. IsSelected is a visual property, not suppression of a repeated activation.
4. Consequently a physical reactivation of the highlighted row follows the destructive replacement path. [ST01, ST03, SC01]

### Deterministic reproduction

Render actual CatalogSurface over a controlled current owner. Acquire an editable existing catalog and retain references to its draft and EditContext. Enter an unsaved name and incomplete numeric text, move to a non-initial wizard step, and establish visible validation. Activate the highlighted catalog row again using its real row button.

Current code clears and replaces the editor and makes another exact owner read. Assert draft/context inequality, loss of raw input and step/validation to make the regression visible. Do not use a fake SelectionListItem or rely solely on calling the handler directly. The source tests already exercise exact missing/retry and A→B→A, but the inspected file lacks this acquired-same-ID case. [ST02, ST09]

Also hold a Save/Test or its read-back, reactivate the same row, then finish the operation. Show that preserving the draft does not bypass the ledger or create another effect, and that new input remains attached to the original editor.

### Repair contract

An already acquired, current, matching non-deleted draft is an ordinary selection no-op. Preserve its raw values, validation, EditContext, wizard step, health provenance and receipt state. An ID placeholder, failed read, missing target, retired context or deleted draft is not evidence of acquisition. Same-ID retry must still work for those appropriate states. If an explicit refresh/discard exists, keep it a deliberate separate intent.

Do not solve the problem by globally disabling selected rows in BaseLib, blocking every reference refresh, treating a failed editor as acquired, or adding a backend concurrency protocol. New/different-target navigation remains an explicit existing transition. The smallest owning-session fix plus actual renderer/browser regression proof is preferred.

### Required verification

See V-S0-01 through V-S0-04 in [the matrix](VALIDATION_MATRIX.md). No reviewer-supplied C# test or runtime success is claimed. Codex must reproduce and validate on its current source before moving to the picker family.
