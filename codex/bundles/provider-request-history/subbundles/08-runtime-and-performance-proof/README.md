# SB08 — Runtime And Performance Proof

## Status

- Execution: Completed

## Objective

- Prove the composed feature through real deterministic producer/host/storage/UI paths and measured boundedness, then run one justified affected-project regression checkpoint.

## Covered Inputs

- N001–N011; R001–R011, R013, R014. R012 preparation boundary remains documented separately.
- [Normalized requirements](../../requirements/01-normalized-requirements.md).

## Required user acceptance environments

The implementation authorization requires both the standard 5032 application and the
existing Docker shared publisher5210/client5212 path. Preserve their data and configuration.
Capture actual agent/chat producer calls, caller identity and both history tabs in each
appropriate host. Deterministic isolated test hosts supplement this acceptance and never
replace it. Record any inability to test these targets as an open gate.

## Prerequisites

- SB01–SB07 closure gates passed and all invalidation keys still match.
- Freeze source/tests/schema/config/fixture inputs and inspect the actual diff with refreshed dependency/impact evidence.
- Provision isolated hosts/PostgreSQL/files and deterministic upstreams; inspect safe setup/teardown before using existing UI helpers.
- Browser tools and component contracts must be available for desktop proof; do not evade tool sandbox/approval failures.

## Exact Source References

- `repo://tests/Playwright/CanDoItAll.Tests.Playwright/SharedProviderTwoInstanceUiAcceptanceTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/SharedProviderStreamingIntegrationTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/SharedProviderOpenAiCompatibilityIntegrationTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/DatabaseMigrationIntegrationTests.cs`
- `repo://src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs`
- `bundle://architecture/01-csharp-boundary-map.md`
- `bundle://architecture/05-history-data-lifecycle.md`
- `bundle://architecture/09-search-security-contract.md`
- `bundle://architecture/10-pricing-and-capture-contract.md`

Linked source context:

[Existing two-instance fixture (inspect only; not automatically safe to execute)](C:/repositories/CanDoItAll/tests/Playwright/CanDoItAll.Tests.Playwright/SharedProviderTwoInstanceUiAcceptanceTests.cs).
[Shared streaming fixture](C:/repositories/CanDoItAll/tests/Integration/CanDoItAll.Tests.Integration/SharedProviderStreamingIntegrationTests.cs).
[Shared compatibility fixture](C:/repositories/CanDoItAll/tests/Integration/CanDoItAll.Tests.Integration/SharedProviderOpenAiCompatibilityIntegrationTests.cs).
[Migration fixture](C:/repositories/CanDoItAll/tests/Integration/CanDoItAll.Tests.Integration/DatabaseMigrationIntegrationTests.cs).
[Production composition](C:/repositories/CanDoItAll/src/App/CanDoItAll.Composition/RuntimeHostServiceCollectionExtensions.cs).
Normative [boundary map](../../architecture/01-csharp-boundary-map.md),
  [lifecycle](../../architecture/05-history-data-lifecycle.md),
  [query/security](../../architecture/09-search-security-contract.md) and
  [pricing/capture](../../architecture/10-pricing-and-capture-contract.md).

## Deliverables

- Production-host deterministic acceptance covering priced/unknown relay use, two keys for one subject, every declared operation path, both query scopes and explicit authorized detail.
- Desktop1920x1080 normal and relevant overlay screenshots with written focus/scroll/clipping/error/coverage findings; no eager search/save side effects.
- Measured PostgreSQL SQL/EXPLAIN, bounded page/response/capture allocation, scale/latency/concurrency and maintenance evidence per validation strategy.
- Crash/restart/profile-transfer/retention/replay proof using real producer/consumer paths, not index seeding as the sole functional acceptance.
- One frozen actual-diff impacted-project broader regression checkpoint for public-contract/schema/DI triggers, reusing still-valid focused evidence.
- Update architecture size/dependency/construction audit and attach complete Governed manifests, transcripts and traceability.

## C# Architecture Impact

