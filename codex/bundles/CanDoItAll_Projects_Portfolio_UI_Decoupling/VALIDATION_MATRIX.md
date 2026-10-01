# Validation matrix — exact scope, current discovery

Read the latest repository testing guide before execution. This document is an acceptance map, not a list of commands already run or assumed test counts. Build the changed production projects and the owning test assembly before `--no-build`; use current `--list-tests` output and account for theory rows.

| ID | Required behavior | Minimum proof |
|---|---|---|
| E0 | Actual application/Components/FileTools pair, source/package mode and preserved dirty work | Git/tree/build input record; protected evaluated graph baseline |
| S0 | Exact ordered shared-provider import → first editor → Ollama model selector, plus stale-provider controls | Configured production test with actual owners and scripted external response; deterministic component controls |
| P1-01 | New leaf and sandbox have no hidden production dependency, public API leakage or unresolved edge | Evaluated direct/transitive project/package/native graph and negative boundary guard |
| P1-02 | Main projects, tree expansion, every filter/mode/include-subprojects and same Cards/Files scope | Real renderer + existing projection unit tests + production navigation |
| P1-03 | Overview and all five steps; raw invalid dates/numbers, Unicode, unblurred text, stable context/rows | Real form event tests and desktop browser |
| P1-04 | Save/Save-and-open/Enter duplicate admission, accepted target, no stale navigation | Held writer + exact counter/owner read-back; browser success and error |
| P1-05 | Delayed save/read-back preserves newer edits and phase/option identities | Real owner ack, deterministic delayed reads, repeat save IDs unchanged |
| P1-06 | Cancelled new project's starter plan cannot leak into B; acknowledged seeding not replayed | Original-page failing/control test + real Workbench batch count and identity proof |
| P1-07 | Original lifetime on save/seeding, same-ID recreation refused, owner authority unchanged | Isolated PostgreSQL source/lifetime negative controls; existing owner/agent suites |
| P1-08 | Delete/partial cleanup receipts survive read failure; exact retry; no successor close | Existing real participant test plus held-refresh/error controls |
| P1-09 | Files slot/dialog and package controls remain actual production behavior | Real authorized file bytes + existing package target tests; no fake success |
| P1-10 | Project-bound context, old completion, actual deterministic Agent/Workflow/file consumer | Context host tests + source/owner/effect identities in production browser |
| P1-11 | Same extracted renderers in source and independently published sandbox; assets and hydration | Desktop Playwright; exact static asset/stylesheet registration |
| P1-12 | Direct route, query, modal close/back/forward, neighbor Settings/Agent/Project Structure routes | Actual shared Web composition and retained navigation acknowledgement/log assertions |
| P1-13 | Isolated development loop and no protected leaf growth | Evaluated graph/watch before/after + restored probe + real edit visibility |
| G1 | Current owning tests, final integration checkpoint when triggered, static/doc/security review | Commands, discovered/executed/skipped counts, exact source binding and reason for scope |

## Existing tests to locate and retain

`tests/Components/CanDoItAll.Tests.Components/ProjectsPageTests.cs` already covers real file viewers/leases, shared Cards/Files filtering, lifetime conflict, partial deletion, main-project filtering, tree behavior, hierarchy drill-down, Gantt navigation and package constraints. The final filter must reflect its current namespace and data rows. Do not reduce it to a new fake session test and delete its production composition. [R18]

Use current code/reference analysis to locate the unit projection/tree/load-generation tests, Projects service/admission/deletion tests, creation receipt/compensation, package transfer tests and Projects context completion tests. Record paths actually found. The fact that a consumer is named CRM, Resources, TestLab, Workflow, HTTP or Workbench rather than Projects does not exempt it when a shared model changes.

For the existing page topic, an initial discovery candidate is:

```powershell
$project = './tests/Components/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj'
$filter = 'FullyQualifiedName~CanDoItAll.Tests.Components.ProjectStructure.ProjectsPageTests'
dotnet test $project --configuration ProjectsUiProof --list-tests --filter $filter /m:1
# Verify the actual expected identities/count, then run the same filter on current assemblies.
```

This is not a fixed count and not a replacement for current instructions. Choose the documented same configuration for browser child hosts. Keep build outputs separate from the user's running watch sessions.

## Test rigor and scope control

Drive the actual field events, including `input` before blur, and await dispatched actions. Use the current bUnit dispatcher/disposal helper. Capture a pending task, observe the interim state, release its barrier, then await completion; do not assert before a queued click executes. Keep state tests deterministic rather than adding sleeps.

Claim owner success only after independent read-back of the exact IDs and scope. Preserve negative authority/unknown outcome assertions. A fake seed callback is not native transaction proof. Scripted external model tests are real application/runtime tests with an excluded model, not paid live proof.

A full Stable checkpoint is expected if P1 relocates widely used public models or adds editor acknowledgement/admitted seed behavior. Trigger it once after the meaningful implementation freezes; if no such trigger actually applies, document the evaluated diff and use the current narrower policy. Do not repeat full Stable for unchanged docs/screenshots. Re-run affected broad proof after a genuine invalidating fix and keep original failed attempts.

A large-only product scope does not permit weakening required existing library tests. It avoids new breakpoint work and repeated small-screen campaigns. A missing external credential, model or generated application remains BLOCKED/NOT_RUN, never PASS. This bundle issues no fresh live request authorization.
