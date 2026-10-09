# State, operation origin and honest outcomes

## One owner per lifetime

The routed host owns meaningful navigation. A selected record, an opened editor, an opened dialog and a pending operation are related but different lifetimes. The editor owns one draft and its validation state. Native owners own durable execution and business authority. Rendering-local focus/disclosure need not become a service.

Preserve the actual EditContext where one exists. Across a subtree unmount, retaining the data object alone does not retain raw invalid field text or validation messages. Keep the required draft/context above that boundary or reconstruct it without data loss under an explicit policy. Same-record tab changes and harmless parameter echoes must not silently reinitialize a dirty editor. A genuine switch uses the existing explicit discard/retain policy; do not invent autosave.

Do not put drafts, exceptions, secret content, CTS objects or authority tokens into route/query state. Opaque concurrency tokens are not ordered counters. Do not adopt a newer unrelated version token while retaining an older dirty draft, thereby authorizing an unintended overwrite.

## Adverse-ordering checklist

For each touched asynchronous operation capture the real target, database/profile/project lifetime when applicable, opening/session identity, submission/revision and relevant callback before awaiting. Fence **success, error, notifications, focus, finally, close and cleanup**. A→B→A must distinguish the two A openings. A local generation does not replace backend admission.

A cancellation-ignoring completion must not change the successor's data, busy state, warnings or dialog. Cancel owned reads on retirement; dispose each request's resources after that request has unwound. Detach old disposable state into local ownership before awaited cleanup; never read a shared field after the await and accidentally dispose its replacement. Observe owned task faults. Renderer dispatch is required for UI work but is not a global transaction lock.

The old operation may still finish durably. Closing a view does not implicitly cancel a process, workflow or accepted command. A successor can obtain its own local admission slot without allowing a stale finally to release that slot. Invoke only the callback belonging to the original interaction, and only while its presentation is still entitled to receive it.

## Read models

Use independent lanes when totals, lists, selected editor, graph history, files and capabilities are separate reads. A failed graph request must not erase a valid editor or declare an unrelated action unauthorized. Keep accepted same-scope data with an explicit stale/error indication after a transient refresh failure. Never retain another profile's data as current after a switch or current-denial result.

Preserve bounded windows, server-side paging and lazy reads. Do not load all history and filter locally. Rendering a snapshot or publishing contextual metadata should not issue another backend query. Missing, unavailable, empty and zero remain distinct.

## Native writes and reconciliation

Validate and capture a submission before dispatch. Enter, footer Save and alternate entry paths share the same admission and validation rules. A disabled button is not a concurrency/idempotency mechanism.

| Native evidence | Required UI behavior |
| --- | --- |
| Known refusal before commit | Keep draft; show precise safe reason; no success or unknown-commit claim |
| Known accepted/committed | Retain native ID, receipt and accepted revision before dependent refresh |
| Committed; read-back/secondary delivery failed | Keep the accepted identity and warning; retry only observation or the owner's documented continuation |
| Genuinely unknown outcome | Preserve recovery identity/draft and block blind replay until native reconciliation |

Use existing receipt shapes and actual stage knowledge. Do not fabricate a universal receipt in UI or infer rollback from any exception. For operations permitting editing while pending, reconcile against the submitted snapshot: keep subsequent field/row edits, adopt assigned identities correctly, and refresh only untouched data. For an intentionally locked editor, preserve that deliberate policy consistently rather than silently removing editing capability.

Tests must demonstrate the owner effect, not just a changed label. Assert dispatch count, exact target/lifetime, accepted identity, persisted state and whether a “retry” performs a read versus a second mutation. For create/launch/cancel, include remount, lost response, post-commit refresh failure and a new explicit user intent.

## PC1 review addendum: independent reads are not mutation retirement

A newer read of the same opening does not retire its pending write. Use distinct read sequencing and mutation ownership. Capture the actual scope/profile/project admission, opening identity, target identity, submitted values and native expected version. Serialize conflicting write intents at the native-host seam without freezing harmless typing or making another view share the slot. Preserve an accepted receipt before dependent observation. A late read cannot roll back an acknowledged revision; opaque tokens are compared for identity, never numerically ordered.

Retirement means the actual opening/authority changed, not that a refresh counter incremented. A stale result may be retained in its origin's bounded operation record, but must not mutate the successor or revive a closed editor. Ambiguous errors must not prompt blind replay of an Add, Import, Publish, Launch or cancellation intent.

An authoritative command can change selection: Add may assign a new role; Delete may select a surviving role or no role. Adopt that selection only for the matching active submission when the user has not since made another explicit selection. Clear the deleted draft and its validation/submission state. Do not stamp the response version onto the old selection and then short-circuit every later observation.

Capture every semantic field from the **actual edited row**, including fields not currently exposed by an input. The selected row in a server projection and a locally selected row may differ. Preserve hidden decision-role and other binding metadata in round trips.

For concurrent edits of nested rows, pair by stable identity and reconcile at the smallest meaningful field boundary; a whole-row local replacement can discard server normalization of an untouched field. An explicit discard, known rejection or retirement clears its submission record. Test same-ID reopenings and multiple command entry paths; a single mutable `submission` field is safe only when its matching mutation gate and origin lifecycle are proved.