This phase validates actual composition and lifecycle. Do not refactor unrelated code to improve benchmark numbers or add a second implementation used only by acceptance tests.

## Boundary Ownership

Each phase owner remains accountable for failed invariants. SB08 coordinates composed evidence; it cannot waive missing identity, source durability, authorization or content-ownership gates.

## Dependency Direction

Recompute project graph/public-signature constraints and manually inspect DI factories/dynamic EF registration beyond the original ten-project snapshot. All approved boundaries remain in force.

## Pattern Decision

Reuse the chosen patterns and existing deterministic host/test infrastructure. Synthetic index data is valid for scale only; functional acceptance must use actual capture producers.

## Testability Contract

New deterministic ProviderHistoryUiAcceptanceTests plus new history integration scale/composition cases. Mandatory acceptance: Provider_and_global_history_are_lazy_and_filter_the_same_attempts; Scale_search_obeys_plan_row_and_latency_budgets; Scale_capture_and_cleanup_remain_bounded_under_concurrent_search.

## Partial Class Policy

No new runtime partial. Existing Razor code-behind/generated files are exceptions only for
their established framework role. New cohesive classes follow the 250-line review and
400-line redesign/exception gate; extraction removes the original behavior.

## Architecture Proof Required

- Record actual changed files, public signatures and project edges against the allowed
  dependency table. Review DI factories and old call sites, not only the new collaborator.
- Record actual runtime class sizes, constructor responsibilities, source diff, moved-body removal and full affected dependency graph; no fake separation, unobserved adapters or undocumented exception above400 lines.

## Dependency Impact

- SB09 cannot close if any mandatory executed proof is missing, skipped or stale.
- A failing invariant reopens its owner and affected descendants; do not fix all failures inside a catch-all runtime phase.

## Validation Depth

- Proof tier: `Governed`.
- Critical foundation: Yes; composed feature, desktop acceptance, real persistence and measured performance..
- Test project/filter: `C:/repositories/CanDoItAll/tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj` / `FullyQualifiedName~ProviderHistoryQueryIntegrationTests.Scale_|FullyQualifiedName~ProviderHistoryPersistenceIntegrationTests.Scale_|FullyQualifiedName~ProviderHistoryRuntimeIntegrationTests`; `C:/repositories/CanDoItAll/tests/Playwright/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj` / `FullyQualifiedName~ProviderHistoryUiAcceptanceTests`.
- Selection reason: New deterministic composed runtime/browser acceptance and actual PostgreSQL scale behavior. These focused commands are separate from the one diff-selected broader checkpoint.
- Expected discovery: The three named cases above plus production-host registration, two-key relay pricing/caller investigation and crash/profile lifecycle cases; all must be real discovered unskipped tests. Record exact actual cases/counts at execution;
  zero discovery or a missing named expected case fails the gate. Discovery has not run now.
- Invalidation keys: FrozenRuntimeCheckpoint; all upstream executable/schema/fixture keys; QueryScaleFixture; DesktopAcceptance.
- Broad-gate decision: Required once at frozen SB08 only if public-contract/schema/DI
  changes made here trigger it. No broad suite here or repeated run without invalidation.
- Future focused commands (after implementing the named cases; use the same unchanged
  source revision for discovery/build and the subsequent no-build execution):

```powershell
dotnet test 'C:/repositories/CanDoItAll/tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj' --list-tests --filter 'FullyQualifiedName~ProviderHistoryQueryIntegrationTests.Scale_|FullyQualifiedName~ProviderHistoryPersistenceIntegrationTests.Scale_|FullyQualifiedName~ProviderHistoryRuntimeIntegrationTests'
dotnet test 'C:/repositories/CanDoItAll/tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj' --no-build --filter 'FullyQualifiedName~ProviderHistoryQueryIntegrationTests.Scale_|FullyQualifiedName~ProviderHistoryPersistenceIntegrationTests.Scale_|FullyQualifiedName~ProviderHistoryRuntimeIntegrationTests'
dotnet test 'C:/repositories/CanDoItAll/tests/Playwright/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj' --list-tests --filter 'FullyQualifiedName~ProviderHistoryUiAcceptanceTests'
dotnet test 'C:/repositories/CanDoItAll/tests/Playwright/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj' --no-build --filter 'FullyQualifiedName~ProviderHistoryUiAcceptanceTests'
```

