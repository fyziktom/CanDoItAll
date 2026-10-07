# S0 / P1-R2 — a failed JS import is rethrown when closing the editor

Priority: bounded UI lifecycle repair before P2. Evidence: inspected `ProjectModalHost.razor` submit/dispose paths (R05); no reviewer-run browser reproduction. This is separate from P1-R1 and does not require changing BaseLib Dialog.

## Exact path

`HandleSaveAsync` caches the import task in `moduleLoad`, awaits it and catches `JSException`. It displays: "Unable to validate the current form. Close and reopen the editor before saving." The cached task remains faulted.

The suggested Close removes/replaces the keyed modal. `DisposeAsync` then executes `await (await moduleLoad).DisposeAsync()` and catches only `JSDisconnectedException`. A failed import with an ordinary `JSException` rethrows the already-handled original error before there is any module to dispose. This can turn a recoverable validation/import failure into an unhandled teardown error. Cancellation has a related unhandled path; do not equate all active JS failures with expected retirement.

## Failing-first and negative controls

1. Render the actual modal with a valid draft. Fail only its real feature-module import with an ordinary JSException. Submit and verify no Save callback/native mutation occurred and the intended recovery message is shown.
2. Close/remove the component using the repository's correct rendered-component-disposal helper. Before the fix this re-awaits the failed task; after the fix teardown must complete without repeating the handled import error. No module existed to dispose.
3. Reopen a fresh editor with a successful import. Submit exactly once and verify the new component validates/dispatches normally; do not reuse the poisoned task from the old instance.
4. Hold import during retirement, then complete success/failure/cancellation. A late successfully acquired module is released exactly once; no stale form inspection, focus, callback or save occurs.
5. Exercise loaded-module disposal and pending `inspect`/`focusInvalid`, repeated retirement and an independent successor editor. Cleanup must not stall unrelated rendering indefinitely. Retain diagnostics for unexpected active JS faults.

Keep original draft values, EditContext and existing invalid-date checks. Do not fix this by skipping native form validation, replacing the real browser module with a fake, removing error assertions or globally swallowing JS/TaskCanceled exceptions.

## Bounded implementation

Give the import/loaded module one explicit owner. Distinguish no-module/import-failed from loaded-module cleanup and observe pending acquisition through a safe retired continuation when necessary. Capture the originating draft/form/lifetime before awaited interop and recheck before touching UI. A known reported import failure must not be thrown again by a cleanup that has no module to release. A genuine unexpected disposal problem still needs safe diagnostics and completion of other owned cleanup.

Use the existing component patterns where appropriate; no new generic interop manager, package bump or sibling redesign is required. Preserve the already repaired shared Dialog and all P1 source/asset paths. Complete S0 together with P1-R1, then continue to Files P2.
