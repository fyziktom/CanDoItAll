# Processes UI PC2 execution record

PC2 stages P0–P5 are complete on `components-decoupling`, with **native authoring
durability blocked**. The assigned mutation, role-selection and step-draft corrections
are implemented and the required affected selections pass: **530 tests, zero failures,
zero skips**. This is not release clearance or a claim that accepted definition edits
survive a new native request scope.

The [machine-readable evidence](processes-ui-pc2.json) contains all 31 acceptance groups,
all 42 PC1 carry-forward rows, exact filters and commands, source hashes, signed
checkpoints, artifact digests and inspected screenshot findings. The
[authoring authority prerequisite](../architecture/processes-authoring-authority-prerequisite.md)
is the next architectural scope. The [existing boundary record](../architecture/processes-ui-boundary.md)
retains PC1 history and its original performance conditions.

## Candidate and provenance

| Input | Reviewed/executed value |
| --- | --- |
| Entry | Clean `a1042e97c38d20fc58ad60521fbe22c205bbccdc`; tree `2eb63b4d31c3eba3820359a3d0d85f35d271cd4e` |
| Tested source and browser checkpoint | `72cbacef3185244c2633d0729443ba9819ddce18` |
| Tested tree | `430e5e1e0969dfbd46a23a3a5cffff06d44d63b5` |
| Last production-code change | `8951ffdf7d35aaf0b00b7fe8220f1aad8d9e3d59` |
| Changed-code digest | `ada37311d31fdaf7f41d881ea9a3fd5e1fb5567d1501214ebd3fba85be5ed93d` |
| Components | `f3745356182444656edfdef56aa827095632d3ea`, clean and unchanged |
| FileTools | `3a080ecd31068a77c1e1bd639f7a78e21c93db85`, clean and unchanged |
| Runtime/build inputs | Windows, .NET SDK 10.0.303, `ProcUiProofPC2`, local sibling source mode |
| Package integrity | PC2's 16 and shared v4.1's 24 sealed inputs pass normalized SHA-256 verification |

Each test lane ran after refreshing its owning assembly and before committing its
tested changes. The lane metadata therefore records the preceding HEAD. The final
code blobs and normalized file hashes in the JSON identify the tested changes;
its aggregate hashes sorted `path + space + normalized SHA-256` lines joined by LF.
The browser index tree before the reviewed baseline refresh was
`128560d9130b15aaa1da72c59bb0bc4ffa25bb09`. Only the intentional baseline delta separates
that tree from the browser checkpoint. Earlier lane metadata's `sourceDelta` lists
unstaged tracked changes only; it is not used as a complete source seal.

No production code changed after the editor checkpoint. Later changes add tests,
the native-owner plan, the reviewed static baseline and this report. The containing
documentation commit is verified after creation; its identity comes from Git rather
than an impossible self-referential hash in its contents. Input bundles were not edited.

## Corrections and failing-first evidence

| Finding | Actual reproduction and correction |
| --- | --- |
| F1, all five command families | Three initial failures reproduced missing accepted receipts and premature mutation-slot release. The host now owns mutations separately from reads, fences each completion to its exact opening, retains acknowledged opaque revisions, and reports unconfirmed read-back without replaying a command. Alternate actions and Enter cannot replace pending submission. |
| H3, profile notification | Two failures showed that switching the database profile did not schedule the expected fresh read. The shell now subscribes to the native notification owner, dispatches through the renderer and unsubscribes on disposal. Retired notifications and command completions cannot affect a successor. |
| F2, role Add/Delete | Native-owner results reproduced selection remaining on the old role after Add and on a removed identity after last Delete. Eligible results now adopt the owner's selection; an explicit later user selection wins. A last deletion clears fields, errors, submission and dialog state. |
| F3, step decision authority | Distinct and null second-step bindings reproduced the unrelated first step's `DecisionRoleKey` being submitted. Draft creation now resolves the actual edited step. Other step metadata and another step's binding remain intact. |
| H1, normalized rows | The owner trimmed a row description, but a later edit to another field restored the old whitespace. Typed field-by-field reconciliation now keeps accepted normalization for untouched fields while preserving later fields, stable-row changes and invalid numeric buffers. |
| H2, discard lifecycle | Pending Discard is disabled in the actual renderer, so the proposed pending-discard path was not established as a reachable defect. The reachable refusal → Discard path clears submission state; a delayed old acceptance is ignored. Callback cleanup releases only its own captured submission. |
| F4, native lifetime | The unmodified native client reproduced accepted content loss on its next request, a new scope and a fresh DI root. No existing durable authoring owner was found. The required implementation-ready prerequisite is delivered; durability remains blocked. |

