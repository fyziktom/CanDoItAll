# Collaboration validation and acceptance matrix

This is the module-specific companion to [the execution prompt](prompt.md) and [shared validation](shared/VALIDATION.md). It describes required evidence, not tests already run. All application builds, test runs, browser journeys and measurements in this assignment are **not run during prompt preparation**. Choose coherent test classes and implementation steps; the rows below are behavior obligations, not a mandatory file layout or one-test-per-row quota.

## Start with the actual checkout

Read current repository instructions, `docs/testing.md` and CI before modifying source or tests. Record branch/HEAD, relevant sibling revisions, SDK, dependency/asset mode and pre-existing changes. Use Code Analytics MCP plus source/reference inspection to identify impacted tests and schema/composition consumers. Follow current Components MCP guidance for the real components used. Record tool unavailability instead of inventing discovery evidence.

A contracts assembly move can affect a test that does not mention Collaboration in its name. Inspect solution membership, namespace/assembly consumers, migration model composition, project-transfer consumers, route discovery and the shell subscription. Do not widen by module-name guesses alone, and do not restrict the final test set to the two baseline classes below.

## Existing source-derived regression anchors

| Current class | Source count at reviewed commit | What it covers |
|---|---:|---|
| `CanDoItAll.Tests.Integration.Runtime.CollaborationIntegrationTests` | 4 `[Fact]` methods | Owner/schema mapping parity; historical records across restart and profile isolation; create/read-state persistence; automation ingress. |
| `CanDoItAll.Tests.Components.Shell.MainLayoutCollaborationTests` | 1 `[Fact]` method | Production MainLayout unread badge using the real owner/component harness. |

These counts come from source (C13/C14), **not executed test discovery**. Recompute expected counts if source changed or tests are added. Run `--list-tests` for each chosen filter, compare actual cases with the expectation, and then execute the same filter on current assemblies. An unexpected count, zero discovery, skipped required case or fixture failure invalidates that proof.

Example baseline commands from the repository root, after reviewing current source:

```powershell
$integrationSuite = './tests/Solutions/CanDoItAll.Tests.Integration.slnx'
$integrationFilter = 'FullyQualifiedName~CanDoItAll.Tests.Integration.Runtime.CollaborationIntegrationTests.'
$componentSuite = './tests/Solutions/CanDoItAll.Tests.Components.slnx'
$componentFilter = 'FullyQualifiedName~CanDoItAll.Tests.Components.Shell.MainLayoutCollaborationTests.'

dotnet test $integrationSuite --configuration Release --list-tests --filter $integrationFilter /m:1
# Check exit status and compare discovered cases with the current-source expectation.
# Continue only after discovery is valid and the test assembly is current.
dotnet test $integrationSuite --configuration Release --no-build --no-restore --filter $integrationFilter /m:1

dotnet test $componentSuite --configuration Release --list-tests --filter $componentFilter /m:1
# Perform the same discovery and freshness checks before execution.
dotnet test $componentSuite --configuration Release --no-build --no-restore --filter $componentFilter /m:1
```

These are staged commands, not an unattended script: check each native process exit code. Never run the next command as proof when the preceding build/discovery failed. New renderer, session, sandbox and browser filters must be discovered after the implementation; no new test name in this brief is claimed to exist.

## Build and boundary proof

Build the new/equivalent contracts and UI projects, the changed Collaboration module, the sandbox and affected production composition/Web projects. At the proposed paths, the direct entry points are:

```powershell
dotnet build ./src/Modules/CanDoItAll.Modules.Collaboration.Contracts/CanDoItAll.Modules.Collaboration.Contracts.csproj --configuration Release /m:1
dotnet build ./src/UI/CanDoItAll.Collaboration.UI/CanDoItAll.Collaboration.UI.csproj --configuration Release /m:1
dotnet build ./src/Modules/CanDoItAll.Modules.Collaboration/CanDoItAll.Modules.Collaboration.csproj --configuration Release /m:1
dotnet build ./src/Sandboxes/CanDoItAll.Collaboration.UiSandbox/CanDoItAll.Collaboration.UiSandbox.csproj --configuration Release /m:1
dotnet build ./src/App/CanDoItAll.Web/CanDoItAll.Web.csproj --configuration Release /m:1
```

