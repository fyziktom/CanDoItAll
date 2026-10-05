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
