# Implement the next UI decoupling slice: TestLab

You are the senior C#/.NET and Blazor engineer implementing this change on the user's actual CanDoItAll checkout. Complete working production wiring, the real standalone sandbox, focused regression proof and maintained documentation. Do not stop at a proposal. Use judgment about class boundaries and the implementation sequence; there is no interface, file, line-count or commit-count quota.

## 1. Assignment, authority and starting point

Implement the **entire existing `/test-lab` workspace**: plan list and filters; Overview, Cases, Evidence and Runs; new/edit/reset/save; project and responsible-party references; existing `planId` and `projectId` navigation semantics. The purpose is a substantially lighter UI-development host using the same renderer as production, not an API-only architecture.

Also perform the bounded **S0 conversation-shell lifecycle repair** described below, preferably as its own signed logical commit. It is a known shared production-host prerequisite found during Collaboration proof, not an instruction to refactor Conversations, Agents or Simple Chats.

The review used `fyziktom/CanDoItAll`, branch `components-decoupling`, HEAD `97989b9d13b9a6fa16280da98ec5b005a2f968c7`. Its first parent contains the Collaboration repair commit `f50c958c3ac9df2ec9df43a76963a8753178735f`; its other parent brings in development. These are provenance only. **Do not checkout/reset/rebase to those SHAs.** Record actual branch, start HEAD, worktree changes, SDK, dependency mode and the relevant sibling revisions before work. Re-evaluate paths, symbols and consumers that have changed since the review.

Read this complete extracted package, particularly [the TestLab review](TESTLAB_REVIEW_NOTES.md), [the validation matrix](VALIDATION_MATRIX.md), [Collaboration closure](COLLABORATION_REVIEW.md), [measurement protocol](DEV_LOOP.md) and [shared v3](shared/prompt.md). Read the current `AGENTS.md`, `.github/copilot-instructions.md`, `docs/architecture/ui-component-seams.md`, `docs/testing.md` and `.github/workflows/ci.yml` in the checkout before editing. Current canonical repository rules outrank historical bundle examples. Do not copy the temporary bundle into permanent architecture documentation or make the build depend on it.

Use the actual Code Analytics MCP for symbol references and affected-test discovery when available. Corroborate its recommendations with code, evaluated project metadata and test discovery. For non-WebGL shared components, use the Components MCP to inspect recommended components and their real contracts. If either is unavailable, record that fact and use local source/build inspection. Never claim an MCP call, benchmark or test that did not run.

Keep all source, comments, tests, UI text, permanent documentation and commit messages in English. Your final owner-facing report may be Czech.

## 2. S0: close the known shell disposal race without a shell redesign