New project paths above are a proposal, not existing resources. Adapt to actual accepted names; build any other affected production project as well. Register product projects in the product solution and tests in the appropriate test solution/project without pulling tests into `CanDoItAll.slnx`.

Prove both the evaluated transitive build graph and the actual runtime/rendered closure. The UI and sandbox must not require Collaboration implementation, EF/persistence, production Web/composition, provider/runtime registrations, Workbench or Scheduler. Contracts must not expose EF entities, DbContexts, service locators or implementation types. Imported package-to-sibling-project rewrites must be included. A missing assembly during traversal is not a successfully verified dependency boundary. Negative guard fixtures are useful; arbitrary component/interface/file-count assertions are not.

Preserve enum values and persisted string names, current read/request shapes and validation. Run the existing owner/schema parity proof after moving shared enums. No schema migration is expected: investigate an unexpected schema diff rather than approving it as an extraction side effect. Keep normal source dependency mode functional; a fallback to prebuilt packages does not prove the user's source/watch workflow.

## Required behavior coverage

| Case | Exercise and required observation | Appropriate proof |
|---|---|---|
| Full renderer closure | All three tabs, quick-create form, lists, selected detail/transcript, reply, badges, empty states and actual BaseLib descendants render through the new library in production and sandbox. | Renderer/components; real Web and sandbox browser. |
| Route and selection | Root route, valid `threadId`, nonexistent ID, tab changes and query changes while a request is in flight. Keep legacy query semantics; an old read cannot revert current selection or route. | Host/session tests; real production deep link and reload. |
| Filter-empty selection | Unread/all behavior on Inbox/Escalations; Threads remains the all-threads view. When a filtered list becomes empty, the owner's null-selection default must not silently activate an unrelated read thread. | Deterministic session/host tests; browser filter journey. |
| Request lifetime | Script A then B, A→B→A, late success, late failure and late completion, including a read that ignores cancellation. Dispose or leave the route before completion. No stale publish, leaked owned CTS, notification or busy-state overwrite. | Deterministic asynchronous tests with controlled completions, not arbitrary sleeps; host disposal. |
| Read failure versus absence | Initial load failure, missing target and valid empty data stay distinguishable. A refresh failure retains a same-scope accepted snapshot with visible stale/failure state and a read-only retry; it does not invent successful zeros. | Renderer/host/scenario tests. No new independent query APIs are required. |
| Draft and form lifetime | Same-target rerender/section changes preserve intended draft and validation lifetime. Target change follows a documented safe policy; old reply text cannot silently become another thread's message. Reset and Clear keep their intended distinct behavior. | Real form component tests, browser invalid/dirty form. |
| Mutation admission | Double-click, Enter plus click, stale rendered callback and two targets. Exactly one owner invocation per admitted local submission; unrelated forms are not globally blocked. Gate resets on every relevant outcome and respects retired lifetimes. | Host/state tests with controlled owner boundary; integration for actual persisted count. This is not a distributed exactly-once claim. |
| Edit during save | Either prove that the affected form is disabled while admitted, or prove submitted-snapshot reconciliation preserves later edits. Switching targets while pending must not clear, navigate or select the successor from the old completion. | Real renderer/host tests with delayed completions; representative browser behavior. |
| Owner semantics | Create notification/escalation, reply, mark-read, not-found/validation refusal, already-read no-op and automation ingress retain current identities, unread behavior and stored text. Local reply still uses `MarkAsUnread: false`. | Real owner and isolated PostgreSQL integration; product form actions and read-back. |
| Save then refresh failure | Successful command followed by read failure stays visibly saved with a refresh warning; create identity is retained. Retry invokes reads, never blindly resubmits create/reply. Preserve draft and provide an explicit recovery state for a genuinely unknown result. | Host failure-injection plus real-owner commit proof. Do not claim an unavailable message receipt/idempotency protocol. |
| Post-commit observer fault | Attach a throwing synchronous `Changed` observer and a later valid observer. A normal owner write remains committed and accurately reported; valid observers still run and the failure is logged. Inspect the actual shell async refresh path and disposal too. | Real owner/isolated PostgreSQL test with a normally committed operation; focused shell regression. Preserve ambient-transaction ownership. |
| Shell and linked context | Badge updates correctly after relevant actions, subscriptions are cleaned up, `/scheduler` and legitimate linked context routes remain usable. Escaped message rendering is retained. | Current badge test plus changed behavior; production browser and source review. No scheduler/process job is needed. |
| Existing durable compatibility | Four-entity mapping parity, existing records/restart, GUID-token behavior, profile isolation and automation signals remain valid after type movement. | Current integration baselines and actual affected migration/composition consumers. Do not infer optimistic concurrency enforcement. |

