# Subbundle 04 - Recall Vector Beta Proof

## Status

- `Completed`

## Objective

- Prove recall uses the Qdrant-backed vector projection channel after live projection rebuild.
- Confirm operators can inspect the result through API and UI surfaces.

## Covered Inputs

- CM-BETA-004: validate live vector recall against Docker Qdrant.
- CM-BETA-005: prove operator visibility for the vector stage.
- CM-BETA-001: close remaining P1 beta proof if vector recall succeeds.

## Prerequisites

- Subbundle 03 projection rebuild proof has succeeded.
- The running app has a durable memory/projection dataset suitable for a deterministic query.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Recall\CognitiveMemoryRecallServices.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Recall\CognitiveMemoryRecallVectorChannel.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Recall\CognitiveMemoryRecallChannels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.AdvancedEndpoints.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\Components\CognitiveMemoryHealthTab.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\Components\CognitiveMemoryRecallTracesTab.razor`

## Deliverables

- Recall API response with vector projection stage completed.
- Persisted trace/audit proof that the vector stage is visible.
- Browser proof for the Cognitive Memory health/trace UI where practical.

## Dependency Impact

- May touch recall vector channel, trace persistence, API DTOs, or UI trace rendering if runtime proof exposes a defect.
- Must preserve lexical fallback behavior while making vector-provider failures explicit.

## Validation Depth

- Execute recall against the running app.
- Confirm vector stage is `Completed`, not `Skipped` or `Unavailable`, for the deterministic query.
- Capture UI proof if the app exposes the trace.

## Implementation Steps

1. Execute a recall/probe request using the durable projection dataset.
2. Inspect response stages, candidates, warnings, and context pack.
3. Inspect persisted/operator trace data through API/UI.
4. Fix defects only when vector proof is blocked by implementation behavior.
5. Rerun targeted tests and browser proof after fixes.

## Do Not Do

- Do not count lexical-only recall as vector beta proof.
- Do not suppress provider warnings.
- Do not add a production endpoint only for test convenience unless no existing public path can prove the behavior and the endpoint is justified.

## Acceptance Checklist

- Recall response includes a completed vector projection stage.
- At least one selected candidate is sourced through vector projection or the trace clearly proves vector contribution.
- Operator trace/health surface shows the vector-provider state.

## Proof Required

- Recall API response summary with trace/stage status.
- UI screenshot or API trace proof showing vector stage status.
- Test/build output after any code fix.

## Browser Validation Logging

- Capture desktop and mobile screenshots plus console logs under `reviews/browser-proof` if browser validation is used.
- Record viewport, URL, and artifact names in the execution report.

## Progression Gate

- Continue to beta docs closure only when live recall proves vector projection or a documented blocker remains.

## Suggested Agent Prompt

```text
Run live recall against the rebuilt Qdrant projection and prove the vector projection stage completes. Capture API and operator-surface evidence, then repair the smallest defect if the stage is skipped or unavailable.
```
