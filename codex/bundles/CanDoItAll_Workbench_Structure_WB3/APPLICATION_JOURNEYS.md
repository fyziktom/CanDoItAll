# Native and browser validation journeys

Read the [validation matrix](VALIDATION_MATRIX.md), [owner rules](NATIVE_OWNERS_AND_OUTCOMES.md)
and current repository testing instructions. These scenarios are required behavioral
coverage, not a quota of new test methods. Reuse actual current harnesses and extend
only the missing transitions. A sandbox echo is never native persistence evidence.

## Owned environment and evidence

Use an explicitly owned PostgreSQL 18 endpoint, unique databases and private artifact
root. Record its sanitized identity and server version. Do not use port 5032 or replay
setup/reset against WB1/WB2 or retained manual-provider fixtures. Linux SDK containers
that run process tests require the existing init behavior. Allocate adequate durable
storage and observe capacity; do not hide storage exhaustion by weakening assertions.

Use a dedicated source plus two clients where existing shared-provider consumers are
needed. Record application input fingerprint, Components/FileTools refs, immutable
image ID, actual container/data mounts and served owner assets. A later rebuilt image
is a new source observation, not permission to rewrite the earlier attempt ledger.
Reuse a correctly owned retained fixture only after verifying ownership and its
recorded inputs; otherwise create a new WB3 fixture. No real customer/provider keys.

Keep raw logs, TRX, transcripts, fixture keys and media private and ignored. Track only
sanitized summaries and cryptographic references. For each test, record the exact
command/filter, expected and actual discovery, execution counts and source/binary
fingerprints captured before the run. Preserve failed attempts and explain later
continuations explicitly. A readback of an already-committed original effect is not
a fresh full-path success; do not replay a mutation merely to obtain a greener report.

## J1 — actual graph authoring and round-trip

Create a disposable project through the existing Projects UI. Keep an unrelated project
and several untouched native nodes as controls. Open Structure using its ordinary route,
not a sandbox or direct fake component. Use the actual toolbox and shared composer to
create a note, block and representative typed node. Edit through the real context menu,
inspector integration and keyboard path. Include a node with unknown metadata extensions,
legitimate zero coordinates, fine decimal values, optional missing references and precise
UTC fields, prepared through supported native owners.

Confirm each accepted node ID and native readback, not just a toast or DOM label. A name
or notes change must preserve unedited fields, bindings, links and metadata extensions.
A deliberate type conversion is checked against its actual conversion policy rather
than asserted to preserve every obsolete subtype field. Invalid raw numeric/date/JSON
input must remain visible and prevent the wrong write; correcting a different field
must not silently accept old parsed values. Known missing/read-failed references are
not interchangeable. Task edits continue through the WB1 form, not the generic node
composer. System-managed projections remain read-only or navigate to their native owner.

Exercise single/group drag, pan/zoom, fit/focus, available collapse/expand behavior,
connect/reconnect/disconnect and the actual contextual command path. Verify exact native
positions and links, unchanged neighbors, cycles rejected and meaningful selection
preserved after the owner's readback. Inspect actual canvas output and the accessible
mirror: a row in the mirror alone does not prove the interactive graph updated.

## J2 — original editor and hierarchy operation ownership

Run WB3-H1 with the real dialog and a controlled native owner boundary. Hold A after
acceptance, close it, open B and settle A with success, known rejection or unconfirmed
acknowledgement. B's values, error, busy state, visibility and selected target must remain
unchanged. Separately inspect whether A's native hierarchy write committed. Cover close
without replacement, A-B-A, same-public-ID/new-lifetime, duplicate submit, an option
change during the operation and independent hosts.

Repeat the relevant lifetime transitions for block conversion and descendant transfer.
Do not assume a generic DTO test covers parent remounts, native modal cancellation or
callbacks emitted by the shared composer. A new deliberate retry can use only its own
freshly read expected state; it must not relabel an uncertain old attempt as rejected.

## J3 — clipboard and structural hierarchy

Use the real copy/cut/paste route, not only a helper method. Copy a selected forest and
verify the native old-to-new mapping, internal links and reported omitted boundary links.
Cut/move retains original node identities and obeys current graph constraints. Reject
projected/system-managed sources, forbidden projected destinations, cycles, unavailable
nodes and a stale project/surface lifetime. Preserve the existing unsupported Duplicate
response and same-surface clipboard limitation; no cross-project clipboard feature is
requested. Browser clipboard denial must be reported truthfully without a native mutation.