Use renderer-only fakes for the layer they intentionally isolate. They cannot replace the real owner in persistence/commit claims. Test the new host and real feature descendants instead of stubbing away the heavy child that the extraction was meant to remove.

For bUnit, follow current repository async rules: await `ClickAsync`; find and dispatch together on the renderer dispatcher where necessary; wait for form readiness, not just a heading; use `DisposeRenderedComponentsAsync` where applicable. Avoid blocking owner reads inside `WaitForAssertion`. For delayed operations, keep the event task, assert pending state, release the operation and await completion.

## Browser journeys and resource safety

Run Playwright against an owned sandbox process and an owned isolated production Web process. Use the repository's supported large-desktop viewport and real assets, not mobile redesign work. Record exact viewport, routes, actions, assertions, console/network/server findings and evidence paths.

The production success journey must create a notification and escalation through the actual UI, open/select them, reply, observe the unread/badge changes, mark an unread item read, filter unread/all, and reopen a durable deep link. Verify stored result through the owner/test fixture and preserve the existing restart regression. Exercise invalid form submission and a target transition with an unsent draft. The sandbox journey must cover deterministic loading/failure, delayed action and saved-with-refresh-warning scenarios. Failure injection stays in test seams/scenario hosts, never as public production query switches or fake production DI.

Use the documented explicitly isolated PostgreSQL 18 endpoint via `CANDOITALL_TESTS_POSTGRES_CONNECTION`. Record server version and sanitized endpoint. Do not reuse the ordinary application on 5032, retained developer databases, provider data or unrelated watch processes. Do not print connection secrets, weaken cleanup assertions or substitute InMemory while claiming PostgreSQL proof. Missing infrastructure blocks that particular lane; report it separately while completing independent work.

## Development-loop evidence

Before changing the working implementation, record the original Web baseline when available. After extraction, record changed Web and the lightweight sandbox under comparable, stated conditions. Do not roll back unrelated work or require a forced checkout of the preparation SHA to get a baseline.

For each available host/mode capture evaluated graph, `dotnet watch --list` file set, startup and edit-to-visible samples. Use actual Razor and presentation C# edits and actual CSS/JS where present. Report repetitions, cold/warm setup, Hot Reload versus browser refresh versus process restart, machine/SDK and exact source/sibling revisions. Missing baseline or unavailable timings mean `not measured` or a bounded one-host observation, never an invented speedup.

Parity must render the actual production theme with correctly generated/scanned assets. A linked CSS content file is not a Web compile dependency. If Fast mode is implemented, verify its independent generation, outputs, selected-mode validation and representative visual behavior. Do not benchmark stale package code or hide renderer sources from watch to obtain a smaller number.

## Closure gates and receipt

Run current portability-static with the reviewed repair/baseline procedure in [shared validation](shared/VALIDATION.md), covering new files as well as tracked files. Inspect intentional baseline changes and finish enforcement without `--write-baseline`. Run the maintained documentation gate after README/architecture/testing changes. Check every exit status.

Expand validation for actual public-type, schema/composition, shared-component, asset or route changes and current named Stable/platform triggers. Do not run all provider/LiveProcess tests, all Stable or unrelated mobile journeys merely because a step ended. Conversely, a contracts or owner-boundary change cannot be excused from its affected integration tests because a sandbox passes.

The final receipt should map the above obligations to concrete commands/tests/evidence, using `passed`, `failed`, `blocked`, `not run` and `not applicable` accurately. Include current start/final HEAD, changed responsibilities, intentional behavior corrections, observed versus unmeasured watch results, mandatory gates and any remaining debt. A clean architecture sketch or successful package validator is not evidence of a working application extraction.
