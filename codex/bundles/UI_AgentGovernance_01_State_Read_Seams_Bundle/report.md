# Governance implementation and blocked closure

Governance is implemented. Its focused semantic, production-tab and extraction checks pass for the proposed source. Formal closure is blocked: the final stable execution returned **10,350 passed, 1 failed and zero skipped**. Published-only delivery remains blocked by the existing Components dialog patch; the documentation gate separately reports 118 historical tracked logs. This is not an unconditional CI/merge-readiness claim.

## Identities and delivery

| Repository | Entry local HEAD | Final local HEAD | Observed remote |
|---|---|---|---|
| CanDoItAll | `039a3a4c98f53aed1178fb7d282d38a92da7ba6d` | `039a3a4c98f53aed1178fb7d282d38a92da7ba6d` | `039a3a4c98f53aed1178fb7d282d38a92da7ba6d` |
| CanDoItAll.Components | `c3e6aa03a878994c0ba8aed6af017d0be75f3796` | `c3e6aa03a878994c0ba8aed6af017d0be75f3796` | `bd1eb1030c438c861b94b3b8d3b9dba72925d685` |
| CanDoItAll.FileTools | `7c7453c6583365ae5bd63f8fc6efc4a776e15818` | `7c7453c6583365ae5bd63f8fc6efc4a776e15818` | `3a080ecd31068a77c1e1bd639f7a78e21c93db85` |

Primary stayed on components-decoupling; Components stayed on codex/original-ui-refactoring-release, with local/remote tree 8ee42b3f1f97fd4bb7b3b3f526029228d8ed6e23; FileTools stayed clean and detached. [Source hashes](proof/final/source-hashes.json) identify proposed source and exact dependency bytes. No index/history/commit/push operation occurred.

Clean archives of entry primary, actual remote Components and the used clean FileTools revision failed the full source build with CS1061: PreserveDialogsOnSamePageNavigation is absent (395.188 seconds). Applying only the five existing Components patch files made that isolated solution build pass (208.140 seconds). The patch remained byte-identical in this task; no further sibling implementation was needed. Direct BaseLib build and 14 dialog-ownership plus seven API/publishing approval cases passed.

Required commit order: publish Components DialogService.cs, its README, DialogNavigationOwnershipTests.cs and two approval fixtures identified in source-hashes.json; update primary CI's CANDOITALL_COMPONENTS_COMMIT from c3e6aa03a878994c0ba8aed6af017d0be75f3796 to that resulting real commit; deliver primary and verify a clean checkout. Commits are explicitly forbidden here, so the future pin cannot be supplied now. FileTools is untouched.

The real Agents page acquires/disposes its lease and owns nested dialog tokens. An active lease can preserve same canonical path query navigation; page departure closes dialogs. Defaults, composed leases, idempotent disposal, result tasks/cancellation and resource disposal are covered. No static/global state or component CloseAll was added.

## Session and accepted state

The host constructs/disposes one AgentGovernanceSession and owns injected reads, logging and context callbacks. One cohesive registered adapter delegates to the canonical workspace without duplicating persistence/execution policy. The private request helper serves only the three Governance lanes.

| Lane | Desired identity | Accepted behavior |
|---|---|---|
| Catalog/agent | Target revision and nullable agent ID | Null is All; Guid.Empty/missing explicit IDs fail closed; only validated current identities publish. |
| Run list | Captured agent target and manual-selection revision | Rows become usable before detail; same-target refresh failure retains stale rows; newer manual selection wins. |
| Detail | Selected run and expected row agent ID | Both identities are checked; retry does not reload catalog/list; removed selection remains unavailable without fallback. |

Each lane captures its token and request identity before asynchronous work. Supersession/disposal cancel and dispose its CTS once. Success, failure, finally and callbacks verify current ownership. Noncooperative underlying work may continue but cannot publish or clear newer busy state. Failed reads are not proof of deletion. Tests cover synchronous completion, delayed token use, every disposal lane, A/B and R1/R2 races and parent echoes.

Access/selection callbacks acknowledge a new accepted target revision even if the value matches an earlier value. Direct RED/green tests repair A -> missing -> A and Ready A -> route B. Invalid null is never echoed as All. No production routing change was made.

## Presentation and extraction

The Surface accepts immutable records/arrays, explicit state and typed intents. It injects no workspace, navigation, notification, database, provider, execution or time service. It retains safe identities/labels/status/provider/model, bounded approval/artifact/checkpoint/receipt rows, timeline and metric totals. Runtime text is encoded. Labels are bounded to 160 UTF-16 units with rune-safe truncation, summaries to 320, section rows to 30, timeline to 12 and metric rows to 10; complete totals remain visible.

