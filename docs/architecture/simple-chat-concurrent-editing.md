# Simple Chat concurrent editing

Reusable definitions and conversation transcripts have separate owners. Definition
updates and lifecycle commands require their original native concurrency token.
The PostgreSQL repository conditionally replaces that version and appends a new
immutable configuration revision. It does not adopt a freshly read token for an old
form. Conversation append, dispatch, cancellation and recovery retain their existing
operation and transcript protocols.

The main definition editor keeps one original actor and database activation grant.
Protected reads validate that grant before and after awaiting data. Writes carry it
through the application boundary into the native repository and the transaction's
precommit check. A grant or dataset change after SQL executes rolls the transaction
back. Retired or foreign openings cannot use a newly authorized actor to finish an
older command. The pure rendering library references the existing Editing UI leaf;
it has no persistence, concrete module or Web dependency.

Original, local and current values are compared explicitly. Applying the comparison
changes the local draft and its original; a separate Save performs the conditional
write. Provider/model/effort/parameters and the output schema are cohesive choices.
Text fields use the existing bounded text comparison. One bounded pre-comparison
snapshot supports a local Undo; restoring it requires another current-state review
before Save and never performs a server inverse. Later input, hints or a command
invalidate that Undo's applicability. Unfinished numeric input,
pending tag text and invalid JSON stay local. The existing dialog and client tab
panels retain the same field components. Closing or navigating with unfinished
changes requires explicit discard.

Save acknowledgment compares submitted, current local and exact accepted values.
Typing during the await remains in the editor for another save. A later peer read
cannot replace the accepted original. The native unit of work captures its accepted
result immediately after primary commit; secondary failures carry
`LlmChatCommittedException<T>`. All notification subscribers are attempted. The
UI shows a saved warning and HTTP definition and conversation metadata writes preserve their normal payload
and ETag with `CDA-Llm-Chat-Outcome: committed-reconciliation-pending`.

An unconfirmed response remains distinct from a conflict or a known commit. It
blocks another submission. Existing definitions can read and explicitly acknowledge
current state before starting a new conditional save. Matching values do not establish
the earlier outcome. An unconfirmed create remains blocked; the operator must inspect
the catalog before explicitly discarding that local draft. Refresh never repeats it.
The separate native create-receipt protocol remains available to its existing API and
managed-tool callers.

The native definition read captures its definition, immutable revision and tags in
one repeatable-read snapshot when it owns the read transaction. This prevents a
comparison from combining an older definition with newer tags.

Committed definition changes publish only targeted identity metadata through the
existing instance feed. A hint invalidates a comparison without reading the body.
Current-state reads also fence hints and input arriving during the await. Own hints
do not invalidate the editor's acknowledgment.

The definition catalog uses explicit refresh after a metadata hint (`DEFERRED-LIVE`).
Its existing keyset query materializes full current revisions and tags, and refreshing
several loaded pages would replace the operator's current result set. Hints perform
no body query. Refresh restarts the bounded page with the same filters and preserves
the separately owned editor and conversation. Only a covering read acknowledges the
captured hints. There is no new background polling loop or transcript-wide merge.

Conversation metadata keeps the title, metadata token and transcript revision from
the dialog's original opening. Refreshing the transcript never adopts a newer token
for that dialog. A targeted metadata hint preserves the title and composer, invalidates
an older comparison and offers an explicit current-state read. Title comparison uses
the original, local and current values. Apply and bounded Undo change only the local
draft; Save or Archive remains a separate native conditional command. A review that
arrives after another hint or input change cannot become the new original.

The conversation workspace keeps original actor and dataset access for its lifetime.
Read, metadata management, turn execution and managed recovery remain separate rights.
Native admission distinguishes definition writes, conversation metadata and operation
commands, checks the matching original resource before SQL and revalidates before
commit. An authority change hides the protected view and retires its observers.

Message admission keeps its existing durable operation identity and transcript
precondition. The workspace clears only the submitted composer revision; later typing
stays local. Selecting the current thread is a no-op. Switching threads or navigating
with unfinished input requires explicit discard. Stream and transcript refresh do not
replace the composer or a separately open metadata draft. The bounded conversation
catalog uses explicit refresh after a hint and acknowledges only the changes its read
covered. It preserves the selected conversation and search input.

Definition and conversation views load on first use and retain their owners afterward.
The conversation does not read or expose editable fields during prerender; input starts
only when its interactive owner exists. Parent Agents tab navigation waits for navigation
admission before applying the new route, so removing the child cannot bypass its guard.
Switching between those supporting views keeps the conversation selection and composer.
The main module identifies the same retained route; navigation elsewhere still requires
discard and reveals the conversation's guard when its view was hidden. The main shell
and tabs constrain their content through the existing full-height parameters, so the
transcript owns its scroll and the composer remains visible without a viewport offset.
Native rename during an active turn remains a known rejection; the metadata transaction
rolls back and the title draft stays available after the response completes.
Its refusal belongs to the metadata dialog and remains visible through transcript refresh.

Floating Simple Chats opt into mounted content while hidden or unfocused. Reopening or
focusing another conversation preserves the same editor and unsent composer. Hidden dirty
editors reveal their own window when they block navigation. Access retirement removes the
protected windows and catalogs. The shared shell owns visibility and geometry only; it
never stores or reconstructs a feature's draft.
The conversation-window wrapper hides its root without disposing its body. A fresh focus
request restores a minimized window while retaining its geometry and editor.

The floating catalog retains original actor and dataset access. Its command session
serializes create/archive admission, preserves exact accepted identities and warnings,
and blocks blind replay after an unknown result. Explicit bounded history review and
acknowledgment permit a separate command; they never prove the earlier commit. Hints only
mark the catalog stale. Its visible refresh updates rendered metadata and action versions
without changing an open editor's original. A queued action retains the version clicked
by the operator. Focused reload acknowledges its captured notice after a successful read;
it does not acknowledge an unread main conversation catalog.

Known admission commits retain their operation identity when postcommit signaling or
readback fails. The UI follows that existing operation, and HTTP preserves the normal
accepted response with the outcome header. The warning never repeats provider dispatch.
Unconfirmed conversation creation stays blocked until explicit discard after catalog
inspection. Unconfirmed title or archive commands require a current-state review and
explicit acknowledgement before a new conditional command; matching current values do
not prove the earlier outcome.

Undo restores only the previous local draft. The reviewed current definition or title
remains the original for the next comparison; Undo cannot restore an older write token.

Focused regressions are in `SimpleChatConcurrentEditingTests`,
`DefinitionCatalogSessionTests`, `LlmChatTransactionalConcurrencyIntegrationTests`,
`LlmChatDefinitionUiGatewayTests` and `SimpleChatKnownCommitApiTests`. Conversation
coverage includes `LlmChatConversationEditingTests`, workspace regressions and the original
conversation permissions in `EditingUiAccessTests`. Browser validation
uses independent Chromium and Firefox sessions against the real Blazor host and native
database, including normal, comparison and close-confirmation views.
