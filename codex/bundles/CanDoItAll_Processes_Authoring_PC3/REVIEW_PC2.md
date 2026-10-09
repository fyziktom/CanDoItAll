# Review of the pushed PC2 implementation

## Candidate and limits

Compared the PC2 input baseline `146067ed133f624878dfe5756c441a43c0f21b4a` with `095c418b7981511ba9c96a75b7d2950ff4f326a6` on `components-decoupling`.
The range contains eight commits: one bundle import and seven working/final checkpoints.
The branch HEAD's GitHub signature verification is valid. The report identifies the
signed series; this reviewer did not locally re-verify each signature or possess the key.
The final branch recheck is recorded in SOURCES.json, not a checkout instruction.

Three production files changed: ProcessWorkspaceShell.razor, ProcessRoleEditorState.cs and
ProcessStepEditorState.cs. The selected source review covers their changed ownership and
reconciliation paths, the new tests, the maintained report and important native consumers.
It is not a line-by-line re-certification of every old shell method or repository file.
The source register marks ranges and partial reads. No .NET SDK/build, PostgreSQL server,
Playwright run, runtime timing or CodeAnalytics query was executed by this reviewer.

## What is actually implemented

**Read/mutation ownership.** The shell now uses a separate authoring operation fence,
completion sequence and unknown-outcome state rather than letting workspace read generation
own an in-flight write. It retains accepted results through read completion and prevents
alternate authoring actions from replacing a pending submission. Pending canvas moves are
coalesced, and template import origin is separate from later library browsing. [S01, S04]

**Real retirement.** Profile change notifications retire the old opening even without a
parameter echo. Exact operation, profile generation, definition key and project binding
protect completion/error/finally paths. The test matrix covers five command families,
scope/project-incarnation/disposal changes and A -> B -> A openings. [S01, S04]

**Role identity.** Add/Delete apply the owner's selected role; the last deletion clears
fields/dialog state. A later explicit role selection retires the captured submission, so
an older completion cannot take that selection back. Save normalization merges only
untouched fields from the accepted result. [S02, S05]

**Actual step identity and raw edits.** DecisionRoleKey is resolved from the actual edited
step rather than an unrelated projection selection. Row normalization preserves later
edits and invalid numeric buffers using stable keys. The tests assert both the edited and
unmodified steps, not merely an Accepted label. [S03, S05]

These are substantive PC2 corrections. Preserve them; no source evidence found in this
selected review justifies undoing the UI extraction or replaying PC2 from scratch. This
is a source-review conclusion, not an independently executed no-regressions guarantee.

## What the execution report proves and does not prove

The maintained PC2 report attributes 530 passing cases to its final source/configuration
campaign: 21 light UI, 181 workspace/editor, 61 adjacent component, 238 unit, 14 integration
and 15 browser cases. It reports no failed/skipped cases in that union and successful
portability enforcement. It explicitly leaves broad Stable and new performance proof
unrun/not applicable for the bounded PC2 production delta. These are reported executions,
not this reviewer's test runs or PC3 target counts. [S06]

The new authoring browser tests use real controls and native owner logic. However, their
fixture holds role/step service instances and substitutes their reads/writes inside a
client decorator. Production's ordinary client creates fresh scopes. Thus those journeys
prove the PC2 UI/owner-result handling, not production authoring durability. The report
states the qualification openly. The timing-only Definition result hold is a separate
fault seam; do not conflate it with the retained role/step ownership. [S07, S08, S09]

The separate PostgreSQL diagnosis uses the actual client and finds lost definition/role/
step/import state and canvas loss in a new client scope. Its new-root result is not an OS
process restart. The diagnostic source/raw output are ignored local artifacts; hashes in
a report do not make their bytes independently available in this review. PC3 must commit
reproducible positive regression tests and a genuine restart harness. [S06, S10]

GitHub returned zero check-runs and zero Actions runs for this HEAD. CI's push trigger names
main/development, not this feature branch. Therefore the available evidence is a tracked
local campaign report, not a green CI run. Do not alter workflow triggers merely to turn
this review into a CI exercise. [S30, remote observations in SOURCES.json]

## Remaining owner defect — deliberately outside PC2, explicitly inside PC3

Definition/editor services keep snapshots in instance dictionaries, and the native client
recreates a scope for ordinary reads and commands. Canvas uses a retained session dictionary;
imports keep a per-instance list of imported metadata. Catalog counts remain template-
derived with DraftDefinitionCount = 0. Launch ResolveDefinition still loads the distributed
template. No durable authored-definition entity exists in the reviewed Processes context.
These are preexisting native-authoring limits, not new extraction regressions. [S09-S15]

The PC2 prerequisite is a useful starting point. PC3 is its implementation, not another
request to write that document. A singleton cache or only persisting the visible form
would leave the actual Save -> Catalog -> Publish -> Launch contract incomplete.

## Integration findings to address while implementing PC3

| Finding | Evidence/classification | Required consequence |
| --- | --- | --- |
| An accepted token can permanently pin the view | ReconcileDefinitionRead retains accepted panels whenever an incoming opaque token differs. This is source-verified; a real newer multi-opening symptom was not executed here. | Add authoritative observation relation; preserve receipts separately and handle dirty/clean newer revisions. |
| One revision cannot be applied only to one panel | Current result helpers replace separate child projections. A future aggregate-wide version affects sibling baselines. | Coherent revision envelope/change set; no silent expected-version advancement on unreconciled drafts. |
| Editor DTOs omit execution meaning | Template documents contain guidance, capability/operation contracts, typed subprocess contracts, artifact inputs and other fields not represented by a simple edit form. | Command-owned patches onto a complete supported document; lossless rich round-trip tests. |
| JSON alone loses resolved guidance | ResolvedExecutionGuidance is JsonIgnore, while the kernel hash includes guidance fingerprints. | Freeze materialized resources/content hashes or an equally reliable immutable resolver; preserve legacy no-op identities. |
| An empty driver list can mean two different things | LaunchVariablePreparation reloads template activations when DriverActivations.Count == 0. | Distinguish resolved-empty from not-yet-resolved; never restore disabled template drivers in an authored launch. |
| Import currently records a label, not merged executable content | Import service only updates ImportedComponents metadata in the reviewed path. | Materialize the selected process/role/artifact meaning and provenance into the same target aggregate. |
| Canonical migration ownership is not the narrow runtime DbContext | Migration factory builds AppDbContext using the complete composition catalog. | Append to the existing canonical chain/model/transfer contract, not a parallel startup SQL schema. |
| A NULL global scope can evade a composite UNIQUE constraint | PostgreSQL default NULL uniqueness semantics; this is a design risk, not a diagnosed current PC2 DB bug. | DB-enforced non-null scope identity or equivalently correct constrained indexes; concurrent global creation test. |

See NATIVE_OWNER_DESIGN.md and PUBLISH_AND_LAUNCH.md for the selected implementation
boundaries. The subprocess contract resolver inspected here is a pure decoder of captured
launch variables, not a template loader; keep that property rather than introducing a new
mutable global resolver. Publication integration must follow actual consumers. [S16-S25]

The remote compare from reported browser-source checkpoint `72cbacef3185244c2633d0729443ba9819ddce18` to the reviewed HEAD contains only the maintained boundary report and the two PC2 validation documents. No production or test-source change is present in that final delta. This strengthens source attribution but is not a rerun of the locally reported results.
