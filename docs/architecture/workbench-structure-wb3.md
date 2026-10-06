# Workbench Structure WB3

WB3 begins at main `bbd9e8de96dc7895abdecc04406766f7aea94c8e`, Components
`24d182c664d0b1f293098643e52caed7384a5d50` and FileTools
`3a080ecd31068a77c1e1bd639f7a78e21c93db85`. WB1 Planning and WB2 Insights remain
independent leaves. The supplied WB3 handoff is immutable input; execution evidence
lives in the ignored `artifacts/workbench-structure-wb3` directory.

## Responsibility and dependency decision

The route owns native reads, project admission, catalog and metadata composition,
graph mutations, layout persistence, runtime context and deferred integrations.
The new `CanDoItAll.Workbench.Structure.UI` Razor library will own the actual
Structure stage, canvas, accessibility mirror, toolbox, toolbar, generic editor
and structural dialog markup. Its independent sandbox will render those same
components. It must not reference Workbench, Projects, EF, Agent runtime or the app.
Presentation contracts are narrowly projected; the mixed Workbench model file is
not a candidate for wholesale movement. Existing Planning/Insights renderers are
composed through explicit slots, with their existing native owners and sessions.

The observed force is independent rendering and opening lifetime, not pluggable
business algorithms. A renderer with typed view data and commands is sufficient;
no service bag, universal repository, new transaction protocol or schema is needed.
New partial classes are not an extraction boundary. Existing native code-behind
may retain cohesive route orchestration while the real markup and local editor
state move to independently testable top-level types.

## Execution and proof selection

1. Reproduce WB3-H1 through actual rendered dialogs and gated native writes. Cover
   close/replacement, duplicate admission, original project lifetime, conversion,
   transfer receipts and opening reads. Repair these before moving renderers.
2. Extract the complete stage/toolbox/generic composer, with original submission
   identity, metadata preservation, native graph gestures and clipboard owners.
3. Extract structural hierarchy/conversion/transfer dialogs; preserve Projects
   editor return semantics and explicit deferred native slots.
4. Prove independent source and published Fast/Parity sandboxes at 1920 by 1080,
   two instances, dependency closure and measured edit loops. Preserve the known
   native CSS restart limitation without relabeling it as hot reload.
5. Execute current native operator, Agent, Workflow, Scheduler and multi-instance
   journeys. Freeze the final inputs before the single broad Stable run triggered
   by public contracts, project composition and admitted writer changes.

Focused tests use configuration `WB3` and a newly owned PostgreSQL 18 instance.
Production compilation precedes test discovery; discovery and actual execution
are recorded separately. Native effects and exact accepted IDs must be read back;
callback echoes cannot prove persistence. The shallow repair is falsified by an
old completion arriving after a new opening on the same target. Original mixed
results are retained, and reviewed follow-up attempts never rewrite their outcome.

## S0 checkpoint

The original dialog reproduction failed all three cases on the entry production
image. The original native hierarchy write succeeded after close, but its accepted
feedback was missing; reopening hierarchy or conversion let the original completion
close the successor. The repair captures each opening, original node and project
admissions, admits one submission, retains its typed outcome before readback and
fences publication to its original receiver. Delayed choices cannot restore a
retired opening. Native hierarchy writes validate all original participants, and
conversion refuses a source whose type changed.

Transfer retains the original creation receipt, compensation disposition and durable
partial-commit recovery. The nested Projects editor retains its acknowledged project
identity without restoring a successor draft. No schema, broad transaction protocol
or new effect owner was introduced. The existing native partial classes remain route
orchestration; they are not claimed as the forthcoming rendering boundary.

Proof is in `artifacts/workbench-structure-wb3/evidence.json`. The first repaired
three cases passed. The expanded attempt was mixed (18 passed, four failed): two
test event dispatches did not await editor opening, one recovery assertion used
incorrect JSON casing, and recreated-lifetime choices left an old dialog visible.
Those failures remain recorded. After repair and fresh production/test builds,
26 discovered native cases passed with no skips. Nine Calendar/Gantt and six WB2
Insights continuity cases passed separately. Builds used SDK 10.0.303, configuration
`WB3`, local sibling sources, zero production warnings/errors and 15 existing test
warnings. No broad Stable result is claimed at this intermediate checkpoint.

At 1920 by 1080/DPR1, the native browser created a project through Projects, opened
Structure, created a child through the nested Projects editor, returned with its
exact accepted identity selected and attached it. Native database readback confirms
both original lifetimes and the hierarchy edge. The private fixture owns a new
PostgreSQL database and storage/control-plane roots; no historical setup was replayed.

Portability enforcement passes 15,229 reviewed findings. Its four-entry delta removes
two obsolete message-prefix comparisons and updates the unchanged, intentional
case-insensitive project-name sorting expression. WB2's earlier qualified broad
result and native SDK scoped-CSS restart limitation remain unchanged.

Status: bounded lifetime repair verified; W1–W5 and final WB3 closure remain in progress.

## Rendering and generic authoring checkpoint in progress

Structure.UI now owns the actual stage, toolbar, standard blocks toolbox, generic
CanvasLib composer and hierarchy/conversion/transfer dialogs. Its five-project
source closure consists of neutral component dependencies; the independent sandbox
adds only its host. The route projects typed presentations and origin-bound intents,
and composes the existing Planning, Insights and deferred native integrations.
Public presentation names moved with compatibility type forwards. No mixed native
model file, semantic catalog, storage or runtime owner moved into the leaf.