The allowlist excludes MetadataJson, serialized state, approval arguments, raw input/result/log prose, structured output/validation JSON, complete session/checkpoint/receipt graphs, absolute working directories, credential values and infrastructure exceptions. Unsafe paths/opaque references become bounded omission/reference labels. Execution prose is deliberately omitted. Arbitrary producer-authored labels are not claimed to be semantically secret-free merely because they are allowlisted.

No suitable explicit application display-zone policy was found. Timestamps use invariant UTC (yyyy-MM-dd HH:mm:ss UTC), or Not recorded. Positive/negative offsets and multiple ambient cultures pass; no server-local conversion is used.

Six files now live in src/UI/CanDoItAll.AgentFramework.UI/Governance: presentation records, mapper, Surface Razor/CSS, ExecutionTimelinePanel and MetricSummaryPanel. Timeline/metrics moved once; Governance and AgentRuntimeDetailsDialog both consume the shared implementation directly. No old wrapper or copy remains. Source consumers use the new namespace and must rebuild; no binary compatibility shim was added.

The evaluated UI/sandbox closure has 14 projects and no Module, Core, Persistence, provider runtime, Voice, AppComponents or broad AgentFramework.Components dependency. Existing references suffice; no production project/reference edge was added. CodeAnalytics had no diagnostics; informational record/mapper member-count findings were reviewed. Actual transitive MSBuild evaluation, not an empty scoped analyzer graph, provides dependency proof.

## Validation

- **140 primary focused cases:** 47 Governance Unit, 46 Governance Components, four canonical Integration, 11 owning context Unit and 32 owning page/runtime Components. **21 sibling** cases also pass. All have zero skips. [Merged executed TRX](proof/final/focused-tests.trx.gz) omits passing stdout.
- **34/34 original G00** pass, including all **29** formerly RED. Only the original fixture's adapter registration changed; no assertion, trait, name, skip or exclusion changed. In-place semantic/security checks passed before movement.
- Integration uses real registered adapters, a PostgreSQL test profile and the canonical profile-scoped execution-history file store. Run records are not falsely described as database rows.
- Direct builds passed for UI, broad shared Components, Module, Web, both sandbox modes, browser fixture and sibling BaseLib. Full product/stable builds passed. Exactly one final stable execution ran **10,351** cases: **10,350 passed, 1 failed, zero skipped**. The failed Windows workspace alias test is outside the change and uses a real process; it passed on an unchanged-source exact retry. No broad pass is claimed. [Validation summary](proof/final/validation-summary.json) has exact commands, counts and durations.
- **45 browser checks** passed across real Web, Parity and Fast: canonical data, keyboard/accessibility, overlapping selection, refresh, independent failures/retries, removed targets, long encoded text, sentinel absence and 768px layout. Fourteen controlled sandbox scenarios plus intents/retry/responsive checks pass in both modes. Six final screenshots were inspected.
- Visual review reproduced Refresh overflow with a failing DOM range assertion. Moving only the Governance description below its title/action row repairs it in every mode. Stable option keys separately repair native selection after asynchronous catalog population. Sandbox waits acknowledge accepted state rather than only a dropdown value.
- New test/fixture setup corrections covered Button attributes, precise heading selectors, canonical ExecutionRunId, valid positive pricing, route-parser arguments and interactive circuit waits. No original G00 behavior or provider production semantic was weakened.

Portability covers complete proposed source: five intentional presentation/comparison findings and two stale findings were reviewed; final enforcement runs without --write-baseline. Source and compressed evidence scans introduce no new secret finding. The new fixture README fixed its documentation finding; only the unchanged 118 historical tracked-log paths remain. [Static gates](proof/final/static-gates.json) records these limits without waiving them.

## Unresolved broad failure

[The compact failure record](proof/final/broad-failure.json) preserves the exact original error, stack, source hashes, isolated retry and read-only probe. WorkspaceCommandExecutionServiceTests.PowerShellRunScript_preserves_external_working_directory_when_script_path_is_shortened failed because the operating-system alias command did not start. All four inspected runtime/test files equal entry HEAD. The underlying adapter discards the inner startup cause, so job attachment versus operating-system launch cannot be distinguished from the original result. One exact retry and 100 read-only process starts passed; neither establishes a fix or converts the failed broad run to green. No unrelated runtime/test change or second broad execution was made. Investigate startup diagnostics in the owning runtime slice; all 34 Governance G00 cases passed in the broad execution.

## Watch smoke and limits

