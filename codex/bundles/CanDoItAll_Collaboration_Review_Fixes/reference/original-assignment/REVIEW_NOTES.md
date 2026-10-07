# Collaboration review notes

This file records observations used to prepare the assignment, not executed defect reproduction. Read the current source before implementation. All fresh source observations refer to `7db3543ab437376baeca55089cb331fbe1b30483`; links and read coverage are in [SOURCES.md](SOURCES.md).

## Current ownership and extraction cut

| Surface / responsibility | Current owner | Intended disposition |
|---|---|---|
| `/collaboration`, `threadId` query, navigation/title, notification effects | `Pages/CollaborationHomePage.razor` + `.razor.cs` | Retain the routed host; make reads, command admission and lifetimes explicit. |
| Header/stats, Inbox/Threads/Escalations, quick create, detail/transcript, reply, empty/validation states | Same page; real BaseLib descendants | Extract into a lightweight feature renderer family reused by production and sandbox. |
| Read values, request values, annotated drafts | `CollaborationContracts.cs` inside implementation assembly | Move the suitable plain values into a new or equivalent lightweight contracts seam. |
| Six enums | `CollaborationModels.cs`, mixed with persistence | Extract only shared enums; retain exact numeric values and names. |
| Four EF records, mapping configuration, bounded context, transfer participant | Collaboration implementation | Retain ownership and schema behavior. No migration intended. |
| Query, manual create, reply, read state, automation ingress | `CollaborationService` partials | Retain application authority. Allow only the narrowly demonstrated post-commit observer repair and required seam adaptation. |
| Shell badge and service subscription | Production MainLayout | Preserve and test; do not render the production shell in the preview host. |
| BaseLib primitives / styling | Shared component family + product assets | Reuse real components and asset conventions. No broad sibling refactor. |

The reviewed markup contains forms inline, not modal editors. Participant count is rendered, but a new participant-management UI is not part of this task. `CollaborationThreadState.Closed` exists as a value; this does not authorize implementing thread close/reopen commands.

## Specific findings and treatment

### R1 — A light-looking contracts file is not a light assembly

`CollaborationContracts.cs` uses only ordinary types/annotations, but it is compiled into the same Razor project as the owner. That project references Infrastructure and SharedKernel. The six enums it consumes live in a file containing `IHasConcurrencyToken` and EF configurations. Moving only the page or making its service an interface would preserve the heavyweight assembly edge. Extract the actual shared values; do not relocate EF implementation under a `.Contracts` name. Sources C06/C07/C11.

### R2 — Read responses have no current lifetime fence

`LoadWorkspaceAsync` directly awaits `GetWorkspaceAsync(selectedThreadId)` and replaces both `workspace` and `selectedThreadId`. It does not pass a cancellation token, compare a generation, or guard disposal. Selection/tab/route handlers can await it independently. This is a source-level race opportunity, not a runtime trace captured in preparation. Introduce a small host-owned read lifetime and verify out-of-order completion, A→B→A and disposal. Sources C05/C08.

### R3 — Null selection is overloaded

The owner chooses the first inbox item/thread when the requested ID is null. The page also uses a nullable selection for filter realignment and default handling. If no unread candidate exists, sending null to this owner does not mean “show no selected filtered result.” Separately, a null route query can differ from an implicitly selected default and trigger another load on parameter delivery. Keep initial default, explicit unavailable ID and intentional no-selection distinct. The backend fallback itself need not be redesigned. Sources C05/C08.

### R4 — Reply state and completion are not bound to the rendered target

The page owns one readonly reply editor for all threads. A selection change does not visibly retire/reset that draft. After awaiting `AppendMessageAsync`, `AddReplyAsync` resets the shared reply editor and reads `workspace.SelectedThread` again; a concurrent target change can make those completion effects refer to a different current view. Capture submission/target/lifetime and ensure retired work cannot clear or redirect the successor. `MarkSelectedThreadAsReadAsync` has an analogous post-await target read. No claim is made that the backend request itself changes target after dispatch: the issue includes draft origin and later UI effects. Source C05.

### R5 — No explicit admission or edit-during-save policy

Create/reply controls and host handlers lack an explicit mutation admission flag/gate. `OnValidSubmit` provides validation, not duplicate-command admission. Form completion resets mutable data, so edits made while a write is awaiting could be discarded. Add the smallest appropriate gate and either disable that form during its write or reconcile the live draft against a captured submission. Maintain independent editor lifetimes. Sources C04/C05/C06.

### R6 — Persistence success is followed by fallible effects

