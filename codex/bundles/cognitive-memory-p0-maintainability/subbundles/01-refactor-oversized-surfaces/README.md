# Refactor Oversized Surfaces

## Status

- `Completed`

## Objective

- Reduce Cognitive Memory maintainability risk by splitting oversized service/API/page surfaces into focused files without changing behavior.

## Success Criteria

- Advanced services are separated by use case.
- API DTOs and endpoint mapping are separated from the core API entry point where practical.
- Recall orchestration is split into focused partial files where safe.
- Blazor page extraction is performed with component/build/browser proof.

## Covered Inputs

- CM-P0-001 oversized surface split.

## Prerequisites

- Prepared-stage bundle validator passed.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Recall\CognitiveMemoryRecallServices.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Advanced\CognitiveMemoryAdvancedServices.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.CognitiveMemory\Pages\CognitiveMemoryPage.razor.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Web\Api\CognitiveMemoryApi.cs

## Deliverables

- Focused files for independent advanced services.
- Focused files for API DTOs and endpoint groups/helpers where practical.
- Focused recall partial files where practical.
- Test/build proof that behavior still compiles.

## Dependency Impact

- Later behavior changes should use the new focused files; weak compile proof here blocks all downstream implementation.

## Validation Depth

- Critical maintainability foundation.

## Implementation Steps

1. Split mechanically safe files first.
2. Build/test compile after structural splits.
3. Record any intentionally deferred UI decomposition with rationale.

## Scope Exceptions

- Full visual redesign is out of scope.
- Broader decomposition of older non-P0 large services is beta hardening.

## Do Not Do

- Do not change route names, API paths, or public behavior as part of structural splits.
- Do not hide compile failures by weakening tests.

## Acceptance Checklist

- File split compiles.
- DI registration remains valid.
- API routes still map.
- Any UI markup change has component/browser proof or is deferred explicitly.

## Proof Required

- Targeted build or test compile.
- Component/browser proof if rendered Blazor behavior changes.

## Proof Captured

- Advanced services split into focused service/support files.
- Recall orchestration split into focused partial files.
- Recall channels split into vector, workspace/signal, and graph expansion files.
- Recall internal types moved to `CognitiveMemoryRecallInternalTypes.cs`.
- API split into endpoint groups and DTO file.
- Page rendering helpers extracted into `CognitiveMemoryPage.Rendering.cs`.
- Page code-behind split into probe, settings/source operations, and formatting files.
- Ten tab bodies extracted under `Pages/Components`.
- `dotnet build src\CanDoItAll.Web\CanDoItAll.Web.csproj --no-restore -m:1 --verbosity:minimal` passed with 0 warnings and 0 errors.
- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemory" --logger "console;verbosity=minimal" -m:1` passed 1/1.

## Browser Validation Logging

- `/cognitive-memory` validated at 1440x1000 and 390x900.
- Settings tab operational controls rendered in both viewports.
- Narrow viewport had no horizontal overflow.
- Screenshots captured in `reviews/browser-proof`.

## Progression Gate

- Downstream subbundles may continue only after the refactored code compiles or the split is reverted/adjusted.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
