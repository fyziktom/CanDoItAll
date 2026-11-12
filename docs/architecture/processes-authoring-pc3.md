# Durable Processes authoring

PC3 replaces the five request-local authoring authorities with one scoped PostgreSQL
aggregate. A new native client scope reads the committed definition, and publication feeds
that exact semantic content into preparation, launch and child execution. The existing
[UI boundary](processes-ui-boundary.md) remains intact. Current execution evidence belongs
in the [PC3 validation record](../validation/processes-authoring-pc3.md); earlier PC1/PC2
reports remain historical.

## Owners and dependencies

| Owner | Responsibility | Source |
| --- | --- | --- |
| Application | Complete semantic envelope, captured observation, field-owned patches, catalog overlay, immutable executable resolution | `ProcessAuthoringWorkspace`, `ProcessAuthoringCodec`, five `*AuthoringAdapter` types, `ProcessExecutableDefinitionResolver` |
| Persistence | Head/revision, immutable publication, operation receipt and coordinated PostgreSQL transaction | `EfProcessAuthoringStore`, `ProcessAuthoringEntities`, `ProcessAuthoringMutationLock` |
| Processes module | Current profile/caller/project admission and ordinary native client scopes | `ProcessAuthoringAdmissionPolicy`, `ProcessWorkspaceProjectionClient`, module DI |
| Projections | Light observation/operation/value contracts | `ProcessAuthoringObservation`, editor command/projection records |
| Processes.UI | Rendering, raw drafts, selected identities, coherent read reconciliation | `ProcessAuthoringReadReconciler`, existing editor state and renderer families |
| Native shell | Opening retirement, asynchronous reads, submitted commands, receipts and recovery | `ProcessWorkspaceShell` |
| Canonical migration owner | Application-wide EF model and additive schema | `20261009124700_AddProcessAuthoring`, `AppDbContextModelSnapshot` |

Existing Application -> Templates/Projections and Persistence -> Application references
suffice. No project references or new contract assembly were introduced. UI and the sandbox
retain their light dependency closure; neither resolves an authoring store, EF context,
application service or module owner. No new runtime partial-class boundary was introduced.
The JSON source-generation contexts use the required generated partial form.

## Identity, storage and admission

The canonical address contains database profile, project, project lifetime and exact
case-sensitive definition key. Global scope uses two non-null empty GUIDs, so PostgreSQL
uniqueness cannot be bypassed with NULL project values. A database check requires complete
project/lifetime pairs. Existing catalog keys are canonical; alternate casing is rejected.

One transaction obtains the existing project mutation admission, the caller/operation
lock, any inherited-global lock and the local aggregate lock. It compares the expected
local revision and, for the first project override, its observed inherited revision.
Head, publication and receipt are then written together. A precommit failure rolls all
three back. A postcommit response failure leaves a recoverable receipt. Project retirement
and recreation participate in the same transaction gate; another profile or lifetime cannot
reuse a captured write or receipt.

The operation key is profile + caller + operation ID. Its fingerprint covers the immutable
submitted command. A matching retry returns the original result and committed timestamp;
a different payload or origin is refused. CAS conflicts have durable conflict receipts.
Validation/permission refusals before a commit remain explicit refusals, not saved changes.
The store contains no singleton dictionary or browser-storage authority.

`ProcessAuthoringWorkspace` caches reads only inside one native request scope so all five
projections see one snapshot. The adapters instantiate existing projection engines as
scratch normalization/validation mappers. Their historical dictionaries remain useful for
pure projection consumers and scenarios, but production writes go exclusively through the
aggregate store. The native canvas path also opens an ordinary scope.

## Complete meaning and command boundaries

The versioned envelope contains the existing typed template document, separately
materialized `JsonIgnore` execution guidance, effective role resources, base provenance,
visual reference placements, import history and frozen executable dependencies. A
source-generated codec rejects unsupported schema versions. It preserves supported hidden
governance, allowed operations, capability, workflow binding, artifacts, branches, child
outputs, forwarding, receipt policy and resource defaults.

Definition SaveDraft/Publish/Archive/Delete, role Save/Add/Delete/ApplyTemplate, step
Save/AddBranch/AddArtifact/MapSubprocess, all nine canvas commands and all three import
kinds use the same revision. Patches change their owned fields rather than reconstructing
execution meaning from form DTOs. Deleting a referenced role is refused; an unreferenced
last role can be deleted and its dialog/selection clears. A non-first step keeps its own
DecisionRoleKey. Partial child mappings are rejected instead of silently selecting defaults.

Canvas semantic actions create real steps, branches, bindings and artifacts. A visual
clone adds a reference placement without duplicating executable roles or steps. Final drag
coordinates and recomposition persist; zoom, selection and pointer frames do not. Imports
merge complete content into the captured target, allocate collision-safe keys, rewrite
internal links and store provenance/remaps. Unsupported driver-key collisions fail the
whole import. Replaying an import does not allocate another copy.

