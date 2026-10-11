# Failing-first and positive-control scenarios

Do not treat these source observations as already reproduced test failures. First use the current production component/owner wiring. Prove a failure, repair the smallest real cause, then keep the same control after extraction. When a finding is already fixed, record the current proof and continue. Do not create a broken workaround to force a red test.

## R1 — Same-definition draft replacement

**Trace:** P02 `HandleDetailTabChangedAsync` → `LoadAsync` → `SyncDefinitionEditor`. The synchronization method assigns all editor fields from the accepted projection. P05 independently synchronizes the open role draft when version/selection changes.

Load an actual definition, type a unique unsaved name/owner/summary and leave a raw invalid value in a field with validation. Switch the real Server-rendered tabs to Runs/Graphs and back, and trigger a same-definition refresh returning the old saved projection. Before explicit Save there must be no native write. The intended dirty values/raw text and validation must still be present; an unrelated successful read must not declare the editor clean.

Repeat for the role dialog, including an invalid non-empty Workflow GUID, and for a step's typed/raw fields. Distinguish genuine target switch/discard from harmless same-target parameter echo. If a newer authoritative version is observed while a draft is dirty, apply the current deliberate conflict policy without overwriting local text or silently rebasing its version.

Hold save/read-back, edit another field while pending where the current policy allows it, then complete the native receipt. Reconcile against the submitted snapshot, preserving later edits and adopting only the appropriate accepted identity/version. Positive controls cover explicit discard, a clean initial load, a valid explicit save and a new genuinely different target. An intentionally locked-while-saving policy must be consistent and justified, not a shortcut removing existing editing behavior.

## R2 — A late file error painted on B

**Trace:** P08 guards Open/Activate/action success with the captured operation but its general catches set error fields without the equivalent origin check.

Use the real ProcessRunFilesDialog and actual coordinator path with a controllable supported scope/session/action boundary. Start A, keep its operation pending, open B, let B succeed, then fail A with a cancellation-ignoring exception. B must retain its content, enabled controls, feedback and target. The old error must not appear as a B failure and diagnostics must identify the originating run safely. Repeat for Open, Activate, authorized Download/local action and close/dispose. Include A→B→A so record-ID equality alone cannot pass.

Keep positive controls: a current operation error is visible with safe wording, current Retry re-resolves its scope, forbidden access stays forbidden, and a late allocated successful result is disposed rather than leaked.

## R3 — Asynchronous cleanup disposes the successor

**Trace:** P08 ResetAsync awaits disposal and then writes shared interaction/workspace fields. Source shows the ordering risk; the actual effect depends on re-entry and owned session implementation.

Hold disposal at the actual supported file/browser/content-session boundary. Retire/open A, start a successor B while old cleanup is suspended, then release cleanup. Assert old resources are disposed exactly once, B resources are not disposed, B references/state remain current and the original close callback cannot close B. Use two simultaneous dialogs as well as parameter retargeting. Detach old resources to locals before await and keep request-owned cleanup independent from current fields. Do not replace the entire dialog with a fake solely to make ordering easy.

## R4 — Launch/new-intent/feed origin and busy ownership

**Trace:** P02's launch/feed paths touch shared busy/feedback after awaits; explicit new intent clears retained fields after awaited sessionStorage removal. Existing definition-editor operation fences are a positive precedent, not something to delete.

Hold launch, projection refresh or storage cleanup for A. Retire A and let B acquire its own UI busy state; release A with success and failure. Neither path may clear B's busy flag, replace B's pending preparation/intent, close B or navigate using B's current target. An admitted original native launch may still finish and must remain associated with its original authority; view retirement cannot cancel it implicitly.

Preserve T02: restoring accepted intent does not recapture a new project lifetime; missing browser preparation is explicit and does not silently generate a replacement; retained storage is acknowledged only under the current continuation/link rules. An explicit new intent removes only the original key and dispatches only for its captured/currently valid opening. Refresh/reopen after acceptance observes the same run, never creates a second one.

## R5 — Partial read failure versus current denial

Start with accepted same-scope data, then fail one transient read. Keep unaffected accepted lanes usable and label the failed lane stale/error with a targeted read-only retry. Initial failure has no invented rows. A profile/project switch must not display old scope data as the new scope. A current authorization denial or deleted/recreated lifetime is not a transient stale-data permission: remove/refuse access according to the native owner.

## Test hygiene and source drift

T01 contains private-member access that may need adaptation after the seam moves. Preserve the exact semantic assertions, not the old field layout. T05 contains older selectors, fixed waits and DOM-triggered canvas interaction; reconcile current production readiness and add real pointer/keyboard coverage rather than weakening the test. New tests should use awaited bUnit events, the repository's async disposal helper and deterministic TaskCompletionSource/clock-controlled ordering rather than short sleeps.

The review also observed repeated injection declarations in the inspected shell header. Let the actual current Razor build establish whether this is relevant; do not claim a compiler failure from source appearance alone or make unrelated cleanup the main assignment.
