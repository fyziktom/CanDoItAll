# CanDoItAll.Modules.Workbench

## Purpose

Product module for workbench views, projections, canvas state, and user workspace orchestration.

## Project Type

- SDK: `Microsoft.NET.Sdk.Razor`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Modules/CanDoItAll.Modules.Workbench/CanDoItAll.Modules.Workbench.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Modules.Workbench.csproj](CanDoItAll.Modules.Workbench.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

This module owns product semantics for its bounded area. Keep business behavior here and expose it through typed services, Razor components, and module contracts. UI and transport adapters should call into these services instead of duplicating module logic.

Processes.Application owns process-run root semantics through `ProcessRunArtifactRootPolicy`. Workbench consumes its typed resolution when projecting current-run managed roots, collapses artifact evidence under `artifacts/.../process-runs/{runId}` to the run artifact folder, and collapses generated or external-delivery output persisted under `output/.../process-runs/{runId}/{productRoot}` to the product folder. Wrong-run, dated receipt, absolute, traversal, or otherwise unanchored paths are ignored instead of mirroring noisy artifact subtrees. Raw `external-target/...` aliases remain Processes grounding metadata; Workbench projects the managed output root that records the run-owned delivery evidence.

### Runtime node execution and terminal presentation

Project Structure runtime nodes compile to typed executable, argument, environment,
working-directory, and target values. Direct execution uses the owned workspace process
host and does not require a terminal. PowerShell and POSIX shell nodes remain explicit
script modes; they are not fallbacks for .NET, Docker, Python, Node, or other ordinary
runtime nodes. POSIX runtime nodes invoke `sh` on Linux and macOS and are unsupported on
Windows. Separately, AgentFramework file-skill `.sh` scripts invoke `bash`; Bash is a
skill-script dependency, not a Workbench runtime fallback.

Interactive terminal presentation is optional. Windows enables its PowerShell
presentation adapter by default. Linux and macOS require an explicit executable and
argument prefix under `Workbench:RuntimePresentation`; a headless host can leave these
values empty without preventing startup or direct execution. For example:

```json
{
  "Workbench": {
    "RuntimePresentation": {
      "EnableWindowsTerminal": true,
      "LinuxTerminalExecutable": "/usr/bin/x-terminal-emulator",
      "LinuxTerminalArgumentPrefix": ["-e"],
      "MacOsTerminalExecutable": "",
      "MacOsTerminalArgumentPrefix": []
    }
  }
}
```

The configured prefix must make the selected terminal treat the following typed
runtime executable and arguments as its child command. Linux/macOS elevation remains
unavailable by default; Workbench does not add `sudo`, `pkexec`, AppleScript, or a
password-prompt fallback. Windows elevated launch is a separate, explicit `runas`
capability.

### Project Structure Agent Invocation Snapshot

The ready Project Structure chat-context provider publishes a typed
`ProjectStructureInvocationSnapshot` copied from the surface already loaded by the UI.
It retains no component, tracked entity, service, or mutable domain object. The
snapshot is bounded to 512 nodes and 1,024 links, includes explicit coverage and
omissions, carries database-profile generation plus deterministic fingerprints, and
expires after five minutes.

`ProjectStructureReadRequest.Source` is a typed three-way policy:

- `ContextDefault` selects the invocation snapshot only for eligible interactive
  Project Structure chat and otherwise selects canonical current data.
- `InvocationSnapshot` requires that exact held snapshot and fails closed on
  context/scope/project/profile/freshness/fingerprint/coverage mismatch.
- `CanonicalCurrent` performs the canonical service read.

There is no silent snapshot-to-database fallback. Governed process execution and
non-Project Structure contexts use canonical data. Snapshot reads are read-only
context; all mutations still pass through current canonical authorization and
concurrency checks.

Resource and TestLab projections and node-scope checks consume owner-provided facts.
Resources resolves connector kind/subtype before returning those facts. Workbench owns
node keys, bindings, layout, and scope comparison; it does not read those owners' EF
records or configuration JSON. The shared projection context and other contributor,
lifecycle, and transaction seams remain separate boundary work.

During relational Workbench mutations, each assembly operation enters the supplied
context's active transaction and selects explicit coordinated owner-query methods.
Those queries share the caller's connection, transaction, and snapshot. Participation
ends before the assembly operation returns; the caller retains commit ownership.
Ordinary owner queries and InMemory assembly reads remain independent. InMemory does
not provide transactional atomicity.

## Assignment persistence boundary

`WorkbenchDbContext` explicitly maps the ten existing Workbench record types, including
canonical ProjectObjects, bindings, references, lifecycle and cross-module mutation
records. Runtime consumers include the direct-assignment revision writer, project
deletion participant, and complete mutation-processor claim/checkpoint/heartbeat path.
Other Workbench mutation and projection paths remain dependent cutovers. The complete
AppDbContext continues to own schema initialization and migrations.

The Projects-owned assignment mutation bridge carries final typed assignment facts,
the canonical node reference, and the expected revision. Workbench opens an explicit
enlisted owner context, preserves the existing managed-binding lock and pricing rules,
saves its object/binding/reference changes, then disposes without committing. CRM owns
the enclosing transaction and final save. Historical unknown top-level metadata is
retained by the existing preserving serializer. Task identity remains the existing
ProjectObject ID and project/node key; display metadata is not a second assignment
source of truth.

## Project deletion ownership

The project deletion participant saves Workbench records through an explicitly
enlisted WorkbenchDbContext during Projects' authoritative transaction. It saves even
when a completed recovery needs only residual view/layout cleanup and returns no
preparation. Residual object recovery acquires both the project and managed-binding
keys before planning and removing bindings, preserving the original and any required
follow-up recovery IDs.

Storage planning consumes owner catalog facts and explicitly enlists those database
reads in the supplied mutation transaction. FTP configuration is inspected only for
referenced rows in the reached planning phase. Physical deletion retains a separate
postcommit Serializable binding gate across provenance/liveness validation and the
driver call. Storage owns the current catalog record and driver; its bounded callback
provides typed facts to Workbench. Normal postcommit reads use independent factories.
The fact query follows binding-ID collection, so these independent reads are not a
new atomic catalog snapshot. Existing record-based provenance helpers remain explicit
compatibility adapters for creation/transfer callers that have not yet migrated.

Workbench runtime services use the explicit fifteen-record `WorkbenchDbContext`: the
existing ten records, Workflow contribution receipts and admissions, Work assignments,
immutable imported assignment history, and retained Process asset contributions. The pooled
factory remains bound to the immutable canonical host profile. Complete migrations
remain the only schema authority; selected profile transfer uses explicit owner sessions.

Structure contributors receive graph data without a caller context. Projects owns
bounded hierarchy and phase facts, including the reached descendant closure and its
external parents; unrelated projects are never materialized. Limits are explicit:
2048 reached/related projects, 8192 links and 1024 phases. Exceeding a limit fails the
projection without returning a partial graph. Prompt bindings and projection fields
come from Prompts; Process runtime and completed-record facts come from Processes.
Historical Process assignment discovery retains its existing JSON scope filter and
now rejects more than 4096 matching candidates; runtime ID batches have the same cap.
The existing 1000 completed-root history cap and warning remain unchanged.

Mutation planning explicitly enlists these reads in the native serializable
transaction. Normal query methods always create independent owner contexts. Prompt
creation stages its Gallery record and native binding under that same transaction,
then completes search/activity follow-up after releasing the transaction and its
coordination scope. A lost commit acknowledgement preserves the existing ordinary
create exception; ordinary creation does not acquire Workflow intent replay semantics.

Workflow contribution receipts and admissions are retained across ordinary project
and node deletion. Profile transfer and explicit purge must preserve or deliberately
account for that evidence. This ownership cutover does not establish admission for
all project-attributed writers; the separate lifetime/admission boundary remains
required. Storage reconciliation and Workflow launch authority retain their own
explicit pending/conflict/reconciliation dispositions.

## Work assignment ownership

`ProjectWorkAssignmentService` owns direct `WorkItemAssignee` rows and stages the
existing canonical work-item revision/display/pricing update on the same database
transaction. `ProjectObjectRecord` remains the sole writable native task identity;
no task table is duplicated. Ordinary fact queries use an independent Workbench
factory. Explicit staged commands require the caller's coordinated transaction and
ordered project/assignment identity locks. Preallocated IDs preserve the original
global assignment identity constraint across the two owner tables.

CRM owns Party/affiliation validation, participation roles, capacity and rates. The
canonical schema retains the affiliation Restrict FK for each assignment table;
the Workbench runtime model contains only its scalar reference and indexes. Work
records retain IDs, phase/opportunity, allocation, UTC intervals, primary flags,
source and notes, including unavailable historical references. Existing task edit
compensation across assignment and later pricing commits remains unchanged.

The existing twelve-collection Projects package does not export assignment rows.
Native task metadata still travels with Objects. Work assignment residue therefore
blocks a nonempty target as it did when stored in CRM; this cut does not add a new
assignment export feature. Captured project-lifetime admission for every writer
and the broader Work Management scheduling/reservation invariants remain required.

Migration `20260911000227_MoveWorkItemAssignments` copies every direct Work row and
all thirteen fields under exclusive locks before enforcing the CRM participation-only
check. Existing native identities, duplicates, unavailable references and affiliation
FKs survive. The populated Down restores those rows to CRM and refuses conflicting
assignment IDs before changing data or constraints. Stop old/new writers during this
cutover: old binaries cannot write Work assignments after Up, and new binaries require
the Work table. Downgrading further remains subject to each earlier receipt gate.

## Ordinary Agent native authority

First-party ordinary Agent graph and task adapters carry an opaque, nonserialized
source admission alongside their captured project lifetime. The native mutation
scope acquires the existing cross-process Agent catalog read lease before starting
its serializable transaction, checks original and current grants and the admitted
execution ceiling, then retains that lease through the actual owner commit. Exact
project lifetime reads participate in that transaction. A missing installed policy
or captured lifetime fails before native writes; Process source authority remains
a separate path and cannot be combined with ordinary Agent authority.

The scope checks changed native task records and the endpoints of link mutations
against both original and current task-write grants. Gantt title, schedule,
dependency, insertion and row-order commands retain the displayed project's
admission across awaits. Task creation, assignee, pricing and compensation commits
use the same final authority check. Generic graph creation, editing, metadata,
status, progress, markers, priority, move, reparent, recomposition, copy, deletion,
outline import, approval requests and prompt commands preserve the source context
through their native service call.

Media preparation runs outside the held catalog lease and SQL transaction; the
native commit rechecks authority afterward. Ordinary asset creation/revision and
outline import retain their existing non-idempotent or multi-commit behavior.
This fence adds no ordinary-command receipt and does not turn external bytes into
an atomic SQL effect. Exact durable deletion cleanup continues from retained
evidence, without rebinding that evidence to a fresh project lifetime.

Ordinary Agent project creation/update, subproject hierarchy changes and cross-project
transfer now carry captured source authority to the final owner transaction. Projects
uses its own held-source port implemented by the same Workbench Agent policy; new
subproject movement binds the original reservation and owner-issued creation receipt.
Automatic compensation only deletes an unchanged exact creation after Projects and
native/retained evidence checks in the real owner transaction. A lost creation reply
retains the project and reservation because no original receipt was acknowledged.
Agent tools and the Structure subproject dialog expose a typed partial-completion
warning with the original target ID; the original failure remains the cause.

Governed Process Projects operations retain their own source/claim adapter through
the Projects writer and native transfer. The saved ceiling comes from the original
source; assigned Executor settings only narrow tool availability. Exact server
reservations extend the original project target within the current tool session,
without adding a multi-project grant or reconstructing targets after restart.
Legacy launches keep observation and supported original-project edits; creation or
hierarchy without saved permission requires source reconciliation. New authority
records use a versioned preparation hash and cannot be read safely by older binaries.
Explicit lease-management writes, Workflow output lifetimes and other remaining
producer captures remain required work. This layer adds no recovery UI or blanket
idempotency claim for project commands.

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
- Agent execution activity and runtime snapshots: `docs/architecture/internal-communication.md`
- Agent runtime tool surface: `docs/agent-runtime-tool-surface.md`
- Runtime execution and shell portability: `docs/architecture/runtime-execution-portability.md`

### Captured task and assignment admission

Work assignments retain the existing canonical WorkItem identity and all assignment fields, with nullable lifetime provenance. Native task create/edit, assignment staging, pricing/compensation, and their row-order/resource-link follow-ups carry the original project admission. Process-origin task commands also retain their saved Process mutation authority at each actual commit. Durable subtree/project cleanup and move payloads retain their original source lifetime; a recreated project with the same ID is outside that cleanup scope. Legacy pending payloads without a binding require reconciliation.

HTTP clients must first read `/api/project-structure/projects/{projectId}/structure/read` with `source: CanonicalCurrent`, retain the returned `expectedProjectAdmission`, and submit it unchanged to task create, task update and task resource attachment. These routes and their existing fields remain available. Missing admission returns `409 ProjectLifetimeRefreshRequired`; stale admission is a conflict. Reload and reopen the editor before retrying. The tuple does not replace current actor authorization. Invocation-snapshot reads do not manufacture a current mutation admission.

Ordinary Agent task and Gantt commands retain their source admission through the final held catalog and native mutation guard. Imported native journal payloads remain historical evidence under the transfer disposition gate and cannot grant authority in the target profile.

## Project package history

Project package v3 and database row transfer preserve prepared and completed Workflow
contributions and every delivery admission state as history. Compatible v2 packages
remain accepted. Original plan, command, node, receipt and status payloads are retained,
including rows whose native target has been deleted; imported storage references in
those receipts remain historical evidence and are not rewritten as target effects.

A nullable typed import provenance value is mapped by each owner. The Workbench
owner rejects imported contribution replay, prepared-command recovery, admission
replay and delivery mutations. Automatic delivery excludes imported rows before its
batch limit. History queries can still read saved receipts and admissions, and an
explicit new admission can act under current target authority. Native imported project
objects keep their public identities and editable content.

Preflight and final target checks still cover all twelve owners; the final owner
queries use the same locked target transaction. Existing package byte staging and
compensation remain unchanged. The legacy cross-module recovery transfer exclusions
and versioned contributions for other evidence owners remain separate required work.


Project row/package transfer now calls the Projects owner with compatible data DTOs;
Workbench loads and saves only its own records. Storage owns catalog planning,
reference adoption, driver access and final catalog persistence. After byte staging,
the final import acquires the existing ordered advisory gates on the exact target
connection before starting its Serializable transaction. It then takes every ordered
exclusion table lock before any transactional query, converts the gates to transaction
locks, and runs the final owner checks and saves in that same physical transaction.

Table exclusion uses `NOWAIT`: concurrent table activity rejects this import attempt
without database mutation instead of waiting with an older snapshot. The caller may
retry as a new explicit action after resolving contention; there is no automatic
retry. Cancellation, rollback and completion release the temporary session gates. If
cleanup also fails, the error retains the original failure while cleanup discards
the affected pooled connections and closes the target connection. Ordinary owner
sessions and source export reads keep their existing transaction behavior.

New v3 packages may contain the versioned `Workbench_WorkAssignmentHistory` section.
It snapshots assignments attached to transferred native WorkItem nodes, preserving
all original fields, source profile, nullable lifetime and explicit WorkItemAssignee
role. Imported rows are immutable history without CRM foreign keys and do not create
live assignments. Payload text remains byte-for-byte unchanged on re-export; prior
import provenance is retained. Existing v2 and v3 packages without the optional
section remain accepted and do not fabricate relational assignments. This adds
narrow history portability; earlier packages did not transfer relational assignments.

The optional versioned Process asset contribution section preserves every prepared,
materialized and committed record as imported history, including deleted native targets.
Original plan/media/node/receipt text and source profile, lifetime, execution and intent
identities are retained. It does not export whole CRM organizations, Process histories,
Workflow catalogs or Storage placement journals.

## Governed Process asset contributions

Recoverable background Process asset creation and revision use the exact admitted
dynamic tool proposal as their business intent. The owner retains its original source
execution, claim, project lifetime, parent/binding fingerprint, preallocated native
and Storage IDs, and first materialized media. Retries cannot change that preparation.
Revision creation saves the native asset, binding, DerivedFrom link and contribution
receipt in one Workbench transaction. Each new native effect rechecks the current
Process claim and the original source's remaining authority through commit.

Workspace/HTTP media reads and stable Storage placement run outside catalog leases
and SQL transactions. Stored materialization avoids repeating an external source
read after restart. Storage's own provider-specific reconciliation determines whether
ambiguous byte dispatch can be confirmed; native receipt absence does not establish
that no bytes were written. These two commands do not perform image/provider
generation. Metered generation keeps its explicit started/uncertain journal barrier
and cannot acquire replay safety merely by returning a media request.

Committed contribution replay preserves later human edits, unlinks and deletion.
Receipt disclosure checks the original source and exact project grant using current
read policy. Intentional new proposals retain separate identities even when their
content is identical. Ordinary interactive/legacy asset commands keep their existing
first-dispatch behavior and generic uncertainty policy; they do not inherit this
Process receipt protocol.

Every Process asset contribution state is retained across ordinary deletion. Selected
Project transfer includes this evidence under an explicit import provenance marker;
preflight and final locked target inspection refuse every retained target state.
Historical owner reads retain their immutable snapshots, while Prepare, Materialize,
Commit, native replay and journal receipt reuse require the saved native disposition.
A returned snapshot with a cleared marker cannot bypass the final owner-row check.
Imported evidence never becomes current source authority or a target Storage claim.
Complete canonical migrations remain the only schema authority. Downgrade must refuse
retained preparations/receipts when removing their table, and imported evidence when
removing its provenance marker. Operator recovery remains separately authorized; this
transfer path cannot resume or reconcile copied source effects.
