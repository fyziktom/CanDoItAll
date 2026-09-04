# SB05 — Asset Commit Evidence And Refresh

## Status

- `Completed`

## Objective

Preserve reliable asset commit evidence through later failures and refresh the matching open graph from canonical state without a manual reload.

## Covered Inputs

- N03, N04, N07; R07, R08, R10; F06 and F07.

## Prerequisites

- SB01/SB02 contracts and SB03 scope policy passed; SB04 parity proof available before live validation.
- Requery Components MCP before markup changes. Preparation saw Transport closed; no new component choice was inferred.
- Inspect existing asset storage atomicity and mutation lease boundaries before deciding the exact commit point.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAgentService.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Context/AgentChatContextInvocationFactory.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentChatExecutionOrchestrator.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Services/AgentChatExecutionNotificationHub.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/Components/ProjectStructure/ProjectStructureAgentChatContextProvider.razor`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.AgentWindows.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/ProjectStructureAgentRuntimeToolRoundTripIntegrationTests.cs`
- `repo://tests/Components/CanDoItAll.Tests.Components/ProjectStructureAgentChatContextProviderTests.cs`

## Deliverables

- Focused asset/operation boundary that retains trusted committed node identity and managed storage outcome when later analytics fails.
- Effects-aware scoped notification using the existing hub and canonical reload path.
- Status/details presentation that reflects SB02 safe outcomes using existing components; no broad canvas redesign.
- Real temporary managed-storage and component tests for partial effects, scope, deduplication and disposal.

## Dependency Impact

- SB06 visible behavior depends on this phase. A change to commit semantics can reopen SB02 terminal assessment and SB03 historical evidence.
- The original reported run failed before execution; this phase repairs an adjacent window and protects refresh after truthful failure statuses are introduced.

## Validation Depth

- Proof tier: `Behavioral`.
- Test project/filter/expected exact cases: V05 in [validation-plan.md](../../plan/validation-plan.md). Planned new cases are explicitly identified there; no test has been implemented or passed during preparation.
- Selection reason: Real storage and tool service plus throwing analytics dependency; bUnit on existing hub/context with real scope matching.
- Invalidation keys: Changes to storage commit order, effect identity, notification filtering/deduplication, run outcome, scoped context lifecycle or canvas reload reopen SB05/SB06.
- Broad-gate decision: Not required in this phase; shared receipt/persistence contract trigger is consolidated at the final frozen SB06 checkpoint.
- SB06 visible behavior depends on this phase. A change to commit semantics can reopen SB02 terminal assessment and SB03 historical evidence.
- Every protected source/test change requires the portability procedure and final enforcement without --write-baseline.

## Implementation Steps

1. Identify the actual service commit/readback point; record how managed content and node metadata become durable and which failures have Unknown effect.
2. Separate invocation execution/effect capture from post-operation analytics in the touched asset path; log safe actionable analytics failures while preserving committed effect evidence.
3. Reuse declared side-effect metadata and typed affected-source identity. Do not derive effects from tool-name strings or assistant text.
4. Update notification creation/orchestration to publish matching committed effects for failed/cancelled terminal runs; coalesce/dedupe per run/project and honor subscription disposal.
5. Keep page reload canonical and source-scoped. Preserve selected project and existing canvas interactions.
6. Run V05 plus current asset round-trip regressions; perform the SB05 desktop check and static gate.

## C# Architecture Impact

Workbench service owns managed commit, tool adapter reports it, Core/modules route scoped effects, page owns canonical read and rendering. Follow [architecture checkpoints](../../plan/architecture-checkpoints.md).

## Boundary Ownership

Workbench service owns managed commit, tool adapter reports it, Core/modules route scoped effects, page owns canonical read and rendering.

## Dependency Direction

Preserve [the approved project directions](../../architecture/02-csharp-dependency-direction.md). No new project reference is planned; no Core-to-Maf/Workbench/Web or neutral-contract-to-SDK dependency.

## Pattern Decision

Focused asset collaborator plus existing observer hub; explicit outcome/effect data instead of hidden fallback.

## Testability Contract

Real storage and tool service plus throwing analytics dependency; bUnit on existing hub/context with real scope matching. Expected discovery must match V05; test-created success artifacts cannot substitute for production producer/consumer proof.

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

- No manual repair of Tetris3, unsafe file/path fallback, global refresh, exception swallowing, unconditional retry, new layout system or rewrite of the whole project tool catalog.

## Acceptance Checklist

- A valid asset call creates exactly one canonical node under the requested parent with managed content matching the source file.
- Invalid parent/path/unauthorized request creates no asset and reports a useful safe failure.
- Analytics failure after commit preserves Committed identity and prevents an automatic duplicate mutation.
- Committed effect followed by later failure/cancellation refreshes the current graph; no committed effect does not fabricate a node.
- Another project or disposed context is not refreshed; duplicate completion/effect notifications coalesce.
- Normal canvas and runtime-details overlay remain usable at the named desktop viewport.

## Proof Required

- V05 integration and bUnit tests, production builds and final static enforcement.
- Managed file/hash, node parent/identity and canonical readback evidence from a real authorized tool call; analytics/cancel negative fixture.
- Reviewed screenshots evidence/SB05-desktop-normal.png and evidence/SB05-desktop-runtime-details.png with refresh assertions; failures recorded explicitly.
- Record exact commands, expected/actual discovery and exit codes. Zero or unexpected discovery is a failed proof.
- Use [semantic evidence rules](../../plan/validation-plan.md#semantic-evidence) and preserve both positive and adversarial negative results.

## Browser Validation Logging

- Route: /projects/{disposableProjectId}/structure; open contextual agent chat and runtime details.
- Viewport: 2048×1100 CSS pixels, desktop. No mobile scope.
- Playwright MCP: navigate, select the intended parent, submit the agent request, await terminal state, assert node visibility without manual reload, inspect safe failure/receipt details.
- Reviewed screenshots evidence/SB05-desktop-normal.png and evidence/SB05-desktop-runtime-details.png with refresh assertions; failures recorded explicitly.
- Review screenshots for normal canvas and open-overlay states, first-viewport graph access, scroll ownership and clipping. Record actions, actual assertions and screenshot findings in the execution report.

## Progression Gate

- Proceed only after canonical and visible state agree for successful and partial-effect failed runs, with safe scope/disposal proof.
- Requery Components MCP and inspect existing component usage before any new markup API is selected.

## Reopen Triggers

- Changes to storage commit order, effect identity, notification filtering/deduplication, run outcome, scoped context lifecycle or canvas reload reopen SB05/SB06.

## Suggested Agent Prompt

```text
Execute this subbundle only after the user authorizes implementation. Verify prerequisites and current source. Preserve the outcome, ownership and scope contracts. Capture the required production-path proof, update the execution report, and stop progression if its gate fails. Do not infer implementation permission from this prepared bundle.
```