Catalog metadata overlays project, global and distributed defaults with truthful lifecycle,
counts and executable source. A deleted local override restores inheritance. FeedDefaults
is an idempotent read of existing defaults, not a destructive rewrite. The metadata query
uses the profile/scope index and excludes ContentJson, receipts and publication history.
Filtering/paging occurs after scoped metadata is loaded; the output is capped at 200 rows.
This avoids per-item JSON reads and N+1 queries but does not claim constant-memory database
pagination. Selected content is loaded separately. Measurements use 1,001 head rows.

A definition can have a draft and a launchable publication at the same time. The total
therefore comes from the complete All scope group, independently of those overlapping
status counts and the current page/search. Workspace, live dashboard, tab and agent-context
counts all use that same total, including visible archived definitions.

## Reads, drafts and recovery

Opaque version tokens carry an owner observation but are never lexically ordered. The
observation compares exact ownership, local revision and inherited revision. A newer
coherent read advances clean forms; dirty forms retain raw input and their original
baseline and show a conflict. A stale read cannot replace confirmed state. A receipt is
historical evidence, not permission to pin the view against a newer authoritative revision.
Lazy sibling reads still complete while a command is pending. A late older command result
can attach its receipt without rolling back a newer view or stamping a dirty draft with a
newer baseline.

An automatically resolved definition key becomes the opening's selected key. Renaming a
definition can reorder the catalog without switching the editor to a different definition
or retiring its pending operation.

Unknown outcomes retain the original operation locator and immutable submission. Explicit
status recovery checks the durable owner. A recorded outcome is replayed idempotently; an
absent outcome offers an explicit retry with the same ID and payload. Refresh and rendering
never repeat a write. Known acceptance survives failed read-back with a safe warning.
Profile/project/definition/disposal retirement fences success, errors, status callbacks and
cleanup against a successor opening. Mutating header actions wait for pending reads;
Refresh and nonmutating role/template browsing remain available. The open role dialog
offers AddRole while preserving one visible Add control; later role selection cannot be
replaced by the pending Add result. Template browsing and target choice can change during
an import, but the submitted command keeps its original target and query.

## Publication and exact execution

Publish validates with the existing template validators and kernel/compiler without running
a workflow or model. It creates an immutable content identity and captures a finite closure
of child definitions/resources (maximum 256 definitions). Draft edits retain the prior
published pointer. Selection uses project publication, then global publication, then the
unchanged distributed template. An archived selected override blocks a new launch; it does
not silently select a different definition. Delete restores inherited content for new work
while retaining publication and receipt history.

Native producers resolve this closure before launch-variable enrichment. An explicitly
resolved empty driver list stays empty. The same content feeds the kernel, readiness,
assignments, briefs and persisted `ProcessInstancePlan`. A prepared v1 admission remains v1
after v2 is published. Acceptance rechecks current authority, lifetime and archived state
under the coordinated transaction and aggregate locks, without silently recompiling the
reviewed plan. Already accepted runs retain their original identity and recovery path.
Delayed child dispatch uses the parent's captured child meaning, so child republishing does
not retarget it. The captured subprocess contract decoder remains unchanged.

Existing direct native project launches may omit the separate `ProjectAdmission` object.
Their executable closure still carries the exact profile, project and lifetime, which the
authoring owner revalidates under the real mutation gate. When an admission is present,
it must match that closure. Existing-run lookup checks the saved closure before reusing a
native run and refuses another project or a recreated lifetime. It does not invent an
admission for legacy child lineage or accept a project run with neither retained scope nor
an explicit admission.

Unchanged distributed definitions keep their legacy definition/version/content/artifact
identities. Publication-backed plans include the frozen publication/dependency identity in
their hash. Existing HTTP, standalone, Workbench and agent-tool producers enter this same
path; the native in-process UI adapter remains supported.

The Workbench launch dialog stays busy while it captures authority, the target and frozen
variables. Continue becomes available only after that context is installed; direct submit
callbacks obey the same guard. Closing or navigating can retire a pending opening, and its
late result cannot replace a reopened dialog or the next project's context.

## Migration, transfer and operational limits

The canonical migration adds three tables and their keys/indexes/checks. It neither creates
a parallel runtime schema nor rewrites existing workflow, plan, assignment or admission
payloads. The populated predecessor upgrade and idempotent reapply tests verify those bytes
and typed identities. See the [migration runbook](../../src/Foundation/CanDoItAll.Migrations.PostgreSql/README.md)
for deployment and rollback limits: stop old writers, back up, migrate, verify, then start
one compatible version. In-memory drafts from an old binary cannot be recovered by this
migration, and dropping the new tables after writes is not a lossless rollback.

All three new fact types participate in transfer residue/model coverage. Generic project
export/transfer and target restore are explicitly refused when retained authoring facts
would otherwise be lost, including global facts. Existing supported transfers remain
covered. This work does not add a new authoring archive/transfer format or purge retained
publication/receipt history.

The remaining native host owns orchestration, policy, files, chat, voice and project/run
lifetimes. These are intentional owners, not missed renderer extraction. Actual external
model availability, deployment and ordinary user database migration are outside PC3.
No sibling or shared-bundle change is required. The shared decision is `NO_CHANGE`.
