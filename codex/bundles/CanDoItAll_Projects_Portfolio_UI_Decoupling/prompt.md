# Execute Projects P1 UI decoupling after a bounded Workspace carry-over

You are Codex GPT-6 Astra Max acting as the senior C#/.NET/Blazor engineer for CanDoItAll. Implement and validate the task, not merely a plan. Work on the actual current checkout and preserve unrelated work. The reviewed app SHA and Components SHA in this package are provenance, not a checkout/reset instruction.

## Mission and scope of authorization

Workspace rendering is complete; keep its existing boundaries and accepted repairs. First address the one remaining configured shared-provider selector observation under S0. Then implement **Projects P1: portfolio, hierarchy inspection, overview and the complete five-step project editor**, including deletion-result rendering, package-dialog presentation and real production handoffs. Deliver an independent sandbox using the same renderers.

Do not start the Workbench/Project Structure/Gantt/Calendar/Processes extractions. Do not claim all Projects complete: its governed Files renderer/coordinator family remains a later explicit cut. Do not spend this run on responsive redesign. The user requires **large-screen-only**, default **1920×1080**. Read [LARGE_SCREEN_POLICY.md](LARGE_SCREEN_POLICY.md).

Read the current `AGENTS.md`, `.github/copilot-instructions.md`, `docs/testing.md`, `.github/workflows/ci.yml`, `docs/architecture/ui-component-seams.md` and relevant repository-family instructions first. Use Code Analytics / Components / dotnetwatch MCPs when actually available; if unavailable, use evaluated project graphs, source/reference searches, existing CLI and tests and state the fallback. Never pretend an unavailable MCP ran.

Read [WORKSPACE_REVIEW.md](WORKSPACE_REVIEW.md), [S0_SHARED_SELECTOR.md](S0_SHARED_SELECTOR.md), [SCOPE_AND_ARCHITECTURE.md](SCOPE_AND_ARCHITECTURE.md), [PROJECTS_SOURCE_REVIEW.md](PROJECTS_SOURCE_REVIEW.md), [VALIDATION_MATRIX.md](VALIDATION_MATRIX.md), [APPLICATION_JOURNEYS.md](APPLICATION_JOURNEYS.md), [DEV_LOOP.md](DEV_LOOP.md) and [EXECUTION_AND_CLOSURE.md](EXECUTION_AND_CLOSURE.md). Apply the included [shared v3 foundation](shared/README.md), but treat its older per-module audit and responsive examples as historical. The current task's desktop scope and live-budget restrictions are controlling. Use English for source, code comments, UI, maintained documentation and commits.

## E0 — record the actual baseline

Record app/Components/FileTools commit and tree IDs, dirty state, source-versus-package resolution, .NET SDK, build configuration and owned test environment. Components development now publishes `4a858412d2c2a3f6123bf23d8c4584f05b47627d`, whose tree matches the repaired `22d5b21…`; do not recreate the Dialog fix or ask for a push already completed. Verify the actual current dependency selected by the consumer and by its target CI branch. Keep informational version differences distinct from source-tree equivalence.

Read the current Workspace R2 closure record, not an archived instruction as evidence. Preserve its successful 15,879-case Stable checkpoint and its still-mixed browser/live report as historical facts. Capture current protected leaf/sandbox graphs and the Projects renderer's actual host and asset inputs before changes.

## S0 — finish the bounded carry-over, then continue

Use the exact cold/ordered empty-client shared-provider test identified in [S0_SHARED_SELECTOR.md](S0_SHARED_SELECTOR.md). Its second Simple Chat model selector observed one option instead of three although catalog metadata existed. This is not a known missing prerequisite, and nineteen later warm observations do not prove a repair.

Resolve the original ordering with safe provider/model/generation/event evidence, held provider responses and actual component events. Repair a demonstrated small product or harness defect and repeat the exact configured case with negative controls. No silent model fallback, lost edits, removed assertions, blanket longer timeout or paid inference.

