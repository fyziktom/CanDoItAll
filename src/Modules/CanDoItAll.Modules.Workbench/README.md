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

Workbench runtime services use the explicit thirteen-record `WorkbenchDbContext`: the
existing ten records, Workflow contribution receipts and admissions, and Work assignments. The pooled
factory remains bound to the immutable canonical host profile. Complete migrations
remain the only schema authority; profile-transfer maintenance is a separate cutover.

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

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
- Agent execution activity and runtime snapshots: `docs/architecture/internal-communication.md`
- Agent runtime tool surface: `docs/agent-runtime-tool-surface.md`
- Runtime execution and shell portability: `docs/architecture/runtime-execution-portability.md`
