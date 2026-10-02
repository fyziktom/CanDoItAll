# State, effects and operation identity

| Fact | Owner |
|---|---|
| Provider/source selection and active host slot | Existing product host, distinct from nested editor |
| Local alias/enablement/source draft and validation | Original editor instance with its own baseline |
| Remote catalog/models and accepted revision | Read snapshot, not a mutable local form |
| Confirmation | Exact target, expected versions, operation and current dialog lifetime |
| Native commit, unresolved attempt and delivery ledger | Existing management/recovery owners |
| Read cancellation, focus and presentation state | Owning operation/view; not a global singleton |

Use read lanes for source list, current Sharing state and catalog discovery when they have distinct
lifetimes. Capture IDs/context before awaiting. After waits fence successful values, errors,
notifications, busy cleanup and callbacks. Retiring the view is not undoing a confirmed write;
clearing one dialog is not clearing sibling dialogs. Clean up only the actual owned instance.

PP2C-R1 adds an explicit local draft baseline. It must be maintained separately from public remote
models and source-health metadata. Do not adopt arbitrary fresh optimistic tokens when retaining
older writable values. A source setting change can affect multiple imports; this does not give
one import editor permission to overwrite local configuration in another.

Capture complete source requests, selected publication sets and expected source/import/provider
versions before dispatch. Prevent duplicate Enter/Save/alternate actions inside handlers. Input
validation failures are not unknown writes. Native commit notifications and callbacks can fail
after persistence; keep the confirmed identity/stage and retry only pending work. `Recovery.RecordCommit`
and explicit parent delivery must not become a toast-only success path.

A discovery result is not an imported write. Selection is not authority to use every model or
all future publications. Source refresh must not silently reactivate retired imports or add
unselected publications. Disabled, Missing, Unpublished, SourceOffline and identity mismatch are
separate states; preserve exact existing policies and no-fallback behavior.

No credentials in generic command histories, URLs, protected fixtures committed to Git or public
screenshots. Use source/secret metadata and error classes safe for operators. Keep native URL/TLS/
network validation; never introduce `AllowAll`, certificate bypass, broader scopes or read-only
presentation claims for actual mutating diagnostics.

On completion demonstrate successful/error late results, A-B-A, same-target echoes, unknown outcome,
confirmed-write + failed delivery, repeat review without replay and real owner conflict. A second
view may keep its own local draft but cannot inherit another view's successful commit token.
