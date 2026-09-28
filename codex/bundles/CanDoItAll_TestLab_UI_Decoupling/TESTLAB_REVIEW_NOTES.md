# TestLab: source map, consequences and implementation risks

This is a design review against HEAD `97989b9d13b9a6fa16280da98ec5b005a2f968c7`, not a runtime diagnosis or a forced execution base. Source references resolve through [SOURCES.md](SOURCES.md). The current checkout must be re-inspected before implementation.

## Selection and bounded scope

TestLab is the next suitable small-to-medium **whole workspace** after Collaboration: one routed plan editor with a list and four sections, backed by an already bounded owner. It is more involved than Collaboration because it edits nested records and has admitted project references, but it does not require extracting an orchestration runtime. The current sandbox directory contains Agents, Collaboration, CRM/HR and Prompts, not TestLab. [D01](SOURCES.md#d01)

This review does not claim to have re-audited every remaining module. The previous small-first plan remains reasonable, and the actual TestLab source now justifies this choice. Processes and Workbench remain outside this assignment. Workbench **consumers** may require compilation/regression checks when their TestLab DTO dependency changes; that is not a Workbench UI extraction.

### Feature preservation map

| Existing surface | Preserve | Do not expand into |
|---|---|---|
| Plans list/header | Total/visible/case/evidence statistics; ordered plan selection; text, project, phase and latest-result filters; reset filters | General server paging redesign or repository-wide dashboard framework |
| Overview | Project/None, responsible party/None, phase, title, coverage goal, Playwright spec path | CRM editing, permission redesign, upload or runner launch |
| Cases | Existing add/edit fields and status values | A new test execution engine |
| Evidence | Existing add/edit label, path, kind and notes | Uploading bytes or validating arbitrary user filesystem paths |
| Runs | Existing add/edit recorded timestamp, runner, result and summary; latest recorded result | Automatic task acceptance or actual execution |
| Host operations | New, Reset, select, save/update and durable `planId`/`projectId` links | New delete API, new HTTP control plane, global navigation framework |

The four EF entities, search/activity integration and projections already have owning semantics. Keep them. [T02–T04](SOURCES.md#t02)

## Current ownership and proposed movement

| Source/type family | Current state | Expected treatment |
|---|---|---|
| `TestLabPage.razor` | Markup and asynchronous state/owner calls combined; directly references Projects implementation types | Keep the routed host in the module; move real rendering to feature UI and isolate per-page state/effect lifetimes |
| `TestCaseStatus`, `TestPlanSummary`, parent/child editor DTOs | In the same assembly/file as EF and `TestLabService` | Move genuine shared types to light TestLab.Contracts, keeping source/serialization compatibility |
| `ITestPlanChildEditor` | Internal interface consumed by DTOs and backend generic synchronization | Deliberately resolve cross-assembly visibility; do not drag EF source into Contracts |
| Entity types, EF configurations, `TestLabDbContext` | Owner/complete-schema mapping responsibilities | Stay backend; no mapping or migration rewrite |
| `TestLabService` | List/detail/save, exact project admission, transactional children, postcommit search/activity, typed projection facts | Remains production owner; adapt only a genuine UI/test boundary if necessary |
| `ProjectWriteAdmission` | Already in Projects.Contracts with SharedKernel only | Reuse unchanged and preserve owner-issued values |
| `ProjectWriteSelection`, `ProjectPartyOption`, party bridge | Pure values mixed into Projects implementation files | Map minimal UI/reference values in host, or narrowly move only justified stable pure types; never expose full bridge in renderer |
| `TestLabModuleAssemblyMarker` and DI | Scoped TestLabService; pooled owner factory; transfer participant; production assembly catalog | Preserve registration/route/mapping discovery; add only needed UI references/adapters |

[T01, T03, T05–T09, T12](SOURCES.md#t01)

The feature contract can reference existing Projects.Contracts when that carries a real domain fact. A dependency named `Contracts` is not automatically light: check evaluated transitives in actual source mode. Conversely, using a justified lightweight cross-module contract is not the same as depending on that module's implementation.

## Critical owner invariants already implemented

`TestLabService.SaveAsync` does not simply call a generic repository. It validates the project's captured admission, takes deterministically ordered project/plan mutation locks, checks that a previously read plan's project binding has not changed, writes parent and all three child families in the owner transaction, then commits. Submitted parent/child IDs are updated only once the owner knows the commit succeeded. Search and activity follow the plan commit. A postcommit exception is wrapped in `TestPlanCommittedSaveException`, preserving the plan ID. [T03](SOURCES.md#t03)

That distinction matters: project retirement can occur between the plan commit and search staging's separate lifetime recheck. The plan is still saved even when the later call fails. A UI-side retry must not re-create the plan or its children.

Global and historical reads retain stored nullable project references. `GetAsync` reconstructs admission from the **stored plan binding and owner profile**, not by silently capturing a fresh incarnation of today's project. Current project labels/filters use lifetime equality. Plans remain after project retirement, and deliberate selection of a new current project can rebind a historical plan. Keep this behavior rather than auto-clearing missing references. [T02–T04, T07](SOURCES.md#t02)

No application-managed optimistic concurrency token exists for these records in the reviewed model. Do not add one merely to mirror another UI module. Admission is not a record version, authorization grant or bearer token. [T04, T07](SOURCES.md#t04)

## Source-derived UI defects/risk paths to prove during extraction

These are concrete reasons not to mechanically move the current `@code` block. They were not reproduced by this reviewer.

### T-R1: same-target edits can be replaced by successful save read-back

`SaveAsync` deep-clones the submission correctly, but its final `IsCurrent()` checks editor reference, project ID and admission only. If the operator edits title, notes or a child while the write/read-back is pending, those still match; assigning `editor = loadedEditor` can erase the new text. Preserve field-level newer work against the submitted snapshot and prove the actual browser/input path before blur. [T02](SOURCES.md#t02)

### T-R2: no per-editor duplicate admission gate

The reviewed form/button and `SaveAsync` do not have a save admission gate. Two events before a new plan ID is adopted can submit two snapshots with a null ID. The server can create two plans because this is not a server-idempotency protocol. Establish one dispatch per editor lifetime while keeping independent UI interactions available. A test must assert owner call count and persisted aggregate count, not just the button state. [T02–T03](SOURCES.md#t02)

### T-R3: route and lookup completions are not generation-fenced

`OnParametersSetAsync` always reloads and resets the editor/tab. Plan selection and responsible-party options await external reads before assigning shared mutable fields. The options method reads the current editor again after awaits. Out-of-order A/B/A reads, repeated same parameters, or a project change during a party lookup can overwrite newer state or carry an option from the wrong scope. Capture target/generation and maintain same-scope stale/error state. [T02](SOURCES.md#t02)

### T-R4: same IDs are not the same editing intent

The existing save guard compares values, so selecting A -> B -> A on the same mutable draft is not automatically a new save origin. The next design must explicitly decide which operations retire an admitted target while preserving the existing ability to edit/reset/select a project during a write. Do not borrow an old completion's IDs merely because the visible IDs match again. [T02, T10](SOURCES.md#t02)

### T-R5: typed model state is not all form state

There is currently a real `EditForm`, but the reviewed editor types have no DataAnnotations and the markup has no DataAnnotationsValidator. Title refusal is enforced by the owner; field conversion still matters. Do not assert that old annotation validation existed, or inadvertently bypass actual input parsing by switching to an OnClick-only fake form. Preserve invalid/raw input and `EditContext` lifetime through tabs and background reconciliation. Adding client feedback for existing owner constraints is reasonable; inventing stricter evidence/runner semantics is not. [T02–T03](SOURCES.md#t02)

## Existing tests that must not regress

`OwnerPostcommitPageTests` is especially important after the development merge. It uses a real component harness and owner faults. TestPlan cases verify: [T10](SOURCES.md#t10)

- Search/activity failure after commit preserves the **same editor**, plan ID and all three child IDs; an explicit later save updates the existing aggregate.
- A failed refresh or missing saved row still retains confirmed identity and warning, without a false success notification.
- A late known-commit warning can identify the original operation without replacing a new editor/project selection.
- Changing project while the owner's flush is paused does not copy the original plan or child IDs into the new project state.
- Replacing a child with an equal-looking new object while save is pending does not assign the original child's returned ID to that new object.
- Missing persisted children may receive replacement IDs; those exact new IDs must survive a postcommit failure and explicit resave.
- Retiring/recreating the same project ID refuses the old lifetime, with no postcommit search/activity work.

Do not solve the new races by deleting these tests, forbidding all edits while saving, or changing a warning into a generic unknown result. Update test wiring through the actual form/view seam as needed; preserve semantics.

`TestLabOwnerPersistenceTests` covers exact four-entity model parity, rejection of foreign entity queries, historical restart/read-back/edit identity and owner-profile isolation. Read and discover the full current class and relevant cross-module tests before running; the review inspected its first 230 lines rather than claiming execution or exhaustive test discovery. [T11](SOURCES.md#t11)

## Performance and coupling consequences

The lightweight renderer and scenario host should remove the production backend closure from the **sandbox**, not make the production host stop owning its runtime. Preserve source-mode development rather than hiding project references with `Watch=false` or selecting stale packages. A smaller graph is evidence of isolation, not a timing result. [G03, E04](SOURCES.md#g03)

Keep text/phase/result filters local over the accepted list as currently implemented. Do not reload TestLab, Projects and parties on every keystroke or tab click. Map reusable lookups once per accepted snapshot/filter change rather than repeatedly sorting/copying the entire editor during render. Snapshot nested lists on submission/reconciliation, not on every render. Use an explicit large scenario to catch avoidable allocation/render regressions.

The owner currently materializes plans/runs and aggregates counts in `ListAsync`. This is a genuine future scaling consideration, but changing its query protocol or adding pagination is not necessary for this extraction. Record it instead of widening the assignment. Similarly, the project's full write fence is a backend invariant, not a reason to introduce new UI database checks on each input. [T03](SOURCES.md#t03)

## Compatibility and change control

Keep namespaces and public payload behavior stable where practical; changing an assembly still requires rebuilding consumers even with the same namespace. Check every serializer, source/assembly scan, DI registration, projection/agent consumer and test solution affected by moved types. Update only necessary compatibility references; do not introduce production registration in the sandbox to solve a missing dependency.

Keep development commands and findings in maintained module docs. Temporary source hashes in this bundle identify evidence, not permanent execution pins. User data, the existing running application and unrelated branches/changes remain untouched.
