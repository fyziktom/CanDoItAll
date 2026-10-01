# Files source review — preserve behavior, repair lifetime hazards

These are inspected properties of the still-deferred Files family (R11-R21). They are not all regressions introduced by P1 and are not reviewer-executed reproductions. Confirm the relevant orderings with controlled tests as part of extraction.

## F1 — cleanup and target acquisition can cross

The dialog `OpenAsync` awaits `ResetAsync` and then reads the current ProjectId/ProjectName. Reset awaits disposal through shared `interaction` and `workspace` fields before clearing those fields. Close awaits reset and then invokes the current Closed callback. A re-entrant selection or close can therefore mix old cleanup and new acquisition.

Required: capture the request origin before any await, synchronously detach the exact old references, retire their callbacks and dispose those detached resources. Only the still-current acquisition may publish. An older close must not invoke a callback for a successor. Exercise held release, two opens, Close→Open and A→B→A. Confirm release counts and the validity of the surviving content handle, not just the title.

## F2 — late exceptions, callbacks and cancellation sources

Both surfaces fence some successful completions, but generic failure branches directly overwrite openError/activationError; the pane also assigns a requested fingerprint in its catch. Snapshot callbacks are not explicitly bound to the originally supplied host session. Replacement cancels and disposes an old operation CTS before that operation necessarily returns.

Required: ownership checks for success, failure, notification, snapshot and finally. Capture safe tokens while their source is live, keep source lifetime until the accepted task no longer needs it, and clear fields by identity. Do not revive an old operation just because a public ID or fingerprint matches again. Preserve truthful active-operation failures; global catch-and-ignore is not a repair.

## F3 — projection and actual source replacement must agree

The pane stores requestedFingerprint, then can await preview release and later pass the live Projection into the coordinator. The coordinator resolves sources asynchronously and updates the supplied mutable workspace. Workspace.ReplaceSourcesAsync first awaits Browser.UpdateSourcesAsync and only afterward sets sourceScopes, sourceActions, Revision and ProjectCount.

A generation check after that call can prevent a stale caption, but cannot undo mutation of the supplied browser. Verify the actual accepted provider set, current source/location, scope/action maps and revision. Use a small staged resolution with current-origin acceptance, a sound serialized mutation policy, or independently owned replacement. Preserve a valid selected source and reject removed keys. Do not add a second catalog or loosen source membership checks.

Include overlapping P0→P1→P0, a delayed failing old resolution, two updates to the same workspace, retirement during Browser.UpdateSourcesAsync, successful empty projection, filter-only repeated parameters and explicit catalog revision refresh. If the library already fences one layer, prove how the host's accepted revision composes with it instead of adding redundant gates blindly.

## F4 — preview and browser are separate resources

FileBrowser.DisposeAsync unsubscribes and cancels its own binding; it does not call Dispose on the supplied session (R21). The project workspace owns its browser; the interaction separately owns a FileTools content handle and releaser (R15). Preserve the ability to render content after the browser component is removed. Preserve valid original-context external actions while that preview is shown, without treating a revoked or disposed workspace as valid.

On preview replacement, release only the previous handle. After closing the owning view, independently verify that old content access is revoked and a surviving second view still reads. Test granted-then-cancelled activation and partial construction: release a granted handle with cleanup cancellation independent of the cancelled operation. If that cleanup itself fails, keep both original and cleanup diagnostics and mark incomplete cleanup accurately (R17).

## F5 — actual host actions, not UI-only capability checks

Use existing ItemActionService and FileToolsHostActionRunner. They provide trusted-path/preference resolution, current authorization, allowed system associations, separate download operation and stream delivery (R19, R20). A disabled menu item does not enforce the operation. A queued action must remain attached to its exact item/source/session origin; a reused item key cannot gain a successor's scope.

Keep keyboard internal activation versus pointer double-click's existing external-open fallback, native-launch unavailability in headless hosts, read-only content, current preferred-application policy and explicit download. Do not issue a native action twice after a lost reply or a subsequent refresh. A completed download test compares actual saved bytes, not just a success message.

## F6 — exact existing bounds

| Contract | Reviewed value |
|---|---|
| Maximum project/source count | 64 |
| Browser page size | 50 |
| Progressive-search containers / inspected items | 32 / 2,000 |
| Search duration / concurrent requests | 5 seconds / 1 |
| Retained matches / retained search bytes | 200 / 2 MiB |
| Browser search debounce | 280 ms |
| Preview MaximumContentBytes | 4 MiB |
| Browser state retention | Disabled |

Preserve these separate budgets, current provider-native ordering and source revisions (R18). A 4 MiB preview limit is not a search-retention limit. Test large bounded data, paging and progressive cancellation without turning the regression into an unbounded scan.

## F7 — production parity

The P1 desktop browser checks now assert viewer width and viewport visibility, having found a two-pixel renderer despite correct DOM bytes (R27). Keep those assertions. The Files source order, project name/filter interpretation, Cards/Files exact projection and contextual agent source must stay aligned. The renderer must not perform extra database/source refreshes on every render or keystroke outside the browser's existing bounded search mechanism.
