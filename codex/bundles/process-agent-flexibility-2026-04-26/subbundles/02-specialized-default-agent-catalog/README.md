# Specialized default agent catalog

## Status

- `Completed`

## Objective

Add specialized managed default agents for .NET, JavaScript, business strategy, financial strategy, and marketing so specialization lives with agents and capabilities instead of the base process prompt.

## Covered Inputs

- `N002`: Specialized .NET architect/developer/QA agents.
- `N003`: Specialized JS architect/developer/QA agents.
- `N004`: Business strategist, financial strategist, and marketing specialist agents.

## Prerequisites

- Subbundle 01 completed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\Seeds\SandboxWorkspaceSeedBuilder.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\Seeds\SandboxWorkspaceSeedNormalizer.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Providers\Seeds\ManagedSeedProviderFallbacks.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\SeedAssets\manifest.json`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\SeedAssets\instructions\agents`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\AgentFrameworkWorkspaceSeedIntegrationTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\ManagedSeedProviderFallbacksTests.cs`

## Deliverables

- New managed seed agents:
  - .NET Solution Architect
  - .NET Application Developer
  - .NET QA Review Lead
  - JavaScript Solution Architect
  - JavaScript Application Developer
  - JavaScript QA Review Lead
  - Business Strategist
  - Financial Strategist
  - Marketing Specialist
- Instruction assets for each agent.
- Managed seed refresh/fallback key updates.
- Seed tests for presence, capability assignments, and non-code agent neutrality.

## Dependency Impact

- Subbundle 03 uses these agents as the intended staffing model for business-plan process roles.
- Subbundle 04 uses these agents in mock/real validation. If seed coverage is wrong, real process proof is not meaningful.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Add instruction asset files under `SeedAssets/instructions/agents`.
2. Register the assets in the seed manifest if required by the asset loader.
3. Add stable IDs and agent definitions in `SandboxWorkspaceSeedBuilder`.
4. Assign capabilities conservatively by role.
5. Update managed seed template-key sets in fallback and normalizer code.
6. Add tests asserting the new agents exist and are specialized.

## Scope Exceptions

- Do not create a dedicated JavaScript skill unless the repository already provides one or the validation shows it is necessary.

## Do Not Do

- Do not give business/finance/marketing agents write/delete/build/test capabilities they do not need.
- Do not make JS agents depend on .NET-only skills.
- Do not remove existing broad agents; specialized agents should complement them.

## Acceptance Checklist

- All listed agents are present in `SandboxWorkspaceSeedFactory.Create()`.
- Managed seed fallback recognizes all new template keys.
- Managed seed normalizer refreshes the new template keys.
- .NET agents include .NET/ASP.NET/test capability assignments.
- JS agents include workspace/frontend/provider capabilities without .NET-only capability assumptions.
- Business/finance/marketing instructions avoid coding defaults.

## Proof Required

- Targeted seed catalog tests.
- Targeted managed seed fallback tests.

## Completion Proof

- Added nine managed default agents and instruction assets: .NET architect/developer/QA, JavaScript architect/developer/QA, business strategist, financial strategist, and marketing specialist.
- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~ManagedSeedProviderFallbacksTests" --no-restore` passed.
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~AgentFrameworkWorkspaceSeedIntegrationTests|FullyQualifiedName~BusinessPlanProcessPostgresIntegrationTests|FullyQualifiedName~LiveSpecialistAgentScenarioIntegrationTests" --no-restore` passed, including seed catalog tests.

## Browser Validation Logging

- N/A. This subbundle affects seed/catalog data, not browser UI.

## Progression Gate

- Downstream process-template work may proceed only after seed tests pass and new agents can be resolved by template key.

## Suggested Agent Prompt

```text
Implement subbundle 02 only. Add specialized managed seed agents and tests. Keep capability assignment minimal and role-specific.
```
