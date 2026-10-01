# Projects Files P2 review and continuation decision

Reviewed application: `92a3c373c742537608fded3c483b685291339853`, not the old default-branch
search snapshot. The compare from `c207b499c3165ba95e46ffaf518884097d3135dd` is three commits ahead,
including the historical input archive. Preserve it as history, not current execution work.
Source IDs resolve in [SOURCES.md](SOURCES.md).

## Accepted architecture and earlier fixes

P2 moves both complete Files renderers to a separate leaf. The original component entry points
retain effects and production composition. Independent staged workspace replacement avoids
mutating an accepted successor through a late source resolver. The leaf's declared packages are
BaseLib and neutral FileBrowser/FileInteraction components; P1 does not gain file implementation
references. Actual authorization/coordinators and content grants remain the owners. [R01, R03, R24, R27]

P1-R1 now catches the exact original `ProjectWriteAdmissionRejectedException` in the seed/delete
path. `AdmissionRefused` keeps an earlier confirmed save and rejects the stale draft while releasing
only its mutation slot. Genuine unknown outcomes stay guarded. P1-R2 separates pending import from
acquired JS module ownership; teardown no longer reawaits a previously handled failed import or
waits indefinitely for a pending one. Preserve these changes and their controls. [R08, R09]

The review found no new reason to reopen the Workspace extraction or rewrite P1. One small
operation-publication gap remains in Files: [P2-R1](S0_PREVIEW_OPERATION_FENCE.md). It is an UI
origin-consistency issue, not established unauthorized access or corrupted persisted data.

## Evidence evaluation — implementation-reported, not rerun by this reviewer

| Selection | Recorded result | Interpretation |
|---|---|---|
| Native mutation controls | 14 pass; four initial controls failed before repair | Supports original refusal fix |
| P1 leaf | 18 pass, including seven interop cases | Supports form-import lifecycle fix |
| P2 neutral leaf/scenarios | 17 pass; repeated after final fixture/menu changes | Does not substitute for native owners |
| Owning unit selection | 158 pass | Includes narrower 14-case session/action selection; do not add it again |
| Native combined selection | 87 total, 85 pass / 2 fail; exact two later pass | Mixed run stays mixed; viewer expectations corrected to actual composition |
| Neighbor leaves | Resources 70 pass; Workspace Core 52 pass | Bounded neighbor checks, not whole-Workspace execution |
| Combined desktop/consumer attempt | 11 total, 8 pass / 3 fail | Six refusal controls passed; retain failures and later causal follow-ups |
| Final menu/source/publish/production selection | 3 pass | Covers final generated CSS and full-menu geometry; not a new complete 11-case run |

All these statements come from the checked-in P2 evidence index [R02]. Raw TRX, screenshots,
private manifests and full runtime logs were not provided to this review. The reviewer did not
execute C#, product builds, PostgreSQL tests or Playwright. The reported failures were kept and
several had explicit test/fixture/composition causes rather than unexplained retry-until-green.

Broad Stable was deliberately not rerun for this bounded feature. The reported graph/owner
analysis and selected native/consumer proof are a reasonable bounded decision; historical P1
Stable is not fresh P2 proof. Reevaluate the trigger for A1 according to actual impact, not by
copying that decision. Inherited selector uncertainty remains `REVALIDATED_WITHOUT_ROOT_CAUSE`;
no new paid inference was authorized. [R01, R02, R26]

## Development loop and census

The implementer reports unchanged 39 prior UI/sandbox closures, P1 graph 5/6 and watch set 300,
Files leaf/sandbox 8/11 and watch set 479. Three sandbox Razor observations were 1.33/0.83/0.83s;
C# 0.68/0.71/0.68s, with hydration 0.45s. These are local recorded observations, not reviewer
benchmarks or a uniform whole-Web speedup. [R01, R02]

Maintained `modules.md` still says Files P2 is deferred. Correct that small documentation gap,
while preserving historical sealed files. Confirm actual UTF-8/desktop title separator before
changing the apparent `Files Â·` text; do not turn it into an encoding or responsive sweep. [R06, R10]

Continuation: after S0, implement **Agent Editor Core A1**. This is a partial technical-editor
slice, not a claim that AgentFramework, Workflow authoring, Workbench or Processes are complete.