The five initial editor failures are from an explicit owned template pack. An earlier
wrong projection-property compile attempt and a historical default-template fixture
failure were setup defects, not product reproductions. The first Save browser attempts
also contained harness mistakes: a wrong Definition tab selector and an assumption that
Refresh was disabled. The final browser test clicks the available Refresh action and
asserts that Save/Publish stay disabled throughout the held native command.

The shell keeps one mutation slot per opening. Actual definition/scope/profile/project
lifetime changes, denial and disposal retire it. Accepted, rejected and unknown outcomes
have separate recovery behavior; unknown results block blind replay. Pending canvas
moves coalesce by node identity and drain with the latest acknowledged revision. Import
keeps its submitted target even if the user browses another template meanwhile. Thirty
additional cases cover all five families across scope, project lifetime and disposal,
with late success and failure while a same-ID successor owns another command.

## Required validation

Direct builds passed for Processes.UI, the native Processes module and Web, followed
by the sandbox in separate Fast and Parity outputs. All use `ProcUiProofPC2` and `/m:1`.
Browser child configuration matched prepared outputs; no external browser base URL
was set. The JSON records the exact project and filter for each command, along with
build, discovery and execution evidence.

| Final selection | Expected discovery | Actual discovery | Executed/passed | Proof |
| --- | ---: | ---: | ---: | --- |
| Independent Processes.UI | 21 | 21 | 21 | Real renderers/tabs, raw drafts, two canvases, Markdown, positive closure and forbidden/unresolved/public-type negative controls |
| Native workspace plus new editor regressions | 181 | 181 | 181 | All workspace partials, read/write races, accepted/unknown outcomes, profile/project retirement and editor semantics |
| Native adjacent files/cancel/chat/activity/Workbench | 61 | 61 | 61 | Late effects and cleanup, typed/default chat consumers and actual project opening lifetime |
| Definition/pipeline/launch/operator/files/Workbench units | 238 | 238 | 238 | Native returned selections, exact expected versions, caller intent, runtime authority and executor mapping |
| Native execution/workflow/producer API integration | 14 | 14 | 14 | Actual execution/result/artifact and durable preparation contracts |
| Browser union | 15 | 15 | 15 | Three authoring, four source/published sandbox, three smoke, two native, one Workbench and two voice cases |
| **Total** | **530** | **530** | **530** | **Zero failed or skipped cases** |

All selected theory rows were expanded during discovery, so the listed and executed
counts match. The initial adjacent-suite estimate of 56 missed five facts in
`ProjectStructurePageProcessOpeningTests.cs`, another partial of the launch-scope
class. The source count was corrected to 61 before running that selection. No zero-test
success or unexplained mismatch is accepted as proof.

The final browser union ran on 2026-10-09 from 10:56:57 to 11:04:15 UTC. J1 holds an
actual native definition result, switches tabs, refreshes, types later input and presses
Enter: exactly one command is observed, acceptance remains visible and later text survives.
J2 checks the returned added role, each authoritative survivor and final empty dialog.
J3 checks the actual submitted second-step identity and decision role, the unchanged first
step and tab return. J2/J3 retain actual native role/step owner instances in a test-only
composition to inspect sequential accepted UI outcomes. They do not establish durable
native-client storage; the independent P3 probe uses unmodified lifetimes.