The pre-extraction Web baseline preceded source movement. Final pre-Web/post-Web/sandbox-Parity smoke has three process-cold starts each and one Razor/C#/CSS edit twice each: nine starts and eighteen edits pass, with exact source restoration. One [summary](proof/final/development-loop-summary.json) retains protocol, observations, ranges/medians, classifications and superseded successful series.

This is a watcher-functionality/catastrophic-regression check, not a causal benchmark: caches were warm, first-edit/profile confirmation outliers remain, and bounded route/option/header corrections followed the pre-move baseline. Post/sandbox measurements were repeated after the header fix. Fast was browser-validated, not separately benchmarked. No universal speed or Fast-superiority claim is made.

Retained documentation/evidence stays within 25 files and 15 MiB.

<!-- BUDGET --> Conservative retained accounting: **24 changed/new documentation, evidence and validation-configuration files; 5,495,628 bytes (5.24 MiB)**, including the existing baseline configuration. Production/test source files are excluded.

 Raw logs remain ignored and historical G00 proof is unchanged. Underlying noncooperative reads cannot be forcibly aborted, but publication is fenced. Published-only source reproducibility and inherited documentation debt remain delivery limitations.

| Readiness | Verdict |
|---|---|
| Governance semantics | Ready; all original RED cases pass. |
| Production tab | Ready for proposed source; real browser acceptance passes. |
| Extraction | Complete; single implementations and allowed graph verified. |
| Clean source reproduction | Exact proposed overlay proven; published-only graph waits for Components delivery and actual CI-pin update. |
| Diagnostics | Gated; broad stable must pass before its handoff or implementation. |

No next-diagnostics.md was created: its explicit broad-green prerequisite was not met. No Diagnostics source, bundle tree, sandbox or routing implementation was added. This is a completed bounded implementation with a recorded closure blocker, not another intentional Governance RED checkpoint.

## Exact source changes

Primary-relative paths follow. The reviewed portability baseline additionally changes at tools/Validation/Portability/portability-risk-baseline.json. Documentation/evidence is sealed separately.

- `src/MAF/Common/CanDoItAll.AgentFramework.Components/AgentRuntimeDetailsDialog.razor`
- `src/MAF/Common/CanDoItAll.AgentFramework.Components/ExecutionTimelinePanel.razor` (removed; single implementation moved)
- `src/MAF/Common/CanDoItAll.AgentFramework.Components/MetricSummaryPanel.razor` (removed; single implementation moved)
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/AgentGovernanceSession.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentGovernancePanel.razor`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentGovernancePanel.razor.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkUiServiceCollectionExtensions.cs`
- `src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentGovernanceReads.cs`
- `src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox/Components/Catalog.razor`
- `src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox/Components/GovernanceSpecimen.razor`
- `src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox/Components/_Imports.razor`
- `src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox/GovernanceSandboxFixture.cs`
- `src/Sandboxes/CanDoItAll.AgentFramework.UiSandbox/SandboxSpecimen.cs`
- `src/UI/CanDoItAll.AgentFramework.UI/Governance/AgentGovernanceSurface.razor`
- `src/UI/CanDoItAll.AgentFramework.UI/Governance/AgentGovernanceSurface.razor.css`
- `src/UI/CanDoItAll.AgentFramework.UI/Governance/ExecutionTimelinePanel.razor`
- `src/UI/CanDoItAll.AgentFramework.UI/Governance/GovernancePresentation.cs`
- `src/UI/CanDoItAll.AgentFramework.UI/Governance/GovernancePresentationMapping.cs`
- `src/UI/CanDoItAll.AgentFramework.UI/Governance/MetricSummaryPanel.razor`
- `tests/Components/CanDoItAll.Tests.Components/AgentGovernanceContextRevisionTests.cs`
- `tests/Components/CanDoItAll.Tests.Components/AgentGovernanceReadLifecycleTests.cs`
- `tests/Components/CanDoItAll.Tests.Components/AgentGovernanceSurfaceTests.cs`
- `tests/Integration/CanDoItAll.Tests.Integration/AgentGovernanceReadsTests.cs`
- `tests/Playwright/GovernanceBrowserFixture/GovernanceBrowserFixture.csproj`
- `tests/Playwright/GovernanceBrowserFixture/Program.cs`
- `tests/Playwright/GovernanceBrowserFixture/browser.cjs`
- `tests/Playwright/GovernanceBrowserFixture/smoke.cjs`
- `tests/Unit/CanDoItAll.Tests.Unit/AgentFramework/AgentGovernanceSessionTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/AgentFramework/GovernancePresentationTests.cs`
- `tests/Unit/CanDoItAll.Tests.Unit/AgentFramework/GovernanceSandboxTests.cs`
