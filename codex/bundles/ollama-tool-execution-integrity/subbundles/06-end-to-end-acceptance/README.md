# SB06 — End-To-End Acceptance And Closure

## Status

- `Completed`

## Objective

Demonstrate the requested behavior through the whole agent application with deterministic providers and live direct/shared Ollama, then close only evidence-backed requirements.

## Covered Inputs

- N01–N10; R01–R13; all findings and user-visible acceptance.

## Prerequisites

- SB01–SB05 closure gates passed and still match the frozen source checkpoint.
- Use a disposable project/workspace fixture and explicit direct/shared published-model configuration; do not mutate the user's Tetris3 reproduction.
- Same available Ollama model for both paths, prefer captured gemma4-12b-256k if still installed; record any unavailable dependency instead of silently substituting.
- Start a controlled host only for execution validation, respecting existing process ownership and port availability.

## Exact Source References

- `repo://src/App/CanDoItAll.Web/Api/AgentApiResponseContracts.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkExecutionRunTrackingIntegrationTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/ProjectStructureAgentRuntimeToolRoundTripIntegrationTests.cs`
- `repo://tests/Components/CanDoItAll.Tests.Components/ProjectStructureAgentChatContextProviderTests.cs`
- `repo://docs/testing.md`
- `repo://.github/workflows/ci.yml`

## Deliverables

- Deterministic Web/runtime/storage end-to-end agent cases and four live direct/shared model cases.
- Canonical file/graph/public receipt/run-state evidence with reviewed desktop screenshots.
- Updated execution report, traceability, architecture closure and final validation results; outstanding product limitations kept explicit.

## Dependency Impact

- Final closure depends on every earlier contract. Any mismatch reopens the earliest owning phase and invalidated downstream proofs.

## Validation Depth

- Proof tier: `Behavioral`.
- Test project/filter/expected exact cases: V06 in [validation-plan.md](../../plan/validation-plan.md). Planned new cases are explicitly identified there; no test has been implemented or passed during preparation.
- Selection reason: Deterministic external provider responses plus real application internals; live matrix validates actual model behavior and UI.
- Invalidation keys: Any failed live case, stale source proof, changed SDK/configuration, cross-scope evidence, canonical/UI mismatch or static gate failure reopens the owning phase and final closure.
- Broad-gate decision: Required once at the final frozen SB06 checkpoint because persisted shared receipt semantics and public runtime contracts change.
- Final closure depends on every earlier contract. Any mismatch reopens the earliest owning phase and invalidated downstream proofs.
- Every protected source/test change requires the portability procedure and final enforcement without --write-baseline.

## Implementation Steps

1. Run V06 with fake only the external model boundary; real Web dispatch, runtime, tools, persistence and graph services remain in the path.
2. Verify failure plus success prose is Failed with no node; corrected nested call commits one node; next turn sees safe failure evidence; different-target success does not resolve the failed operation.
3. Run live matrix: direct positive, direct correction opportunity, shared positive, shared correction opportunity. Capture actual model calls. A correction case passes with verified correction or honest failure, never a false success. Use deterministic V06 to guarantee the malformed-output branch is covered.
4. Keep the structure page open during each mutation and assert the committed node appears without manual refresh. Compare managed content/hash and parent to canonical readback.
5. At the final frozen source checkpoint run the named broad stable gate once for shared receipt/persistence/composition changes, final portability enforcement, documentation and bundle validators.
6. Review normal and runtime-details screenshots, architecture source/caller evidence, raw-note closure and residual limits. Stop owned validation hosts and record cleanup.

## C# Architecture Impact

No new architecture layer. Exercise the actual Web/application/domain/UI composition with only external provider substitution in deterministic tests. Follow [architecture checkpoints](../../plan/architecture-checkpoints.md).

## Boundary Ownership

No new architecture layer. Exercise the actual Web/application/domain/UI composition with only external provider substitution in deterministic tests.

## Dependency Direction

Preserve [the approved project directions](../../architecture/02-csharp-dependency-direction.md). No new project reference is planned; no Core-to-Maf/Workbench/Web or neutral-contract-to-SDK dependency.

## Pattern Decision