Existing browser journeys execute real launch/workflow/files/chat/attachment/cancellation
owners in both global and project scope and inspect independent native identities and
effect counts. Workbench verifies the Workflow executor and delivered original-node link.
Voice tests cover actual browser media permission denial, permitted use and late retirement.
Upstream model responses are controlled loopback fixtures; no live-provider claim or
external inference spending is involved.

## Native authoring result and next scope

The probe resolves the actual `IProcessWorkspaceProjectionClient` through native DI
against owned PostgreSQL 18.6. SaveDraft, Publish, SaveRole, SaveStep and ImportProcess
return Accepted, but following reads return template content. Reusing the accepted
definition revision in another Save is rejected. The catalog remains 27 published
definitions and zero drafts. Requested canvas coordinates `(321, 654)` survive the same
client but return to `(260, 0)` in another scope/root.

A project-scoped Save loses its submitted name with the same profile, project and
project lifetime. Native operator authority and a real caller intent create a durable
prepared launch that survives a fresh DI root, but its initial plan uses the unchanged
template definition and version. A fresh root in the same operating-system process is
the restart boundary actually executed; this report does not claim an OS process restart.
The diagnostic source, command and result hashes are recorded in the JSON.

The next scope is the prerequisite's native definition aggregate and transactional
revision/command ownership, followed by explicit publication and catalog/launch resolution.
That plan maps current consumers, project/profile admission, transfer/retirement,
migration and rollback, and cross-scope/restart/conflict/native-browser acceptance tests.
PC2 deliberately introduces no schema, singleton cache, template-file persistence or
new launch authority. Durable runtime records do not repair request-local authoring.

## Active renderer and dependency census

| Active family in Processes.UI | Native entry/consumer |
| --- | --- |
| `ProcessWorkspaceSurface` | Global/project pages → `ProcessWorkspaceShell` |
| `LiveProcessesSurface` | Live page → `LiveProcessesDashboard` |
| `ProcessDefinitionRoleEditorPanel` | Workspace deferred Roles tab |
| `ProcessDefinitionStepEditorPanel` | Workspace deferred Steps tab |
| `ProcessDefinitionCanvasPanel` | Workspace Steps canvas and maximized lifecycle |
| `ProcessTemplateLibraryPanel` | Workspace deferred Exchange tab |
| `ProcessRunFilesSurface` | Live dashboard → native authorized files dialog |
| `ProcessRunCancellationSurface` | Live dashboard → native cancellation adapter |

The same families are reachable from the independent scenario host. The evaluated
source graph contains Web **181 projects / 834 edges**, UI **25 / 42**, and sandbox
**27 / 47**, with zero cycles, missing references or forbidden light-project edges.
Runtime closure guards additionally reject forbidden transitive references and
unresolved assemblies and inspect public parameter/injected types.

Deferred tabs reach the real role/step/canvas/template renderers. Typed chat publication
reaches the actual chat and activity children. File content uses the authorized native
lease and actual FileInteraction/Markdown composition. Native remnants own selection,
admission, profile/project lifetime, reads, effects and receipts. They do not duplicate
the extracted controls. No further assigned renderer extraction or public abstraction
is justified; the authoring authority prerequisite is nonstructural.

CodeAnalytics, Components and dotnetwatch were absent from actual enabled-tool discovery.
Source/consumer/DI inspection, evaluated restore graphs, generated/deferred asset edges
and runtime assembly checks are the explicit fallback. There is no claimed fresh MCP
index. Playwright execution was available and used for the required browser proof.

## Visual, static and operational closure

