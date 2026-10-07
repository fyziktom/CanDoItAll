# Review of the pushed WB5 implementation

Review mode: connected GitHub source inspection, not execution of .NET, PostgreSQL, Docker,
Playwright, production UI or watch tests. Original private TRX/logs/images were not supplied.
The current source pair and coverage are in [SOURCES.md](SOURCES.md).

## Verdict

Preserve WB5 and finish the remaining Workbench integration. The reviewed party, secret and
runtime boundaries are real, not just cosmetic wrappers. No new blocking WB5 implementation
error was identified in the inspected critical paths. This is a scoped conclusion, not a
whole-application security audit or release certification.

`ProjectPartyEditorSession` captures the immutable quick-create input, original native target,
operation and accepted PartyId. Assignment replacement uses native occurrence/concurrency
checks and retains assignment IDs separately from node presentation commit and observation.
`ProjectSecretReferenceSession` uses the existing vault owner, retains vault and reference
IDs separately, clears transient values and permits explicit completion without a second
create. Its canonical-root exception validates the exact active project root/lifetime rather
than comparing a newly projected synthetic record occurrence. Ordinary nodes retain their
checks. Runtime approval freezes the reviewed plan, exact node and project; Stop and readiness
use the accepted WorkspaceOwnedProcessIdentity rather than an arbitrary reused PID/node key.

The Operators project references neutral BaseLib/CanvasLib, RecordBrowsing, Security.Abstractions
and the extracted EmbeddedBrowser. The browser markup/CSS moved without a new embedding policy.
Preserve these source relationships; evaluated graph counts in the implementer's report were
not remeasured by this reviewer. [S03, S04, S05, S06, S07]

## Recorded evidence, with its limits

The maintained WB5 report states 15 fixed quick-create cases; 48 party/native continuity
cases; 91 runtime/policy cases; 17 leaf and 23 runtime page cases; 53 final secret/page cases;
and real Windows process/descendant stop checks. Counts overlap and are not additive.

The original frozen Stable run executed 16,906 cases: 16,903 pass, three fail, zero skip,
across 32 assemblies. The neutral EmbeddedBrowser graph expectation and canonical occurrence
assertion have separate 3/3 and 4/4 follow-ups. The remaining source scan detects four
unchanged historical synthetic controls. The original aggregate exited 1; it remains failed.
The late canonical-root repair has separate native proof and a rebuilt application image.
It did not run inside the earlier frozen broad binaries. [S03]

The report describes thirteen browser consumer cases and explicitly distinguishes successful
continuations from original observer failures. Nineteen protocol scenarios and the initial
catalog checks belong to the earlier image; inheritance is claimed only for unchanged
provider inputs. The final affected consumer flows belong to the repaired image. Usage
maintenance changed only a derived owned index; it is not permission to rewrite canonical
records or a guarantee that every normal installation has complete historical coverage.

The source and published sandbox/watch proof is positive, but it is not a genuine-model
customer rehearsal. Most external model responses in those tests were deterministic.
WB6 now requires that second lane explicitly. [S03, S21, S22]

## Remaining work is concrete

The current ProjectStructure component subtree retains the full mixed Workflow/Process dialog
renderer and four staffing/candidate dialogs. Page WorkflowNodes, Processes and
ProcessPreparations own their native effects. SupportDialogs still contains the actual
managed-file delete confirmation. There are also legitimate completed-family adapters and
context hosts, which do not need a second extraction. [S08-S16]

The current Workflow add/start path can publish a late operation into a successor dialog;
[S0_FIXES.md](S0_FIXES.md) gives the source-derived test. This predates WB5.
WB5 also explicitly records an upper content-preview heading overlap with the canvas toolbar.
That limited visual follow-up should be fixed now, not relabeled as full visual closure.

## Dependency delivery

Components development resolves to a3fd4d22f2c4e0432cf389f194c6468b44ab7371, the required WB4/WB5
source. No missing Components push was observed at review. Recheck actual inputs at execution,
including FileTools, because remote availability and the assemblies used in a local build are
separate facts. Do not recreate already published repairs. [S01, S02]
