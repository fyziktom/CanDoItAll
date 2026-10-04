# Agents Completion AC1 — execution assignment for Codex GPT-6 Astra Max

You are the senior C#/.NET and Blazor implementer completing the current component/UI decoupling wave in CanDoItAll. Work on the current user-provided checkout and branch. Complete the stages below, with signed checkpoints, native tests and honest final accounting. Do not stop after one dialog, the first repair or a new inventory document.

## Read first and preserve the baseline

Read `AGENTS.md`, `.github/copilot-instructions.md`, `docs/architecture/ui-component-seams.md`, `docs/testing.md` and the current CI workflow. Apply the repository-family SharedInfo standards and available architecture/extraction skills. Read this package's [shared foundation](shared/prompt.md), [WF1 review](WF1_REVIEW.md) and [execution rules](EXECUTION_AND_CLOSURE.md).

The reviewed main is `33007c1c693c8ae6591ee0f201ed9526bbaaac78` on `components-decoupling`; the now-published Components development head is `2eccdddd05a9b1b0c90935ddee49dc559fd5eec1`. These identify evidence, not a reset target. The latter directly contains the previous Tooltip repair `b495d4c4a28f0a6588ba10bfaa7be6e8409eae18`. Do not recreate these fixes or ask for an already-completed push. Verify the actual local/remote source pair, loaded assemblies and served assets, including the FileTools ref selected by this build. Old report statements that these Components commits were local-only describe their execution checkpoint, not the latest delivery state.

Keep all historical `codex/bundles` folders intact. Do not run old bundles again or edit their sealed claims. Write current architecture/testing records outside the sealed execution package. Read [signing](COMMITS_AND_SIGNING.md) and arrange native PGP unlock early. Commit coherent completed stages and verify every new signature; do not push, merge, release, disable signing or request a passphrase in chat.

## Goal and non-goals

Finish the remaining product-reachable **Agents presentation**, not the entire Agent runtime. The primary purpose remains isolated UI iteration with `dotnet watch`, actual shared components and independent sandbox proof. In-process host-to-owner calls remain valid. Do not convert the product to API-only rendering. Existing Project Structure/Processes control-plane authority stays intact.

Preserve completed Workspace, Projects, editor A2, capability/team CA1, provider PP1/PP2/PP3 and Workflow WF1 boundaries. Do not repeat their extraction. Native route, query, execution, credential, profile, context and admission hosts may remain in implementation projects. A large code-behind is not by itself an unfinished renderer.

No new Workbench or Processes extraction in this run. No provider protocol redesign, database migration, new universal receipt/state/retry framework, speech feature expansion, automatic tool approval, paid inference or arbitrary production cleanup.

## S0 — mandatory bounded repairs before subsequent UI work

1. **SCH-R1: exhausted saved schedule blocks application startup.** Follow [the native restart repair](S0_SCHEDULER_RESTART.md). Reproduce with the actual installed Quartz version, PostgreSQL and the real projection hosted service. Repair the projection owner, not the test fixture. A completed fixed-date plan must survive repeated restart without startup failure or duplicate execution; future and misfire behavior must remain correct. Preserve plan and run identities, histories, enabled intent, source authority and unresolved admissions. Never count the prior manual SQL pause as a product fix. Do not use uninitialized `GetNextFireTimeUtc()==null`, exception-message matching or a broad catch as a terminal-state oracle. Prove the distinction between exhausted completed work and missed/unresolved work before the first UI checkpoint.
2. **WF1-R1: preview identity leaks between attempts.** Follow [the attribution repair](S0_PREVIEW_ATTRIBUTION.md). A successful preview followed by an unconfirmed dispatch on the same native owner must not return the earlier run ID as the new attempt's reserved identity. Retain historical accepted facts separately, enforce the existing unknown-admission guard, and do not replay work to recover an ID. Use the actual owner and both production preview entry paths.
3. Revalidate the retained WF1 hidden-field/port/input round-trip, CA1 diagnostic attribution and published Tooltip/read-only Canvas guard where affected. Do not reopen historical timing incidents without a new reproduction or changed owner.

Both findings are bounded implementation work. If reproduction reveals a substantially different durable-identity/authority issue, preserve the failing evidence, stop that unsafe path and map the necessary ownership change. Continue independent safe work, but never mark Agents ready while a startup or wrong-target blocker remains.

## A1 — complete the current renderer/caller census

Use [AGENTS_CENSUS.md](AGENTS_CENSUS.md) and the optional inventory helper. Follow actual `@page` routes, `.razor` descendants, generic `OpenAsync<T>`, dynamic registries, shell contributions and assets. Search current source, not only the default branch index.

Classify every current Agents-related component as: already isolated renderer; remaining renderer to extract; intentional production effect/route/context host; neutral shared primitive; or proven non-product/developer-only/unreachable legacy surface. Record the caller and destination, not just line counts. Inspect SimpleChats, MAF Components and indirect contextual/floating consumers as well as Modules.AgentFramework.

There is no mandate to recreate historical hypothetical provider test-chat/model-maintenance screens. A backend/API method does not establish an active UI. Conversely, a reachable descendant or runtime-inspection dialog must not be omitted because its outer host is thin. Resolve the actual reachability of ScenarioHarnessPanel and AgentOverviewUsageList. Do not delete dormant public components or historical fixtures just to shrink the census.

## A2 — shell and complete Usage family

Extract the actual Agents header/statistics/navigation presentation and the three consumer/provider/model Usage dialog renderers, plus any genuinely active residual overview usage presentation. Use [SHELL_AND_USAGE.md](SHELL_AND_USAGE.md).

