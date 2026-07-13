# Assumptions And Risks

## Assumptions

- The target branch for implementation remains `memory-providers` unless the user says otherwise.
- Package update work is limited to Microsoft Agent Framework and direct dependency-floor packages needed by that update.
- NuGet package availability must be rechecked during implementation because preview package availability can change.
- The current direct process-tool gap is intentional for this phase; it is not fixed by adding process runtime tools.
- Existing `Microsoft.OpenApi` vulnerability warnings are pre-existing validation noise unless the implementation changes those projects.
- `.slnx` package listing may need per-project commands; this is expected and should be recorded, not treated as a blocker.

## Critical Path Risks

- `SB01` failure to separate pre-existing restore/build/test failures from package-induced failures makes all later evidence unreliable.
- `SB02` could accidentally broaden package updates because NuGet reports newer `Microsoft.Extensions.*` and non-MAF packages. The gate must block unrelated upgrades.
- `SB03` could hide API drift by weakening approval/finalizer/session/provider behavior or by introducing broad fallback logic. That must block downstream work.
- `SB04` is the explicit stop point for architecture drift. If it is skipped, broad tests could pass while governance behavior was removed.
- A2A has a 1.13 preview candidate but Mem0 was not found from configured sources during preparation. This must be treated as a documented compatibility decision, not guesswork.
- `CanDoItAll.AgentFramework.Hosting` and `CanDoItAll.AgentFramework.Tooling` contain package-floor-adjacent references not called out in the original package matrix. Implementation must inspect them if restore/build produces downgrade warnings.

## Validation Risks

- Focused tests named in the previous prep may not all exist or may have different names. Replacement tests must preserve the validation intent and be documented.
- Integration and Playwright tests may depend on local services, browser installation, PostgreSQL, Qdrant, or provider configuration. Skips require exact environment reasons.
- Source scans may find historical docs and tests containing `processes_*`; implementation must distinguish expected historical mentions from new production provider registration.
- CodeAnalytics reported module/type cycles in `CanDoItAll.Modules.AgentFramework` with node ids only. `SB04` must inspect any changed dependencies if package fixes touch that module.
- Package-update compile errors may tempt large runtime refactors because `MafAgentRuntime` and `RuntimeCapabilityComposer` are large. That is explicitly out of scope.

## Reopen Triggers

- Reopen `SB01` if any baseline failure is discovered after package changes.
- Reopen `SB02` if restore shows a package conflict, downgrade, or preview-package incompatibility not represented in the package decision table.
- Reopen `SB03` if fixes touch process APIs, direct process tools, product modules, unrelated package families, or central package management.
- Reopen `SB03` and run the architecture gate if a compile fix adds new abstractions, adapters, factories, partial classes, or project references.
- Reopen `SB04` if CodeAnalytics or source scans show a new cycle, product-to-infrastructure inversion, or service-locator shortcut.
- Reopen `SB05` if focused tests are replaced without a recorded equivalent behavior target.
- Reopen `SB06` if evidence prose claims a command passed without an artifact-backed transcript or exact result summary.