Production-path acceptance harness and existing application composition; no test-only alternate runtime.

## Testability Contract

Deterministic external provider responses plus real application internals; live matrix validates actual model behavior and UI. Expected discovery must match V06; test-created success artifacts cannot substitute for production producer/consumer proof.

## Partial Class Policy

No new partial-file architecture. Touched orchestration partials may delegate to cohesive top-level policies; existing facade roles remain. Document the actual responsibility removed from a hotspot.

## Architecture Proof Required

- Record actual changed types, callers, constructor dependencies and before/after project references.
- Run relevant CodeAnalytics or explicit dependency review, affected builds and the C# architecture gate.
- Reject wrapper-only extraction, service-locator wiring, unused abstractions and untyped context bags.

## UI Composition Contract

Primary surface: existing desktop project canvas, with contextual agent chat and runtime details as supporting floating surfaces. Retain existing Razor component wrappers and current CSS; no new cards, stats row, editor or textarea sizing change is needed. Normal graph remains the main surface. Target 2048×1100 CSS pixels. Canvas owns pan/zoom; chat transcript and details own their internal scrolling. Review first-viewport graph visibility and overlay clipping. Components MCP must be rechecked before new markup; it was unavailable during preparation.

## Scope Exceptions

- The initial investigation proves the captured direct run only. Shared live behavior is pending SB06.
- User requested preparation only; all implementation and product validation in this specification are future work.

## Do Not Do

- Do not mark screenshots, scripted upstream responses, a direct API-created node or one successful provider as complete agent parity proof. Do not change the original user's graph or mark preparation diagnostics as implementation tests.

## Acceptance Checklist

- Both transports show equivalent application behavior; the real shared source path is exercised.
- A model that continues with prose after a tool failure cannot produce a Succeeded mutation report.
- A successfully registered node appears under the intended parent without manual refresh, and content/receipts agree.
- Cross-turn failure evidence helps correction without leaking prior authority or other context.
- Every subbundle gate has current proof; no zero/unexpected test discovery or failing portability finding is waived.
- Original incident data remains untouched; temporary host/fixture cleanup is recorded.

## Proof Required

- V06 deterministic whole-path cases and four live matrix rows with actual run IDs/model/profile/routes, scoped graph readback and safe receipts.
- evidence/SB06-direct-normal.png, SB06-direct-runtime-details.png, SB06-shared-normal.png, SB06-shared-runtime-details.png, plus failure-state evidence.
- Exact build/discovery/test results, final no-write portability enforcement, docs validator, completed-stage bundle validator and architecture closure.
- Record exact commands, expected/actual discovery and exit codes. Zero or unexpected discovery is a failed proof.
- Use [semantic evidence rules](../../plan/validation-plan.md#semantic-evidence) and preserve both positive and adversarial negative results.

## Browser Validation Logging

- Route: /projects/{disposableProjectId}/structure; open contextual agent chat and runtime details.
- Viewport: 2048×1100 CSS pixels, desktop. No mobile scope.
- Playwright MCP: navigate, select the intended parent, submit the agent request, await terminal state, assert node visibility without manual reload, inspect safe failure/receipt details.
- evidence/SB06-direct-normal.png, SB06-direct-runtime-details.png, SB06-shared-normal.png, SB06-shared-runtime-details.png, plus failure-state evidence.
- Review screenshots for normal canvas and open-overlay states, first-viewport graph access, scroll ownership and clipping. Record actions, actual assertions and screenshot findings in the execution report.

## Progression Gate

- Close only if deterministic tests, both live paths, desktop refresh proof and all required gates pass. A unavailable live dependency leaves execution incomplete and identifies the required action.
- No final implementation completion claim can rely solely on the successful preparation gate.

## Reopen Triggers

- Any failed live case, stale source proof, changed SDK/configuration, cross-scope evidence, canonical/UI mismatch or static gate failure reopens the owning phase and final closure.

## Suggested Agent Prompt

```text
Execute this subbundle only after the user authorizes implementation. Verify prerequisites and current source. Preserve the outcome, ownership and scope contracts. Capture the required production-path proof, update the execution report, and stop progression if its gate fails. Do not infer implementation permission from this prepared bundle.
```
