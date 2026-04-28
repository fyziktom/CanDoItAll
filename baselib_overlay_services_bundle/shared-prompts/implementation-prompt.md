# Implementation Prompt

Implement the selected subbundle only.

## Required Discipline

- Read `README.md`, `plan/01-phase-plan.md`, this subbundle README, and `traceability/01-requirement-traceability.md` first.
- Keep Radzen usage as architecture reference only; do not copy Radzen CSS or add a Radzen package dependency.
- Preserve existing direct BaseLib component APIs unless the subbundle explicitly says otherwise.
- Use Tailwind classes and existing BaseLib layout primitives for rendered chrome.
- Add tests beside the relevant component tests.
- Update `reviews/01-execution-report.md` immediately after proof is captured.

## Common Validation Commands

```powershell
npm run tailwind:build
dotnet build src/CanDoItAll.Components.BaseLib/CanDoItAll.Components.BaseLib.csproj
dotnet build src/CanDoItAll.Components.Sandbox/CanDoItAll.Components.Sandbox.csproj
dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~DialogServiceTests|FullyQualifiedName~TooltipServiceTests|FullyQualifiedName~NotificationTests"
```
