# WC-C2: shared Dialog interop cancellation escapes retirement

Status: OPEN. Priority P2 for the application regression gate. This finding belongs to
the shared Components dependency and its consumer validation, not Collaboration's owner
or a new Workspace extraction.

## Reproduction and observed state

The full browser run at checkpoint
`d6bdaa1ff6257c1dc541849025baa70212699ebecef63677a635ffe9ba94510b` retained a renderer
and circuit failure caused by `TaskCanceledException` during shared Dialog disposal.
`CollaborationBrowserTests.Production_create_reply_mark_read_filter_deep_link_and_context_use_real_persistence`
completed its owner assertions and then rejected `Unhandled exception` in the shared
host log. The original error is retained; the assertion was not weakened.

The campaign used entry commit `1080c24163afd3cf65fcc68756913c5dd3262a62`, private
PostgreSQL 18 and WsCompletionProof. Components remained at
`f258ab6a959a97fa16c01d0858e7dc122728a11a`, clean, with the same evaluated source graph.
There is no equivalent historical execution, and the shared log lacks per-entry UTC
timestamps identifying the exact preceding navigation that retired the dialog.

The concrete stack is `Dialog.DisposeAsync` (Dialog.razor, line 284) →
`DialogInterop.CloseAsync` (DialogInterop.cs, line 49) →
`JSObjectReferenceExtensions.InvokeVoidAsync` → `JSRuntime.InvokeAsync`, which throws
`TaskCanceledException`. RemoteRenderer then reports an unhandled disposal error and
CircuitHost reports a circuit exception. No wrong-target mutation or data loss was
observed. Failed teardown may also bypass subsequent module/reference cleanup.

## Boundary and attribution

The application owns whether a dialog is rendered. BaseLib owns its browser registration,
module reference and .NET callback reference. `DialogInterop.CloseAsync` handles a
`JSDisconnectedException`, but cancellation is a distinct exception. `Dialog.DisposeAsync`
sets its retired flag, then awaits close before releasing the other references. There
is no `finally` protecting that cleanup if close throws.

The stack confirms the shared component failure path. Whether the cancellation came
from circuit retirement, a pending JS import/close race or an interop timeout still needs
a controlled reproducer. Attribution to Workspace extraction is **unknown**. A source
blame excerpt is retained in `dialog-retirement-origin.txt`; source provenance alone is
not a before/after runtime comparison.

Affected groups: APP-01/APP-15 shared route/dialog lifetime and GATE-02. APP-10's final
collection-log check detected the failure; a fresh isolated feature rerun cannot close
the broader lifetime finding by itself.

## Follow-up boundary

Use a Components repair bundle with an explicitly pinned consumer update. First hold or
cancel the actual JS close during component disposal and distinguish that from a live
explicit close request. Verify that module and .NET callback cleanup always completes,
that late imports cannot reopen a retired dialog, and that real unexpected interop errors
remain observable. Do not add a blanket cancellation catch to all active dialog operations.

Exercise repeated disposal, disconnected circuits, cancelled close, pending import and
an unrelated dialog remaining usable. Validate reusable BaseLib at small, medium and large
viewports, then repeat actual consumer navigation and the full affected browser/shared
component gates. Review the service/asset graph and dependency pin before consuming the
fix. A local sibling-only patch would not deliver the same implementation to pinned CI,
so no such patch was applied during this frozen campaign.

Original evidence under `artifacts/workspace-completion/20260930-1080c2416` includes
`shared-shell-collaboration-original.log`, `frozen-browsers-01/run.log`, its TRX and source
manifest, the unchanged sibling manifest and the blame excerpt. The campaign ledger
hashes these records. The full log assertion and the dependency remain unchanged.

The unchanged Collaboration production journey passed in `campaign-collaboration-isolated`,
a fresh process/private host, including the full log assertion. That is scoped feature
proof; it does not reproduce the earlier interop cancellation or close WC-C2.

The later complete 143-case `frozen-browsers-02-closure` run reproduced the same shared
Dialog cancellation path. APP-10's final result is therefore FAILED, superseding its
earlier isolated pass without discarding that attempt. `shared-shell-closure-original.log`
retains the new failure at checkpoint
`15e26ac35fdd97478587e21af118fe66fce53bd6b019af9b11053810499b4a3c`;
SHA-256: `c36b3169687c3a7be4f9cb8e50784108d7e6709cedbe144861d8f6658a3f1a78`.
WC-C1 and WC-C3 occur in the same retained log. The sibling dependency remains unchanged.
