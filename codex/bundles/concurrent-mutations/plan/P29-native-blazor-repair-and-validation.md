# P29 — Native Blazor repairs and concurrent editing proof

Status: in progress. Proof tier: Behavioral, with retained real-host/process evidence.
This is a focused successor to P28. It does not close the remaining refresh-delivery work.

## Owned user input

> ok. lets focus on our standard blazor UI now and repairs and proper testing.
> first I tried to run process **Run 1657350f** and it got into escallation. it used to be able to run e2e without escallations. it is simple app development (it must be generic for any type of app, not just the example we use).
> then I need you to try all actions in the project structure and gantt including attaching assets, reading those infos, etc.
> repair bugs on the way.
> then you must also test the concurrency like, there is running process or chat with floating agent and same time you add something via UI into project structure, etc. we need to be sure it is working correctly via our standard UI.

## Requirements and progression

| Requirement | Acceptance and proof | State |
| --- | --- | --- |
| BUI-1 | Diagnose the reported run; repair generic engine causes; prove the normal process path through runtime validation and result recording, with receipts for existing effects before any rework. | Investigating; scaffolding defects reproduced |
| BUI-2 | Inventory every action exposed by native Project Structure and Gantt; exercise each applicable action, assets and information readback; record disabled prerequisites and repair failures. | Pending |
| BUI-3 | Use a real process and floating agent chat while editing the same project through native UI; verify non-conflicting writes survive, stale writes are handled explicitly, drafts and assets remain correct after reload. | Pending |
| BUI-4 | Keep tests, docs and incremental signed commits current; preserve the SPA review branch and ordinary data. | In progress |

Work order: generic process repair and focused regression proof; native action inventory
and repairs; overlapping process/chat/browser proof; final static checks and closure.
Browser checks use the existing ordinary host and explicitly owned manual test records.
Automated database tests use the retained isolated PostgreSQL fixture, never the ordinary
database. No global cleanup, deployment, or SPA merge is part of this work.

## Initial evidence and first repair design

Run `1657350f-61a0-4acc-95de-37a6ba26bfa9` is durably Failed, with zero escalations.
Its first step completed. Implementation execution
`5e59661a-cf57-488f-b481-a17e63cd021e` performed 65 tool calls and submitted a valid
typed completion, but completion assessment retained an unresolved `workspace_dotnet_new`
failure. Three scaffold calls were recorded: solution creation succeeded; an application
under that solution root was prohibited; a `name="."` alternative was rejected as invalid.
Subsequent file/build/test/publish effects must be reconciled before rework.

`WorkspaceCommandPlanBuilder.BuildDotnetNew` treats a top-level solution file as an
existing project when creating an application. It also declares a child target but passes
only `-n`, without an output directory. A local SDK `--dry-run` with a generic console
template proves that this writes `./Program.cs` and `./<name>.csproj` into the parent.

The smallest coherent repair stays with the command planner:

1. Keep the parent-project prohibition, but allow child projects under a solution root.
2. Explicitly pass the child output directory for non-solution templates, keeping solution
   files in their declared parent and retaining all path/overwrite guards.
3. Classify `.` and `..` scaffold destinations as prohibited operations with proven no
   effect, directing callers to repair existing projects or choose a named child. Other
   invalid input remains a correctable rejection. Do not weaken global completion rules.

## C# architecture impact

Ownership remains `AgentFramework.Core/Workspace/Commands`; typed effect evidence remains
owned by the existing execution/effect contracts. No new project, reference, interface,
partial class, composition registration, or dependency on a product example is needed.
Changing only the parent guard was rejected because it would allow the CLI to write outside
the declared child target. Ignoring all failed mutations was rejected because unknown or
unresolved writes must continue to block success.

CodeAnalytics baseline `snap-20261024123248-fe315e8b` loaded one Core project, 266 documents,
804 types and 5,945 members with no diagnostics. The scoped graph reports three existing
type cycles and no project cycles. This repair must introduce no dependency changes;
the existing cycles are not a reason to expand this behavior repair into a Core refactor.

## Testability and gates

First repair selection:

- Unit: `FullyQualifiedName~WorkspaceCommandExecutionServiceTests.DotnetNew`, including
  child output, solution-root admission, existing-target refusal, prohibited dot paths,
  and ordinary invalid-input rejection. Build-backed discovery froze 21 cases.
- Host integration: a dedicated `WorkspaceDotnetScaffoldingTests` class using the real
  SDK and local process host. Verify solution plus at least two distinct project templates,
  actual output paths, unchanged solution root, and refusal to overwrite a scaffold.
  Build-backed discovery froze two cases (`console` and `blazorwasm`).
- Completion regression: `FullyQualifiedName~ToolOutcomeCompletionIntegrationTests`;
  no-effect prohibition must not erase uncertain effects or ordinary unresolved mutations.
  Build-backed discovery froze 17 cases.
- Direct Core build and refreshed owning Unit/Integration test assemblies in standard
  Debug/Release outputs, serialized against managed watch. No unfiltered suite is justified
  by this local planner contract; expand only if a newly affected owner requires it.
- Final portability scan includes untracked protected files, reviewed deltas and passing
  enforcement without baseline write; rendering register and docs checks remain required.

Native UI proof targets 1920×1080. Preserve the existing structure/Gantt primary surface,
editor/dialog ownership and scroll behavior; inspect normal and relevant overlay states.
API reads support receipt verification but do not substitute for native UI interactions.
Add focused component/owner tests for bugs found during BUI-2/BUI-3, naming their filters
and expected discovery before execution. Entry gate passes for the first repair only;
later lanes need their concrete action inventory and prerequisite readback before writes.

## Scaffold repair checkpoint — 2026-10-24

The Core Release build and the owning Unit/Integration Release builds pass. All 40
selected cases pass: 21 unit cases, two real-SDK host cases and 17 completion regressions;
none were skipped. Existing unrelated compiler/analyzer warnings remain. The host tests
removed their unique temporary workspaces after execution.

Forced post-edit CodeAnalytics snapshot `snap-20261024124337-fe315e8b` has no diagnostics.
All 1,074 dependency edges (including their kinds and weights) match the baseline, with
the same three type cycles and no project cycles. No module boundary changed.

The portability scan included untracked files. Final enforcement passes with 15,709
reviewed executable-source findings unchanged; no baseline refresh was needed. The
baseline, artifact-secret and rendering validator tests pass (6 + 4 + 7 cases), the
rendering register passes for 1,212 inputs, and documentation validation passes for 438
maintained Markdown files. Logs and TRX files are retained under the ignored proof root.

The ordinary managed host was stopped only after confirming there were no active runs,
then restarted in standard Debug with its explicit 5032/7271 endpoints. These automated
checks establish the scaffold repair; they do not close process E2E or native UI proof.

## Retained evidence and resources

Ignored proof root: `.artifacts/concurrent-mutations/blazor-ui-repairs-20261024`.
It contains ownership and the reported process/agent records. Keep raw application data
out of commits; commit the reasoning, generic tests and bounded validation results.
The ordinary application is managed by the shared DotNetWatch backend. Retain it for the
user after testing. Keep SPA on `codex/concurrent-mutations`, without source edits or merge.

Closure: BUI-1, BUI-2 and BUI-3 are not yet proven. Historical cleanup denials and open P28
work remain recorded in the preceding validation documents.
