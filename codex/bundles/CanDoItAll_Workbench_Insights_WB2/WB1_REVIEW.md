# Review of Workbench Planning WB1

Reviewed main `be2045c312ee5fa99fb5b1f8526f1825ecf8352c`, Components development
`dc573e2b438621599401a28968acef3682d14e63`. The main delta contains seven commits
from AC1, including the sealed WB1 assignment. The head message is “Close WB1
planning proof and describe attachment receipt identities”; GitHub verifies its
signature. The source review covered implementation, current report and representative
owners, not merely that head commit. See [sources](SOURCES.md).

## Keep

The actual Calendar, Gantt and both task forms/children are extracted. Planning.UI
uses neutral Components and RecordBrowsing plus dependency-free Planning.Contracts
[S02–S03]. That contracts assembly now emits its preserved XML documentation.
Do not undo type forwarding, existing serialization or accepted native identities.

The price preview now tracks input transitions including execution eligibility,
profile/project lifetime, opening, raw validity, resolver and receiver [S04]. Its
cancellation source belongs to the request until the resolver unwinds. Preserve
lazy caching and explicit refresh, not just the newly added guard.

Native task outcomes retain known task/resource IDs and separately describe
assignment, pricing, ordering and compensation [S05]. Calendar state writes are
ordered and carry original admission [S06]. Native compensation checks the expected
task inside the existing coordinated writer before restoring its assignment [S07–S08].
An uncertain pricing commit retains its already-created attachment rather than
silently removing it and leaving a new price [S09]. These are useful bounded fixes,
not a reason to redesign all transactions or introduce generic command journals.

## One bounded source finding

[S11–S12] reveal a fault/cancellation branch in the new Gantt interop disposal path.
It is detailed in [S0_GANTT_LIFETIME.md](S0_GANTT_LIFETIME.md). This review did not run
its runtime reproduction, and does not claim a data-integrity or authorization incident.
Keep serialization/latest-model behavior and repair cleanup at its actual owner.

## Testing disposition (implementer-reported, not rerun here)

| Checkpoint | Recorded meaning |
|---|---|
| Final W3 focused families | 251 native and 44 independent planning cases pass; earlier overlapping topic runs are not additional unique coverage. |
| Shared Gantt | 88 existing/focused component cases and three controlled lifetime cases are reported passing; those new cases exercise delayed success, not canceled updates. |
| Published UI | Actual Fast/Parity controls, gestures, exports, independent views and 1920x1080 geometry are reported verified. |
| Frozen Stable | 16,633 executions: 16,629 pass, four fail, zero skipped, 28 assemblies. 55 expanded theory cases explain the difference from discovery. |
| Repairs after Stable | Two API documentation gaps are covered by a final 32-case follow-up; a two-case Workspace status test fix has separate evidence. |
| Remaining scanner result | The original full-worktree failure is a retained synthetic negative control in historical evidence. Source/delta scans have their own scope; the original run is not all-green. |
| Native final consumer campaign | Nine cases pass on image b58ca3d2…, including provider, planning/file, Workflow and Scheduler paths. |
| Later metadata-only image | a00017cc… has separate document-route and byte-identity proof. The nine consumer cases were not rerun on it. |

No complete pre-run assembly-hash manifest was captured for the wide checkpoint.
The report explicitly records later DLL observations as later, and separate repair
assemblies/images. Preserve this provenance limitation; do not reconstruct a
pre-run claim from later matching files [S01]. No full 19-vector shared-provider
protocol rerun was claimed for WB1, and this review does not imply one.

Three watch observations per owned file kind were reported: Razor 915/925/913 ms,
C# 530/467/428, CSS 822/685/705, JS 318/711/834. Razor/C#/JS included automatic
browser navigation with the same server; CSS changed in place. Different baselines
and observation conditions do not establish a controlled whole-Web speedup.

## Delivery and decision

The report's “local-only” Components note is historical: dc573e2b is now reachable
on remote development. Do not recreate or re-request that push. A new S0 repair
must still be compiled/served in the source pair actually tested and separately
identified if not yet remotely available. CI resolves a matching Components branch
[S30]; merely sharing a package version does not identify the code.

Keep WB1 and proceed to a larger WB2 after the bounded S0. Agents/Workflow, Workspace
and Projects are not reopened as wholesale extractions. Full release readiness is
outside this decision. This review is source/configuration/report analysis: no
C# build, native database, Docker, browser or watch execution occurred here.