The page displays success then reloads after owner success, without an explicit stale/refresh-warning outcome. The owner itself also calls `NotifyChanged()` after `SaveChangesAsync`; `Changed?.Invoke` can throw synchronously if a subscriber throws. That is a concrete source-level post-save exception path. Reproduce it with a real isolated owner and repair subscriber isolation narrowly. Preserve activity mirroring, which already catches its failures, and preserve caller-owned ambient transaction semantics. Do not broadly catch persistence failures and claim they committed; do not introduce a distributed receipt/idempotency subsystem. Sources C05/C09/C10.

### R7 — Existing concurrency token is not optimistic concurrency proof

The module README explicitly says current mappings do not enforce optimistic concurrency on these tokens. Keep token/schema behavior unchanged and do not fabricate a conflict scenario as if the owner supported it. Use actual validation/not-found/refused/unknown outcomes appropriate to the existing API. Source C12, corroborated by mappings C07.

### R8 — Badge integration is external to the feature renderer

MainLayout subscribes to `CollaborationService.Changed`, starts a shell-state load and unsubscribes during disposal. The existing component test verifies the unread badge with an owner-created record. Preserve this subscription/lifetime and test updates as well as initial display. A backend-free sandbox need not host MainLayout. Sources C14/C15/C16. The complete `MainLayout.DatabaseProfiles.cs` was also read: `LoadCollaborationShellStateAsync` directly awaits the shell query, updates the count and requests a render, without a local generation/cancellation/error guard. Trace the complete event-dispatch call chain on the execution checkout before deciding whether a narrow consumer-side repair is required; isolation of synchronous owner-event subscribers alone does not supervise an asynchronous subscriber task.

### R9 — Query scale is separate debt

`GetWorkspaceAsync` loads all inbox items, threads, messages and participants before projecting the workspace. That affects production data scaling, but is not the same as the build/watch graph problem. Do not quietly introduce pagination, transcript virtualization, caching, transport changes or schema changes as part of this UI cut. A realistic large sandbox scenario is still useful. Record scalability debt separately. Source C08.

## Compatibility anchors

Preserve the six enum families and their reviewed values:

| Enum | Values |
|---|---|
| `CollaborationInboxItemKind` | Notification=0, Escalation=1 |
| `CollaborationContextKind` | Manual=0, ProcessRun=1, ProcessLaunch=2, AutomationSignal=3 |
| `CollaborationMessageAuthorKind` | User=0, Agent=1, Role=2, System=3 |
| `CollaborationMessageKind` | Standard=0, Escalation=1, System=2 |
| `CollaborationParticipantKind` | User=0, Agent=1, Role=2, System=3 |
| `CollaborationThreadState` | Open=0, Closed=1 |

Reviewed table names: `Collaboration_Threads`, `Collaboration_Participants`, `Collaboration_Messages`, `Collaboration_InboxItems`. Their mapping configuration and records remain owned by the module. Preserve emitted links `/collaboration?threadId={id:D}` and literal route behavior. Assembly movement may need source-reference updates even with the namespace retained; investigate actual binary consumers instead of promising blanket binary compatibility.

Keep existing `data-testid` anchors where their semantics are unchanged. Examples include `collaboration-tabs`, `collaboration-filter-unread`, `collaboration-filter-all`, `collaboration-thread-create`, `collaboration-thread-title`, `collaboration-thread-message-item`, `collaboration-reply-submit`, `collaboration-mark-read` and `collaboration-open-context`. Add stable anchors for genuinely new states; do not change tests to assert a file/class count.

## Explicit non-goals

No new realtime messaging product, provider/LLM integration, API-only UI, auth/tenancy/per-user unread redesign, thread close/reopen feature, persistence migration, new concurrency protocol, pagination project, general event bus, full-shell extraction, new process/workflow runtime, or cross-module UI redesign. No global source/package switch and no package publication. Do not copy CRM/HR's complete framework for two small forms.

## What preparation did not verify

Preparation used connected GitHub reads and inspected the supplied shared bundle. Code search covers a default-branch revision and was used only to locate paths; relevant substantive files were fetched at the reviewed development commit. A bulk local source-download attempt failed because the environment could not resolve its host. There is no full local checkout or evaluated MSBuild graph from this preparation.

No application compilation, test discovery/execution, PostgreSQL integration, Playwright run, Code Analytics/Components MCP call, screenshot or watch benchmark was executed. The four integration facts and one badge fact are source observations only. General module ordering remains a scoped recommendation rather than a measured effort estimate. Package integrity checks are reported separately from all product validation.