Prefer the existing `CanDoItAll.AgentFramework.UI` and its sandbox; it already references light Usage/Models contracts, BaseLib/Charts and neutral Conversations. Add a new project only for a documented dependency boundary that cannot reasonably use this leaf. Do not couple the completed provider/editor/Workflow/Workspace leaves through a new all-Agents service bag.

Keep route parsing, summary queries, HR/context readiness, exact tab tokens, default feeding and dialog ownership in native hosts. Preserve all existing tabs and supported links. Preserve the bounded default usage window, selected workloads, applied UTC interval, partial/unknown/unpriced distinctions and lazy detail loading. Do not load all history to paint a read view. Charts, grids and copy/status controls must be the real shipped components.

## A3 — conversation/runtime adjunct renderers

Complete the actual runtime detail and execution-log renderers, floating conversation context/affinity strip, close-choice presentation and remaining active switch/thread/confirmation presentation found in A1. Use [CONVERSATION_AND_RUNTIME.md](CONVERSATION_AND_RUNTIME.md).

`AgentChatPanel` and `ChatWorkspacePanel` already compose `AgentChatSurface`; preserve that real surface. `AgentExecutionActivityStatus` may correctly remain the owner-facing typed stream subscriber while its status rendering stays light. Native policy/argument redaction, run state, attachment grants, approvals, context leases and runtime cancellation do not move into the renderer. Use narrow typed slots/callbacks with the original conversation/run/operation identity.

A dialog result must target its opening identity, even after a second conversation, changed profile, fresh run with reused visible labels or disposed owner. Closing a window is not synonymous with canceling a durable run. Do not add global close-all, automatic recovery, or policy fallbacks. Do not show raw runtime objects/exception strings just because a projection now lives elsewhere.

## A4 — close already-isolated Voice/Floating/SimpleChats integrations

The actual global Voice and Floating Settings renderers already live in light UI. Do not duplicate them. Audit their native host lifetimes, published assets and representative sandbox scenarios; repair only reproduced bounded defects. Use [VOICE_AND_SIMPLE_CHATS.md](VOICE_AND_SIMPLE_CHATS.md).

Preserve Save versus sample synthesis versus browser playback outcomes. The existing Play sample path saves settings before synthesis; it is not read-only. Never authorize real paid speech requests in this task. Validate with owned deterministic audio endpoints/media and real browser-facing lifecycle where supported. Trace the real settings scope before changing profile handling; do not assume all global settings belong to a selected business database.

Finish the reachability/destination census and any remaining product renderer in the scoped Agents families. Keep intentional native hosts with an explicit explanation and consumer proof. Do not classify an active full form as a host merely because it calls a service, nor split healthy runtime services for cosmetic file-size targets.

## A5 — final integration and closure

Execute the [validation matrix](VALIDATION_MATRIX.md), [application journeys](APPLICATION_JOURNEYS.md), [multi-instance/restart runbook](MULTI_INSTANCE_AND_RESTART.md) and [development loop](DESKTOP_AND_DEV_LOOP.md) on the final source pair/images.

Use freshly built owning tests with stated and verified discovery counts after each coherent stage. Do not launch broad Stable after each form. The startup-owner change, new public presentation boundary or solution wiring may justify one frozen final Stable checkpoint; explicitly evaluate current repository invalidation rules. Stable disposition must keep every original failure and its later focused proof. Never retroactively relabel a failed full run or a prerequisite refusal as green.

Required production paths include actual save/load and all Agents tabs, standard and floating conversations, approved and rejected tool calls, Project Structure hidden-canary read/write/attachment/download, Workflow accepted/incomplete output, Scheduler completed-plan restart plus a future real fire, usage aggregation and authorized History. Use a real source and two clients for affected provider/selector/usage/context journeys; ensure exact model display names and opaque routing values and no fallback. Existing unchanged protocol proof may be reused only with an explicit source-equivalence/invalidation assessment; the repaired restart and changed UI must run on the final image.

Scripting the external model response is allowed; faking native writes, admission, approval or content bytes is not. Use only owned test databases, endpoints, process helpers and containers. Port 5032, personal profiles/keys and preserved manual/historical test fixtures are not yours to reset. No new paid model requests and no reset of the previous exhausted live budget.

All new visual proof is **large desktop 1920×1080 at scale 1**. No small/medium/mobile/tablet tuning. Check real chart/canvas/media assets, scrolling, footer/action reachability, focus, stacking and two independent instances. Keep preexisting shared tests intact without inventing a responsive campaign.

A meaningful final deliverable includes: completed renderer/caller/asset census, named legitimate hosts, fixed native blockers, evaluated protected dependency graphs, source/published sandbox scenarios, visible repeated Razor/C#/CSS/JS watch measurements where relevant, native consumer evidence, portability-static enforcement, source/delta secret-scan scope and verified signed checkpoints. Update maintained docs with a current Agents boundary status and the next Workbench/Processes roadmap.

Report `agents_ui_boundary_complete`, `scheduler_restart_fixed`, `preview_attempt_identity_fixed`, `native_consumer_campaign_passed`, `ready_for_next_module` and source/dependency delivery separately from `release_ready`. Do not assert product-wide release readiness. Keep unresolved blocking findings explicit; minor unrelated historic qualifications may be reported without redoing completed work.

Use [external evidence templates](templates/README.md); do not edit the sealed package to manufacture completion. Tools validate only integrity and structure. Your final response may be Czech; all source comments, production UI, maintained documentation and execution artifacts must remain English unless they are existing localized content intentionally preserved.
