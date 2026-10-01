# Source review for A1

References below point into [the source register](SOURCES.md). This is a scoped implementation map,
not a claim that every remaining Agent/Workflow source file was line-by-line audited.

## Current host and consumer behavior

`AgentDetailsDialog.razor` contains the real ten-section editor. Its loading/error branch protects
the editable form; the form uses the session's existing EditContext. The shell/footer is shared by
core and access/capability sections. These sections are not separate durable agent records. [R11]

`AgentDetailsDialog.razor.cs` owns reads/commands, provider/reference lists, confirmations, session
replacement and target/section callbacks. Read ranges 1–840 include acquisition, Save/reconcile,
Delete, Reset and capability effects. Remaining helper methods, all actual call sites, CSS/imports
and nested components require the implementation entry census. Current default-branch code search
is only a path-finding aid; fetch the current branch before relying on content. [R12]

Do not replace the whole host blindly. Preserve embedded and DialogService consumers, TargetChanged
and Saved behavior, section route mappings, CRM-linked resources, two simultaneous editors and
initial provider hints. The session's target/create identity is distinct from catalog selection.

## Existing safeguards worth retaining

- `AgentEditorSession` owns target, draft, EditContext, cancellation, PendingRefresh,
  HasUnconfirmedWrite and commit warning. Section change is not session replacement. [R13]
- `AgentEditorDraftPolicy.Capture` creates a detached request, preserves Favorite separately,
  normalizes model/default and image policy, and deep-copies all deferred settings. [R14]
- `AgentEditorCommands` distinguishes concurrency/validation refusal, confirmed save (including
  CRM projection warning) and unconfirmed result. A previous confirmed ID is not lost merely
  because list reconciliation fails. Actual external-root normalization remains owner work. [R15]
- Deferred project list is explicit/lazy; reference failure does not erase unrelated edits.
  Managed agents cannot be deleted through either presentation or actual owner. [R12, R19, R20]
- Existing integration test persists all major settings and arbitrary extension JSON, then
  rejects a stale update. This is mandatory compatibility proof, not replaceable with a fake
  echo in the new sandbox. [R20]

## Dependency pinch points

| Child / data | Correct direction for A1 |
|---|---|
| Conversation identity/provider controls | Reuse genuine neutral Conversations widgets and safe provider projection |
| ProviderModelSelector facade | Map existing default/custom semantics into ConversationProviderModelSelector; do not import broad Core/Voice/Canvas assembly |
| AgentThinkingEffortSettings | Keep Supported/Unsupported/Unknown, configured-unavailable override, Provider default versus explicit None, boolean label semantics and source-managed guidance; preserve actual policy |
| AvatarPicker | Real host-owned acquisition/generation control remains in a typed identity action slot; actual core avatar display moves |
| SharedProviderRefreshButton | Real source-owned refresh and reconciliation stay host-owned; stale delivery cannot update another provider/editor |
| Storage selection / external roots / Memory / capabilities | Remain deferred production integrations with original owner lifetime; no new dependencies in A1 leaf |
| linked CRM resource | Safe link/display data or typed host navigation; not an injected CRM query in renderer |

Effort is not merely a list of labels. The current control retains an incompatible stored override
so the operator can deliberately remove it, and reports a bad provider default separately. Preserve
these semantics without inventing support or silently selecting another model. [R16–R18]

## Required risk controls, not unverified new bug claims

The reviewed host has extensive earlier safeguards. The following are mandatory extraction tests,
not assertions that every listed failure currently occurs:

1. Latest metadata and provider/model intent: delayed references or source refresh must not rebind
   a newer explicit selection, and default/override meaning stays exact.
2. Whole-draft save: core editing must preserve deferred access lists, unknown config and nested
   settings, including later edits made in a deferred section during an accepted write.
3. Shared footer/admission: capability-triggered save, Enter and footer Save cannot create competing
   new write lanes. Known warning, uncertain result and read-only retry remain distinct.
4. Original lifecycle: create/clear, close/reopen, original target/version and late confirmation or
   avatar/refresh result cannot target another live editor. No global CloseAll.
5. Current invariants: round-trip test, native managed delete refusal, authorization negatives and
   redaction tests remain. Do not replace assertions with UI-only success messages. [R19–R21]

Where existing reconciliation intentionally keeps the entire newer draft and updates only its
version, do not casually replace it with a generic merge engine. If A1 changes reconciliation,
prove accepted defaults/normalization plus unchanged/newer fields and concurrent-owner behavior.
A substantial multi-writer protocol question gets a separate causal map, not an incidental redesign.
