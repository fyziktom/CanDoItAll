# 03 mock agent staffing alignment

## Status

- `Completed`

## Objective

Make process role staffing deterministic so launch plans and process runs bind the intended mock technical agents for the calculator flow.

## Covered Inputs

- REQ-006: deterministic mock-agent staffing.
- REQ-007: settings-gated mock execution with no real LLM calls.

## Prerequisites

- Subbundle 01 progression gate must pass.
- Subbundle 02 process role keys should be known before final role alias implementation.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Hosting\ProcessMockAgentSupport.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Hosting\ProcessMockAgentCatalogService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Hosting\ProcessMockAgentRuntime.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Launch.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Launch.CandidateDiscovery.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Launch.Staffing.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Launch.Provisioning.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessMockAgentRuntimeIntegrationTests.cs`

## Deliverables

- Explicit role alias or role mapping strategy for mock roles used by the calculator process.
- Mock agent catalog metadata rich enough for launch candidate scoring to choose the intended agent.
- Test helper or service API that can bind mock agents into a launch plan or project assignment deterministically.
- Tests proving the process launch plan selected candidates are the intended mock agents.

## Dependency Impact

- This is a critical foundation for subbundles 04 and 05.
- If role binding is nondeterministic, dispatcher failures can look like process logic failures.

## Validation Depth

- Critical foundation.
- Catalog, CRM-HR projection, launch staffing, and provider-gating validation.

## Implementation Steps

1. Compare calculator process role keys against `ProcessMockAgentRoleKeys`.
2. Add explicit aliases or update the deterministic process role keys so each process role maps to one mock role.
3. Ensure `ProcessMockAgentCatalogService` seeds party tags, role tags, capabilities, and metadata needed by launch candidate scoring.
4. Add a focused launch/staffing test that enables mock mode, seeds the catalog, creates a process launch plan, and asserts selected candidates by role.
5. Assert the selected candidates have bound technical agent IDs and use the mock provider/model.
6. Assert disabled mock mode does not auto-staff mock provider agents.

## Scope Exceptions

- Do not provision new real AI agents.
- Do not rely on broad role keyword scoring as the only proof of deterministic binding.

## Do Not Do

- Do not make mock agents globally active when settings are disabled.
- Do not add magic strings spread across tests and production; centralize role aliases or constants.
- Do not change person-only production roles in generic templates just to make mock tests pass.

## Acceptance Checklist

- Every calculator process role has exactly one expected mock party/technical agent binding.
- Launch plan candidate recommendations are deterministic.
- Mock provider is the only provider used in the mock process path.
- Disabled mock mode leaves the catalog suspended or absent as currently intended.

## Proof Required

- Focused integration test for mock catalog seeding and launch staffing.
- Existing `ProcessMockAgentRuntimeIntegrationTests` still pass.
- Execution report includes selected role -> party ID -> technical agent ID mappings.

## Browser Validation Logging

- N/A. Backend staffing/catalog behavior only.

## Progression Gate

- Subbundle 04 may proceed only after launch/staffing proof shows deterministic mock technical agent bindings for all roles needed by the calculator process.

## Suggested Agent Prompt

```text
Implement subbundle 03 only. Align mock role catalog/staffing so the deterministic calculator process binds exact mock agents through launch planning. Preserve settings gating and prove no real provider is selected.
```