Use Add subproject/Reconnect parent with actual owners. Verify exact relationships and
all unaffected parents; prevent cycles and stale-lifetime replacement. Create-and-transfer
must retain the original target reservation/creation receipt and native transfer result.
Inject a post-creation or post-transfer follow-up failure where the owner already exposes
that test boundary. Confirm compensated-empty-child versus retained partial transfer,
original recovery identity and no duplicate creation from readback. Never delete a target
based only on its public ID or a guessed name.

Delete and cleanup presentation must preserve WB2's exact original prompt, disposition,
project lifetime and native cleanup/recovery owner. Cancellation of a dialog is not a
cancellation of a confirmed cleanup intent. Do not broaden the existing cleanup scope.

## J4 — state and two independent views

Two views must independently use toolbar modes, selections, composers, menus and floating
windows. Test direct supported tab URLs, back/forward, section switches and a failed graph
refresh. Preserve a coherent stale view with honest interactivity, rather than a current
heading over another project's data. Explicitly test same snapshot echoes versus a real
new project/lifetime and A-B-A navigation.

Hold a canvas-state save, produce a newer state, then let the original settle. Verify
ordered/coalesced behavior using the existing owner's policy, not arbitrary sleeps.
No view-state write may recreate data for a retired project or overwrite a successor
view. Accepted writes may finish after navigation only against their original target;
queued unsent writes obey the defined retired-view behavior. Confirm supported viewport,
selection, window geometry and grouping restoration from canonical readback. Do not
introduce a new cross-user preference model or persist mutable editor state in the URL.

## J5 — preserved Planning and Insights

From the new shell, use both WB1 task forms and verify exact schedule, effort, assignment,
execution/pricing basis and unchanged links. Use native Gantt bar and dependency gestures,
then Calendar and the existing exports to observe the accepted result. Run the inherited
quote eligibility and Gantt cleanup families when their owners/host composition change.

Open Manager Summary and verify that no report query occurs until the existing explicit
Load. Change draft options without Load and compare accepted metrics, cutoff and charts.
Open Activity and prove the accepted scope and pagination using real owner data, not
sandbox totals. Exercise WB2 Index/Signals/Selection actions from the newly composed canvas
and verify original target IDs. Health must not suddenly acquire a native Validate action:
WB2 documented that capability as unavailable. Unknown cost is not zero and a mixed-currency
plan is not silently converted to one total.

## J6 — governed Agent and file journey

Use an ordinary Agent configured through its real editor against the UI-created project.
Read an existing hidden canary whose content was never supplied in the prompt. Use the
real registered project/graph tools and exact approvals to create or update an allowed
node and create/attach a file. Check current tool schemas before scripted tool arguments.
Inspect the proposed project, node, path, overwrite policy and content before approving;
unexpected proposals are denied, not whitelisted to make the test proceed.

Verify native run/approval/effect identities, canonical file metadata, actual content and
actual downloaded bytes/hash. Deny a separate unauthorized or explicitly rejected action
and prove no effect on its target. Use a neighboring project/file as an unchanged control.
Refresh the real new canvas and existing Gantt/Calendar/Insights where relevant. The model's
textual claim of success is not an oracle. External model responses can be scripted, but
admission, registered tools, grants, native writes and content access cannot be faked.

## J7 — Workflow, Scheduler and History continuity

Use an existing supported saved Workflow/version with actual graph or file output. Prove
the accepted output and a known-incomplete response that cannot reach the asset writer.
Keep its separate preview/run identities and original project admission. Include a current
human-response or approval path if affected by the canvas composition.

Retain an enabled finite Scheduler plan through two actual restarts and verify its original
run/history plus a future neighbor firing once. Do not pause the completed plan to make
restart pass. If no Scheduler code or host input changed, a narrow current continuity case
is sufficient rather than repeating every historical timing experiment.

Observe the same native requests in the existing History and Usage surfaces. Metadata
visibility does not grant content access. Preserve scoped reads, denied content and exact
caller/source identifiers. A fresh incomplete derived Usage index is honest partial state;
use the existing explicit maintenance path only in the owned fixture and record byte-
identical canonical evidence. Do not seed fake report rows or erase failed native runs.

## J8 — shared providers and deferred integrations

On the final application image, use the published source models through both clients and
verify displayed source labels separately from opaque routing identifiers, including a
nondefault saved selection and no silent personal-provider fallback. Drive the real Agent
or Workflow consumer, not only test-chat API. Repeat the complete 19-vector protocol only
if actual protocol/driver/source or comparable host changes invalidate it; justify the
chosen current subset and never claim an earlier whole run happened on a later image.

Open and cancel the actual deferred file browser, governed preview, specialized task,
party and Workflow/Process entry points reachable from the new toolbar. Their normal
working composition must survive. Native integration checks are not claims that all
these specialized renderers were extracted. No unapproved runtime process, external
submission, paid inference or new functionality is authorized.
