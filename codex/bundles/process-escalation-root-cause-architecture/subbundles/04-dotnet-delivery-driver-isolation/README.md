# Dotnet Delivery Driver Isolation

## Status

- `Ready`

## Objective

- Isolate .NET solution setup, build/test/run proof, and UI/browser proof policy behind domain-owned driver/template components instead of generic runtime or generic MAF workspace code.

## Success Criteria

- Generic runtime/application/adapter code does not gain .NET, Blazor, Calculator, Tetris, screenshot, or Playwright-specific completion rules.
- .NET delivery readiness and required proof are produced by .NET/software-delivery driver policy or templates.
- .NET scaffold/build/test/browser proof policy is unit-testable without launching the full process dispatcher.

## Covered Inputs

- R06 Domain Isolation.
- R07 .NET Delivery Driver Isolation.
- R05 Driver-Owned Recovery.
- Earlier domain leak concern around image analysis and software-development prompt assumptions.

## Prerequisites

- SB01 completed.
- SB02 completed.
- SB03 completed.
- Architecture checkpoint confirms generic diagnostics/readiness/recovery are available.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionState.cs`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Abstractions`
- `repo://src/Processes/Drivers/CanDoItAll.Processes.Drivers.Standard`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/WorkspaceImageAnalysisPromptNormalizer.cs`
- `repo://Templates/Processes/processes/software-delivery/definition.json`
- `repo://Templates/Processes/processes/dotnet-solution-setup/definition.json`
- `repo://Templates/Processes/processes/dotnet-development-slice/definition.json`
- `repo://Templates/Processes/processes/dotnet-ui-screenshot-writeback/definition.json`
- `repo://Templates/Agents/teams/dotnet-delivery`
- `repo://Templates/Agents/teams/visual-automation-templates`

## Deliverables

- Boundary decision: use existing driver projects or add a clearly justified .NET/software-delivery driver project.
- Driver-owned policy for .NET scaffold/setup proof, build/test receipts, runtime launch proof, and browser proof when applicable.
- Tests proving policy supports multiple app topics and non-UI shapes.
- Domain leak architecture tests covering generic runtime/application/adapter/MAF workspace prompt normalizer.
- Migration notes for any remaining old launch-variable compatibility.

## Dependency Impact

- SB05 uses the isolated driver policy to simplify templates.
- SB06 uses the isolated policy to replay simple .NET delivery and UI/browser proof flows.
- Weak isolation here reintroduces the last failed approach and must block downstream work.

## Validation Depth

- Process-critical domain isolation.

## Implementation Steps

1. Audit all generic runtime/application/adapter changes since the prior bundle for domain-specific rules.
2. Identify .NET proof rules currently embedded in prompts, launch variables, or adapter validation.
3. Choose the smallest domain-owned home for .NET delivery policy using the pattern selection records.
4. Move or wrap repeated .NET scaffold/build/test/browser proof definitions into typed policy builders/descriptors.
5. Wire policy through process driver/catalog composition without creating per-step heavy object graphs.
6. Add architecture tests that fail on domain strings in forbidden generic layers.
7. Add unit tests for .NET policy with Calculator-like, Tetris-like, and non-UI service/library scenarios.

## Scope Exceptions

- Do not redesign all process templates in this subbundle; only move policy needed for isolation.
- Do not remove compatibility launch variables until SB05 or a later migration proves templates no longer need them.

## Do Not Do

- Do not put .NET receipt validation into `AgentFrameworkProcessExecutionAdapter.ProductCompletionState.cs`.
- Do not add Playwright/screenshot requirements to generic runtime.
- Do not make Calculator or Tetris special cases.
- Do not create a broad new driver layer unless it removes real coupling and is test-covered.

## Acceptance Checklist

- Generic runtime/application files pass domain leak scan.
- .NET driver/policy tests cover scaffold, build/test, runtime launch, and browser proof requirements.
- Non-UI .NET scenario does not require browser proof.
- UI/browser scenario explicitly requires browser proof through readiness contract.
- `WorkspaceImageAnalysisPromptNormalizer` remains domain-neutral.

## Proof Required

- `dotnet test tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore --filter DotNet`
- `dotnet test tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter Process`
- Domain leak scan transcript over generic runtime/application/adapter/MAF workspace prompt files.
- CodeAnalytics or project-reference audit showing no new cycles.

## Browser Validation Logging

- N/A for implementation UI, but browser capability policy must be test-covered for UI-visible process steps.

## Progression Gate

- SB05 may start only when .NET/UI proof behavior has a tested domain-owned home and generic layers are clean.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Keep .NET delivery behavior out of generic runtime and adapter completion validation. Use the diagnostics, readiness, and recovery foundations from SB01-SB03 to isolate .NET scaffold/build/test/run/browser proof in domain-owned driver or template policy. Add architecture leak tests and multiple app-shape unit tests. Stop if the simplest solution requires adding .NET rules to generic process runtime.
```