A no-reproduction disposition requires the same configured acceptance path and discriminating controls, an explicit unresolved-cause note and a qualified technical-entry decision; do not call it a root-cause fix. A recurring unexplained configured failure or demonstrated shared authority/data-integrity issue blocks Projects implementation pending a concrete map. Do not spend the entire run on undifferentiated repetitions. Update only current publication/status notes; historic evidence and sealed bundles remain unchanged.

External paid-model or generated-app blockers are separate. This task does not authorize resetting the exhausted 40-request journal or issuing new billable/live requests. Once the bounded entry is supported, continue with P1 in this run rather than stopping with another Workspace report.

## P1 — use real boundaries, not a cosmetic file move

Start from the existing `CanDoItAll.Modules.Projects.Contracts`; keep it light. Add `src/UI/CanDoItAll.Projects.UI` and `src/Sandboxes/CanDoItAll.Projects.UiSandbox` or reuse an equivalent current cut. A Presentation assembly is optional only for genuinely shared state behavior.

Move actual `ProjectsBoard`, portfolio cards/tree presentation, hierarchy inspection, `ProjectModalHost` overview/editor and their scoped styles. Retain all five steps and current functionality. Move package-dialog controls using safe target-option records; do not drag `DatabaseProfileSummary` from Infrastructure into the leaf. Move deletion/recovery notice rendering, with exact typed targets, not recovery ownership.

The board currently instantiates `ProjectFilesPortfolioPane` directly. Remove that hidden implementation edge from the leaf and compose the active production Files region through an explicit typed slot. Keep the real existing Files pane/dialog and file-lease services working. No fake file browser or placeholder counted as P1 proof. Do not move the entire Files family just to avoid defining the slot.

The routed host retains navigation, profile/authority, service effects, agent-context registration and the concrete production slots. No service bag, hidden IServiceProvider, blanket facades or extra HTTP layer. The new rendering graph excludes EF, Infrastructure and concrete Projects/Workspace/AgentFramework/Workbench implementations. Foundation/MAF/AppComponents do not acquire product UI dependencies. Protect all existing Workspace and other extracted UI/sandbox graphs.

Separate pure data types from the mixed `ProjectModels.cs` carefully. Preserve namespaces, enum values, nullability and public wire behavior; use narrow projections where moving a whole type closure would be counterproductive. Retain one shared Cards/Files filter scope and its bounds/order/fingerprint. Do not create a new writable hierarchy or loosen file access.

## State, writes and effects — mandatory behavioral outcomes

1. Give each acquired editor its own draft, EditContext, phase/option/starter row identities, submitted snapshot and operation state. Same-target rerender, overview/step switch and auxiliary refresh preserve raw input and validation. A failed exact acquisition remains retryable and cannot become an editable new project.
2. Capture every save field and starter plan before the first await. Both Save and Save-and-open validate the same context. Enforce one mutation admission in handlers; read generations alone do not prevent duplicate writes. Preserve newer edits without disabling the entire editor merely to make concurrency tests easy.
3. Record confirmed project identity immediately, before seeding or list/read-back work. Merge actual accepted values and child identities against the submission, preserving later local edits and removed/replaced/new rows. Never infer identities by mutable title or adopt the lifetime of a recreated project from a fresh read. Add a small compatible owner acknowledgement at the existing commit boundary if necessary; do not duplicate the writer or redesign HTTP/agent contracts.
4. Keep starter plans local to their original editor. Cancelling an unsaved project then saving B must not seed the abandoned plan into B. Existing seeding is one separate batch transaction; the whole save-plus-seed operation is not atomic. An acknowledged seed batch is never repeated by refresh or Save merely because later reads failed. An unknown seed result retains the confirmed project fact and explicit review, not a rollback fiction.
5. Preserve original project lifetime through the actual seed writer. If the old ID-only port is insufficient for the new editor, use a minimal compatible Projects-owned admitted port/overload into the existing Workbench seed transaction. No SharedKernel-to-Projects reference, native writer duplication or new durable replay engine. Existing callers and owner contracts stay supported.
6. Independent list, exact editor, hierarchy, package options and agent completion reads must not overwrite one another. Old success/error/finally cannot alter a successor after A→B→A, route change, close or disposal. A catalog refresh never certifies an unacquired editor as Ready. Cancel owned reads; a cancelled view does not undo an accepted write.
7. Retain known deletion/partial-cleanup facts before optional reads. Retry only the exact owner-returned participant/recovery operation on explicit user action; do not retry deletion or seed automatically. Package import/export keeps original path/target and existing safety rules.
8. Agent context remains a production responsibility. Preserve current selected project, active Cards/Files/Hierarchy/Editor view, navigation lease, access state and original completion subscription. Do not publish unsaved data as canonical facts or let an old completion reopen a dismissed editor. Existing backend authority stays enforced at its original boundary.