Eleven final-run screenshots were inspected at 1920×1080. The JSON records exact
paths, SHA-256 values and individual observations: accepted Save plus later text,
added-role modal and last-delete empty state, second-step form and canvas, maximized
published Parity canvas, native current file and attachment chat, independent source
Fast openings, published Fast Mermaid and source Parity inline validation. Forms,
modal footers and bounded detail scroll regions remain usable without horizontal
overflow. The last-delete screenshot still shows the shell's template-derived role
count; it is recorded as an owner/read limitation, not durable aggregate success.

The portability scanner inspected 9,304 files and returned 35,207 findings without
truncation. One new executable-source allowance was reviewed: typed
`LoopBudget.EscalationTargetKind` reconciliation in `ProcessStepEditorState.cs` is
semantic process-domain logic without an operating-system/process authority edge.
Only that allowance, the count and generated timestamp changed in the baseline.
Final enforcement **without `--write-baseline` passes 15,358 reviewed findings**,
with no stale findings. Six portability and four artifact-scanner self-tests passed.

Broad Stable is **NOT_RUN**. PC2 changes bounded local state and fixture behavior;
it adds no production project references, schema, root/shared build configuration,
cross-cutting composition or shared test bootstrap. This task does not establish a
new full-application frozen validation campaign, CI, release or merge closure.
The required affected union is complete. Historical PC1 broad results are not presented
as fresh PC2 evidence. Existing nullable/analyzer warnings and AngleSharp 1.4.0's
NU1902 advisory `GHSA-pgww-w46g-26qg` were observed; dependencies were not changed.

Development-loop remeasurement is **NOT_APPLICABLE**: dependency/build/static-asset/watch
paths did not change. Current source/published launchability passed. PC1 timing samples
remain historical, with their original warm-cache and host conditions; PC2 claims no
new speedup.

The task-owned PostgreSQL container was removed after verifying its exact identity and
task label. All native/test processes using `ProcUiProofPC2` had exited. Existing apps,
the ordinary 5032/5432 installation and unrelated containers were untouched. Ignored
task outputs remain available locally. Text secret scans cover private evidence and
627 decoded browser trace/network/served-asset payloads, with zero findings or oversized
skips. Binary images/media are outside the text scanner; inspected images use only
owned fixtures. The initial private helper's literal variable-reference false positive
was corrected; no actual credential was persisted or published.

The committed summary is durable evidence; ignored local logs, TRX and traces are
addressed by hashes for this workspace and are not promised as published artifacts.
Hashes do not substitute for access to missing raw files. Documentation validation passed for 402 maintained Markdown files; all nine documentation-evidence
checks passed. Source/report/helper text scans pass with zero findings. Their commands,
coverage and evidence locations are recorded in the JSON before its signed commit.

## Verified signed checkpoints

The operator unlocked the configured key locally at entry. A nonsecret challenge and
each incremental commit verified with fingerprint
`96E836FAA8854EE98ABC10903C206549E1D7EAD6`, using the existing GPG executable/home/agent
and one dedicated committer session. Normal hooks remained enabled. No signing policy
change, unsigned commit, push, amend or history rewrite was performed.

| Commit | Tested stage |
| --- | --- |
| `5ed0f67a46323cf0d70989b4fad983bf66de2fc5` | Mutation ownership and read reconciliation |
| `4b60ae3b2162ab7208807cb0bd79b4ae5466e3ad` | Database profile notification retirement |
| `8951ffdf7d35aaf0b00b7fe8220f1aad8d9e3d59` | Role identity, actual-step semantics and draft reconciliation |
| `619f7057312abdaf086f102e7c148cbaed8de239` | Native authoring authority diagnosis/prerequisite |
| `ef06bb82b5ced2d6336894dbea619c5444f3b209` | All-family scope/project/disposal completion regressions |
| `72cbacef3185244c2633d0729443ba9819ddce18` | Browser journeys and reviewed portability baseline |

The subsequent report commit contains only maintained evidence and the boundary link;
verify it using `git verify-commit` on its containing commit. Structural and editor
correctness, controlled native runtime, browser/assets and portability pass separately.
Native authoring durability and any release claim dependent on it remain blocked.