Inspect the **current** `src/UI/CanDoItAll.Conversations.Shell/ConversationShellHost.razor.cs` and `tests/Components/CanDoItAll.Tests.Components/ConversationShellHostTests.cs`. The reviewed host starts `InitializeContributorsAsync` without awaiting it; the loop reads `lifetime.Token` separately for each contributor, while `DisposeAsync` cancels and disposes that source. A delayed contributor can finish after disposal and let the loop access a disposed source or initialize the next contributor. Event callbacks also check disposal before enqueueing rather than necessarily inside the dispatched continuation. See [S01–S02](SOURCES.md#s01).

First write a controlled-completion regression: hold the first contributor incomplete, let it deliberately ignore cancellation, dispose the rendered host, then release it. Assert no next contributor is initialized, no post-disposal render/state update reaches a replacement host, no disposed-source error is logged, and no task fault is left unobserved. Cover cancellation-aware completion, queued notifications crossing disposal, and genuine initialization failure while the host is alive. Use the repository's `DisposeRenderedComponentsAsync` and dispatcher conventions; do not create a false disposal test with the known bUnit root-disposal race.

Make a small ownership-correct repair: acquire any required token while its source is alive, check the relevant lifetime after suspension and within queued callbacks, observe the initialization operation, detach subscriptions, and dispose resources at a safe point. Merely catching every `ObjectDisposedException` or changing a log assertion is not a repair. Preserve actionable diagnostics for genuine contributor failures. Do not block the renderer or wait indefinitely in disposal for a contributor that ignores cancellation. Capturing a token is not, by itself, permission to initialize more contributors after shutdown.

Build the changed shell project and its affected host consumers; discover and execute the shell regressions and existing behavior tests. Re-run the production Collaboration browser journey because it exercises the shared shell. Keep the already-correct Collaboration draft and sandbox fixes. Do not restart Collaboration extraction or require all of its historical tests after every TestLab edit.

If current code already contains an equivalent verified fix, record that and do not duplicate it. If a wider unrelated defect emerges, isolate and report the blocked production dimension, continue independent TestLab work, and do not label that dimension green. A successful one-off browser rerun is not a substitute for the deterministic S0 regression.

## 3. Inventory and baseline before moving the renderer

Read the actual `TestLabPage.razor` and its entire rendered closure. Inventory the route host, descendants, deferred sections, form controls, imports, assembly markers, service registrations, Tailwind inputs, scoped CSS, scripts and static assets. Inspect all consumers of the types that will move. Relevant starting sources are [T01–T12](SOURCES.md#t01), but they are not a substitute for current reference discovery.

The review found one page using BaseLib layout, tabs, selection list, form sections and sticky actions. It directly injects `TestLabService`, `ProjectWriteSelectionQuery`, `IProjectPartyIntegrationBridge` and `NotificationService`. `TestLabModels.cs` combines DTOs/status with EF entities, configurations and the service. A UI interface that still exposes types from that implementation assembly would not decouple the build graph.

Capture the actual pre-change Web development-loop baseline and the relevant project/asset graph according to [DEV_LOOP.md](DEV_LOOP.md). Do not stop or rebuild over the user's existing port-5032 application. Use owned test processes and safe isolated build outputs/configuration. Never invent a pre-change measurement after the code has already moved.

## 4. Target architecture: light renderer, existing production owners

Recommended project placement, subject to genuine equivalent projects already present:

```text
src/Modules/CanDoItAll.Modules.TestLab.Contracts
src/UI/CanDoItAll.TestLab.UI
src/Sandboxes/CanDoItAll.TestLab.UiSandbox
```

Production should remain:

```text
Web / production composition
  -> TestLab routed host + per-page state/session and narrow adapters
     -> existing TestLabService, Projects owner query, party bridge
     -> TestLab.UI renderer
        -> TestLab.Contracts + justified existing lightweight contracts + BaseLib

TestLab.UiSandbox
  -> the same TestLab.UI renderer + deterministic scenario state
     -> the same lightweight contracts + BaseLib
```

A cohesive workspace-view contract is a good fit for this editor; presentation records are still appropriate for small read-only pieces. Do not enforce one abstraction everywhere. Keep each state authoritative in one place. The routed host owns meaningful selection, route application, reads, writes, notifications and external lifetimes. The renderer owns markup and browser-local interaction. A host-owned draft includes its `EditContext`; the renderer may create/manage that context only if it remains tied to the same draft instance with the same ownership semantics.

Expose typed intents with their rendered editor/row/selection origin, not a service bag, raw `IServiceProvider`, EF entity, `IQueryable`, module service or an index into a later mutable collection. A narrow testable read/write port is appropriate when it represents a real boundary, not because every helper must have an interface. Do not store mutable page sessions as shared circuit-scoped or singleton state.

### Contracts and compatibility

Extract only genuine lightweight shared types from the mixed TestLab file: the status enum, summary and editor models and any minimal supporting contract actually required. Preserve public namespaces, property meanings, enum numeric values, defaults, serialization behavior and the `[JsonIgnore]` project-lifetime summary property unless an independently justified compatibility change is required and proved. Keep EF entities, mappings, DbContext, transaction helpers, projection owners and transfer maintenance in the backend.

`ProjectWriteAdmission` already lives in `CanDoItAll.Modules.Projects.Contracts`, whose reviewed graph depends only on SharedKernel. Reuse it unchanged. Do not create a weaker `{ projectId }` substitute or a second ownership token.

`ProjectWriteSelection` and `ProjectPartyOption` currently live with Projects implementation contracts, not necessarily in the light assembly. Prefer small TestLab UI/reference presentations mapped by the host, carrying only the values this UI actually displays/needs, including the exact owner-issued admission for project selection. Moving a genuinely stable pure Projects contract to its existing contracts project is an alternative only after checking all affected consumers. Do **not** move the whole party-integration bridge or its unrelated assignment API to satisfy one dropdown, and do not leak contact email/phone data into a picker that only renders a name.

The reviewed `ITestPlanChildEditor` is internal but used both by editor DTOs and the owner's generic child synchronizer. Resolve that accessibility deliberately when splitting assemblies: a justified minimal shared identity abstraction or an owner-local generic/delegate adaptation are acceptable. Do not source-link the entire mixed EF file, create duplicate DTO identities, or reverse-reference the implementation to make it compile.

Keep the existing module assembly marker and production route discovery valid. Update product/test solution membership, required project references, CI test discovery/shards and assets only where needed. The sandbox need not enter the product solution; follow the existing explicit sandbox build/browser integration convention. No TestLab schema change or new migration stream should be needed.

## 5. Preserve the complete workspace and its reference semantics

Keep the existing fields and actions across all four sections, list statistics, case-insensitive text/phase filtering, latest-result filtering, project filter and plan ordering. A recorded run is data; this task does not execute Playwright or any universal test runner. Evidence is stored metadata/path, not file upload or arbitrary filesystem access. Preserve status/default values and date/time behavior; do not silently truncate historical timestamps when rendering a date input.

Handle route application by semantic intent, not every repeated `OnParametersSetAsync` call. Reapplying the same route must not wipe drafts, validation or the active section. An explicit new plan, Reset or selection of another plan retires the prior editing lifetime according to a documented policy. Section changes and list filters do not silently replace an editor. Preserve existing `planId`/`projectId` link behavior, including a plan's stored binding taking precedence over unrelated creation context. Do not add a large route-state framework or confidential draft text to URLs.

Distinguish initial loading, successfully empty data, a filter with no matches, a requested plan that no longer exists, and an unavailable/stale result. `TestLabService.GetAsync` currently returns a new empty editor for a missing ID; the host can classify an explicit missing request by identity mismatch without changing the service contract for all consumers. Do not accidentally present a missing saved plan as a new editable record that can be submitted to create a duplicate.

Use independent request lifetimes for genuinely independent reference reads. Selecting plan A, B and A again must reject the first A's obsolete completion, error and finally block. Apply the same discipline to project/party options, including a response ignored by cancellation. Capture the editor identity, project ID and applicable generation before awaiting; never resume against whichever mutable editor happens to be current. Cancel and dispose retired **read** resources safely, without using read cancellation as proof that a dispatched write did not commit.

### Project and party references are not cosmetic dropdown values

Preserve the admission captured by the displayed project selection or stored plan: database profile, project ID and lifetime ID. Never silently recapture a fresh admission during save or a background picker refresh. A retired/recreated project with the same public ID must not inherit a stale editor's write. Global plans explicitly use both null project and null admission; `Guid.Empty` is not global. An explicit new project selection may intentionally rebind an editable historical plan through the existing owner behavior.

Global/history reads retain orphan or retired plan references; current-project filters and labels must not mistake an old lifetime for today's project with the same ID. Preserve current lifetime-aware filtering and avoid accidental display-based reauthorization.

The current responsible-party loader resolves a saved selection missing from the current option list through `GetPartyOptionAsync`. Preserve that behavior without mixing responses from two editors/projects. If the stored reference cannot be resolved, retain the stored ID and show unavailable state rather than silently setting None. Failed option loading is not an empty successful list. Do not weaken backend policy, reveal extra party data, or add CRM assignment/quick-create features.

## 6. Make the editor lifetime and save outcomes correct before copying them

The current page already deep-copies its submission, preserves committed parent/child IDs, and catches `TestPlanCommittedSaveException`. Keep those guarantees. However, its successful refresh can still replace an editor that received newer same-target edits; it has no explicit same-draft submission gate. Reproduce these source-derived risks before repairing them. Existing owner/postcommit tests are semantic baselines, not obstacles to remove.

### Submission identity and admission

Admit at most one outstanding save for one editor/target lifetime, covering Save, Enter and any alternate callback. A second event must not submit a duplicate create or concurrent update. A retired lifetime must not hold an unrelated successor editor hostage. Do not implement this by globally disabling unrelated views or preventing all editing/navigation for the duration of a write.

Capture a deep, stable submission including parent ID, exact project admission and child origins before any asynchronous work. The live editor must not be handed to a service that will mutate it while the user continues typing or changes project. Track a genuine target/intent generation as needed: equality of project ID and admission alone does not distinguish A -> B -> A.

The current backend assigns IDs to the submitted copy **after commit**, including replacements for no-longer-existing child IDs. Preserve that information separately from whichever editor is currently visible. No callback from a retired/rebound origin may install the original plan or its children into another project/editor.

### Accepted values versus newer edits

Compare the live draft with the values **submitted**, not with its original load and not merely with an overall dirty flag. After known success and read-back, accept owner-normalized fields the user has not changed since submission; retain newer text and field/parse validation for changed fields. Keep the draft instance and `EditContext` when it contains newer work. The same applies to tabs and list filters changed while the save is pending.

Reconcile nested Cases, Evidence and Runs by captured row origin and durable identity, not display text or the current array position. Equal-looking rows are not interchangeable. Owner sorting may differ from UI order. Rows added after dispatch remain unsaved and retain their local identity; removed/replaced original rows must not receive a returned ID. Unchanged original rows must receive actual committed IDs so the next explicit save does not delete/recreate them. A persisted child missing in the DB can receive a replacement ID from the owner; do not keep its stale old ID.

Exercise actual input events **before blur**, not only test code assigning model properties. A browser's uncommitted text/invalid date string is still user input even when the last model value has not changed. Choose the smallest reliable field-binding/reconciliation design compatible with real Blazor inputs and BaseLib. Do not declare draft safety proved by a fake DTO-only test.

### Outcomes and recovery

Keep at least these distinctions explicit:

- Known refusal before commit: preserve editable data and validation; do not show saved state. Preserve a typed admission refusal as such instead of classifying every exception as an unknown database write.
- Known commit: retain the committed plan and child identities exactly once. Reconciliation is a read, never a replay of Save.
- Known commit plus search/activity failure, failed refresh or missing read-back: preserve identity and show a truthful saved-with-warning result. `TestPlanCommittedSaveException.TestPlanId` is affirmative commit evidence. The submitted copy may already contain the child IDs. Do not turn this into a failed-create retry.
- Genuinely unknown write outcome: preserve the submission/draft and prevent blind replay, with an explicit safe review/reload/recovery action. Without an existing owner receipt or idempotency protocol, do not promise that the client can infer an unknown commit from a similarly named plan. Do not invent a new durable protocol in this slice.

A user's later **explicit edit and save** of a known committed plan is valid; it is not the forbidden automatic replay. Distinguish that from a Retry refresh action. Keep truthful origin-specific notification receipts for known late commits where existing tests require them, while never relabeling a successor editor as saved or reopening retired state. Guard completion after page disposal and avoid dispatching through disposed services.

Do not weaken `TestLabService` transaction, lock-key ordering, project lifetime checks, child synchronization, search/activity boundaries, projections, migration ownership or existing cross-module behavior. Any small owner adaptation must preserve these invariants and its current external callers.

## 7. The sandbox must exercise the shipped renderer and meaningful failures

Use the real feature surface and real BaseLib descendants, not placeholder cards. The sandbox must run without TestLab/Projects/CRM implementations, Web composition, PostgreSQL, provider startup or a production service registration. Deterministic scenarios may implement the view/ports in memory. Their stored result and their selected editor are separate concerns: completing an admitted fake save can change the original fake record even after navigation without changing the newer view.

Cover initial/loading, empty, filtered-empty, realistic and large data; each of the four tabs; global/current/historical projects; saved-but-unavailable responsible party; failure and stale refresh; delayed plan and party reads; dirty state and validation; save refused, accepted, committed-with-warning and unknown; duplicate submission; delayed reconciliation with new typing; child identity assignment/replacement; and selection/disposal while work is pending. Scenario switching owns and retires all its waits and callbacks. Make pending operations deterministically releasable without production query parameters or sleep-dependent correctness.

Preserve production-equivalent assets in Parity mode, including BaseLib styles/fonts, application-specific CSS and Tailwind classes moved to the new library. A linked CSS **content** file is not a Web project reference. Do not introduce a Web reference to obtain CSS. Make any existing Fast-mode tradeoff explicit; prove parity before making performance claims. Add only necessary registrations and do not duplicate shared component source.

## 8. Proof and closure

Follow [VALIDATION_MATRIX.md](VALIDATION_MATRIX.md). Before every changed/new filter, derive the expected data-driven case count, run discovery, compare, then execute current built assemblies. A zero test count is not success. Start with changed production projects and the narrow owning tests; do not repeatedly run unfiltered large suites.

Preserve and execute affected real `OwnerPostcommitPageTests` TestPlan cases and `TestLabOwnerPersistenceTests`; discover relevant project-admission, CRM/HR, projection and other consumers rather than using a filename-only filter as the complete impact analysis. Maintain schema/model parity and stable ID persistence through restart. If contracts affect a wider set, build those consumers and execute the bounded affected tests or named broader gate required by current `docs/testing.md`.

Add deterministic state tests, real-form component tests, an actual production-host component path and real production/sandbox Playwright journeys at the supported large desktop viewport (the recent slice uses 1600 x 1000). Prove actual persisted plan/child IDs and subsequent edits through the owner, not only a toast or disabled button. Repeat navigation/disposal with shared shell checks; do not filter page, console, asset or server errors to conceal S0.

Use explicitly isolated PostgreSQL 18 via the current test procedure. Record sanitized provenance/version and clean up only owned resources. Leave `CANDOITALL_PLAYWRIGHT_BASEURL` unset for an owned fixture. Do not use or stop the user's app, ordinary database, unrelated Docker containers, watch/MCP sessions or sibling changes. If ordinary outputs are locked, use a supported task-specific project configuration/output strategy; do not alter shared configuration just to force a build.

Inspect the evaluated source-mode graph, transitive package/native assets, static web assets, startup behavior and `dotnet watch --list`. A parent-only injection test is insufficient. Boundary tests must fail on forbidden transitive dependencies and report unresolved references honestly. UI and sandbox must not transitively pull in module implementations or infrastructure. Measure the real edit-to-visible loop following [DEV_LOOP.md](DEV_LOOP.md); report samples and limitations rather than a promised speedup percentage.

Run the current mandatory portability-static procedure over protected files, including merged changes. Repair genuine defects, review intentional added/stale findings, inspect any baseline delta, and finish with enforcement **without** `--write-baseline`. Run relevant documentation/link checks and `git diff --check`. Read the actual CI files so newly added light test projects participate in owning test solutions and required shards rather than passing only manually.

## 9. Deliverables and stopping boundary

Deliver working code, production route integration, the backend-free TestLab sandbox, focused tests, and maintained docs. Record the chosen contract shape, runtime ownership left in place, project-lifetime semantics, draft/save behavior, sample scenarios, startup/watch commands, and actual graph/measurement evidence in a TestLab boundary record and local READMEs. Keep one canonical architecture guidance source; do not append a competing global rulebook.

Your final report must separate: implemented behavior; actual executed builds and test filters with expected/discovered/passed/failed/skipped counts; source-only conclusions; historical evidence; blocked/unrun dimensions; S0 result; asset/graph and dev-loop measurements; branch/HEAD/sibling provenance; owned resource cleanup; signed commit status. Do not call the work complete while required affected proof or portability enforcement is failing.

Local signed commits are authorized under the existing repository policy. Preserve the ordinary unlocked GPG agent/session; do not disable signing, store a passphrase, weaken security settings or rewrite unrelated work to obtain a commit. A signing block is reported separately from code/test status. No push, merge, rebase, PR, tag, release, package publication or next-module implementation is authorized.

Once this TestLab slice and its bounded S0 prerequisite are complete, stop. Processes, Workbench, Resources, Scheduler and Plugins are outside this run.