The native reproduction confirmed that an original generic edit could overwrite a
node after native reclassification. CanvasLib now offers optional opening/close
notifications and carries the original opening ID on create and note-edit results.
Structure captures native admission and node/parent identity, refuses retired and
duplicate submissions, checks original node facts at the writer, and retains
accepted or unconfirmed outcomes independently of successor editors. The existing
close-on-submit policy remains. Other CanvasLib consumers retain their default
behavior.

The mapper preserves decimal precision, unknown envelope and nested metadata,
omitted duration, references and zero/negative canvas coordinates. Invalid typed
values are rejected explicitly. Browser timestamps display milliseconds; an
unchanged displayed value retains its original finer native precision. Chromium
rejects longer fractional values in `datetime-local`, so displaying seven digits
would silently clear a field. This constraint was verified in the actual browser.

The first extraction continuity attempt passed 40 of 41 native cases; its route
assertion queried dialogs during loading. After waiting for the replacement canvas,
the next 50-case native run passed with no skips. The initial generic reproduction
had five failures: one confirmed stale native write and four fixture subtype errors.
The repaired focused run includes valid fixtures. Shared callback tests passed 16
cases; four JavaScript cases cover invalid input and opening/submission ordering.
Browser proof retained the original invalid-number submission, then confirmed
blocking and correction. A receipt-label timeout occurred after the corrected
submission had succeeded; later readback observed that original receipt without
repeating the write. Raw draft/precision checks and final native journeys continue.

The expanded native attempt remains mixed: 92 of 95 passed. Conversion reconciliation
was using a full read instead of its accepted node; it now patches the original view.
Awaiting toolbar clicks did not repair the other two failures: the next 36-case run
passed 34 and exposed a real snapshot lag in the extracted tab content. A separate
renderer reproduction failed before moving the snapshot capture inside the active
tab fragment. Seven isolated cases and the three original native callers then passed.
The 95-case attempt lacks a pre-run hash receipt because binary enumeration failed;
the follow-up receipts include source and compiled DLL hashes. It is not retroactively
called a frozen or green run.

The independent source browser held A's submission, opened B, then released A. Its
accepted ID and precise inputs remained attached to A; B's draft and the second canvas
were untouched. The native application created and edited one note through the real
toolbox/context menu with the same row, node and parent identity confirmed by SQL.
The snapshot repair also restores immediate toolbox visibility. The 26 evaluated
dependency graphs retain all previously completed closures, with five projects for
Structure.UI and six for its sandbox. The new isolated suite is included in Components,
Stable and all three component CI selections.

Open-state inspection caught composer actions below the short sandbox canvas. The
shared layout now measures the actual host and toolbar, bounds the dialog card to
the remaining height, and keeps its existing internal scroll owner. At 1920 by 1080,
the repaired card spans y=360–880 within the y=258–898 host, with both actions visible.
Five shared runtime cases and asset verification pass. The sandbox closes its toolbox
when opening the editor, matching the native host. Portability enforcement passes
15,241 reviewed findings. Scoped proposed-source secret review found only unchanged
CI fixture credentials in diff context; no added credentials or Components findings.
The shared checkpoint is Components `49decea8ffc057dccf086a572959e8c764967d2e`,
verified with the existing PGP key. S0 is main `d45d56a42`; no push was performed.

W3 graph/clipboard hardening and W5 final-image closure remain outstanding. These
intermediate observations are not the final WB3 acceptance result.

## Graph and hierarchy checkpoint

The native graph owners now validate captured root/destination row identities and
kinds inside their existing admitted transaction. Copy retains its complete ID map
and omitted boundary links; Cut retains original identities. Clipboard replacement,
duplicate submissions and uncertain acknowledgements cannot reuse the old buffer.
Connections retain the accepted native link ID, and deletion refuses a replacement
link with identical endpoints. Movement captures original positions before waiting;
viewport saves serialize accepted writes and retire unsent work on navigation.
Reparenting and recomposition retain original outcomes and fence successor selection.
Transfer validates the original root snapshot and preserves its native child creation,
compensation and partial-recovery facts. No new durable protocol or schema was added.

Three original clipboard cases failed before repair. Fourteen focused graph cases
and 16 existing callers then passed. A separate native transfer reproduction failed
because a changed root was accepted; its repair passed in the 39-case owner run.
Ten integration cases passed canonical copy metadata/bindings/link policy, cut,
recomposition and transfer. The source/binary receipt for those final owner runs is
`w3-owner-final-inputs.json` (SHA-256
`d9a295fe38bc1c1ec586184b095a8254310de11a9d7108f7f9a68c7bca98045d`).

Real browser Copy committed its two-node forest but exposed a new publication bug:
the page advanced its own selection revision before evaluating its readback fence.
The native mapping and metadata were retained; that effect was read back without
repeating Copy. The fence now captures the revision after its own selection update,
with an additional assertion against the actual CanvasWorkbench presentation.
This browser failure remains separate from the earlier passing owner tests.

The sandbox supplies local graph selection/movement, dependency and clipboard
outcomes in addition to its real composer and structural dialogs. Source and
independent publication builds for both Fast and Parity succeeded without native
services. Browser publication, repeated watch, final native consumers and the broad
frozen checkpoint are still required before closure. The earlier main rendering
checkpoint is `5c6b8a998511041729f8e69b4123d9250db52fc2`, with a verified PGP signature.