## Implementation Steps

1. Freeze actual inputs, record broader trigger/affected scope and reuse valid SB01–SB07 artifacts.
2. Run deterministic actual-producer flows, including relay price/unknown, retry, stream terminal/cancel, multi-owner and two-key attribution.
3. Inspect both lazy tabs/settings and normal/detail/permission/error overlays at the target viewport with service/network assertions.
4. Run the declared relational and scale protocols; record plans/measurements including rejected/timeout samples, not just successful averages.
5. Run the once-only justified broader affected regression; analyze failures in their real owner scope.
6. Complete manifest hashes/producer-consumer evidence and architecture review; reopen unresolved invariants rather than claiming closure.

## Acceptance Checklist

- [x] Both views filter the same captured local attempts and distinguish managed keys; old unsupported price remains honest.
- [x] Browser initial/tab/draft state causes no history/count/detail query or provider save; screenshots and assertions cover overlays and keyboard flow.
- [x] One-million-row synthetic scale fixture meets declared page/plan/byte/time bounds or has an explicitly reviewed evidence-based target revision.
- [x] Capture and projection do not copy prior conversation, lose attempts silently, or repeat inference on failure.
- [x] Profile/authorization change before publishing denies stale results; expired/deleted content and stale replay remain suppressed.
- [x] No mandatory fixture is skipped, zero-discovered, mocked into desired output or falsely claimed from old bundle status.
- [x] No real paid model, user database, token issuance on the active instance or deployment occurred without separate authorization.

## Proof Required

- Store a proof manifest, exact command transcripts, discovered cases/exit codes, changed-source revision, artifact paths/hashes and semantic positive/negative evidence under `proof/SB08/` at the bundle root.
- Follow plan02 sections6–8: exact production producer/consumer artifacts, fail-first/pass behavior transcripts, SQL/EXPLAIN JSON, workload/environment data, raw samples and screenshot reviews. User-reported5210 inspection is still unverified from preparation; if later inspected, keep it read-only unless separately authorized and distinguish its historical row from deterministic acceptance.
- Follow [validation strategy](../../plan/02-validation-strategy.md); distinguish existing
  test anchors from proposed new cases, and source proof from executed behavior.

## Browser Validation Logging

Routes: isolated /agents provider editor History, /agents Request history, and /settings policy section. Target1920x1080. Use approved Playwright/browser tools, record network/service counts and inspect normal, loading, empty, error, denied-content, detail and settings overlays. Expected artifacts: proof/SB08/ui/provider-history-desktop.png, agents-history-desktop.png, detail-overlay-desktop.png and policy-desktop.png, plus written first-viewport/scroll/focus findings. Do not run existing real-catalog or whole-Playwright suites automatically.

## Scope Exceptions

- This phase alone does not close the complete product request. Deferred IDM/EGCP person
  mapping, global federation, exact wire replay, mobile redesign and unrelated refactors
  remain outside the bundle.
- No paid inference, user-database mutation or deployment without explicit authorization.

## Do Not Do

- Do not call an illustrative seeded screen production-path acceptance; use seeding only for performance fixtures.
- Do not infer a performance improvement from source review or one timing sample.
- Do not work around unavailable browser/database tools or broaden to paid/destructive tests without authorization.

## Progression Gate

- SB09 may begin only with complete valid runtime, UI, lifecycle, measured query/capture and once-only affected-regression evidence; any failed mandatory invariant reopens its source phase.
- Update [execution report](../../reviews/01-execution-report.md) with actual proof and
  downstream dependencies checked. A planned command or passed intermediary is not closure.

## Reopen Triggers

- Any post-freeze code/test/schema/DI/fixture/environment change invalidates only the evidence it affects; recalculate dependencies before reusing results.
