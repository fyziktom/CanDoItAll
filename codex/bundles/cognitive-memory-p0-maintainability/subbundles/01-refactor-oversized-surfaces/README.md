# Refactor Oversized Surfaces

## Status

- `Completed`

## Objective

- Reduce Cognitive Memory maintainability risk by splitting oversized service/API/page surfaces into focused files without changing behavior.

## Success Criteria

- Advanced services are separated by use case.
- API DTOs and endpoint mapping are separated from the core API entry point where practical.
- Recall orchestration is split into focused partial files where safe.
- Blazor page extraction is performed only where component/build/browser proof can support it.

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
- Full UI child-component split was deferred. This pass extracted rendering helpers from the code-behind without changing Razor markup or browser behavior; the roadmap now carries child-component decomposition as a P0 residual/beta-hardening item.

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
- API split into endpoint groups and DTO file.
- Page rendering helpers extracted into `CognitiveMemoryPage.Rendering.cs`.
- `dotnet build src\CanDoItAll.Web\CanDoItAll.Web.csproj --no-restore -m:1 --verbosity:minimal` passed with 0 warnings and 0 errors.
- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --no-restore --filter "FullyQualifiedName~CognitiveMemory" --logger "console;verbosity=minimal" -m:1` passed 1/1.

## Browser Validation Logging

- Conditional: if Blazor markup behavior changes, validate `/cognitive-memory` at large and narrow viewports and record screenshots. Otherwise record N/A because structural C# splits do not affect rendered UI.

## Progression Gate

- Downstream subbundles may continue only after the refactored code compiles or the split is reverted/adjusted.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