Implement the deterministic regressions in [PROJECTS_SOURCE_REVIEW.md](PROJECTS_SOURCE_REVIEW.md) before or alongside the corresponding move. Review save/read-back/source-lifetime behavior at the owner, not only in a fake that returns the desired IDs.

## Real sandbox and focused proof

The sandbox must use the production renderers, real BaseLib widgets, fonts, CSS and modal behavior with deterministic in-memory scenario data. Cover realistic and large bounded portfolios, filters/tree/hierarchy, every editor step, validation/raw input, missing references, held reads/writes, partial/unknown outcomes, deletion notices and package presentation. Disclose simulated effects and the deferred Files handoff. Register no product backend or database.

Build every changed production project and owning tests in a separate output/configuration. Discover exact tests before execution; zero or mismatched discovery is not proof. Preserve existing ProjectsPage, lifetime, owner, partial deletion, file, package, context and shared-consumer assertions. Use real PostgreSQL for persistence/authority claims and the actual Web host for browser composition. Target 1920×1080; no small/medium-screen matrix.

Run the applicable journeys in [APPLICATION_JOURNEYS.md](APPLICATION_JOURNEYS.md): project create/edit/read-back with stable child IDs; starter seeding; same-ID recreation refusal; partial cleanup; actual Files handoff; safe package target; current project-bound agent context; and an existing deterministic Agent/Workflow-to-file consumer using the newly UI-created project. Script only the external model boundary. Match real persisted effects and bytes, not just toast text or model prose. Do not expand scopes to make a test pass.

Run current mandatory portability-static and documentation checks, regenerate reviewed baseline deltas only when justified and enforce without write-baseline. Reevaluate the Stable trigger from the actual diff: moved public Projects contracts or changed owner/seed semantics require a named final integration checkpoint and corresponding owning suites. Do not run full Stable per phase, or repeat the entire unrelated browser/live inventory without an invalidation reason. Never relabel a mixed historical run as green after a narrow follow-up.

## Development loop and finish

Measure the original and extracted supported-desktop loop once per meaningful scenario, including actual evaluated graph/watch inputs, startup/hydration and changed Razor/C#/owned CSS behavior. Restore probes exactly. Do not imply the whole Web graph becomes small; the key artifact is the genuinely isolated sandbox.

Repair small reproduced regressions within the described boundary. If a new schema, authority or multi-owner protocol redesign is needed, preserve evidence, map that specific blocker and stop the affected unsafe lane rather than hiding it. Keep independent safe validation possible.

Finish with the concrete diff/architecture summary, S0 disposition, each implemented/deferred surface, exact source pair, actual commands/discovery/results, owner/desktop evidence, watch observations and unresolved external prerequisites. Update maintained module status to **Projects P1 complete / Files P2 deferred**, not all Projects or application release ready. Preserve the old application readiness history. Produce a safe evidence index rather than committing secrets/raw traces. Make appropriately signed local commits if allowed by current repository instructions, but do not push/merge/deploy or disable signing.

Do not stop after a plan or S0 when P1 entry is supported. Do not begin a second module. Do not claim local package validation or an early-return test as product or live proof.
