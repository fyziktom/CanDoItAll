# 07-backend-catalog-and-production-runtime-honesty

## Status

- Status: `Completed`

## Objective

Align backend catalog, runtime policy, UI/API visibility, and registered backend implementations so users cannot believe production durable workflows are supported when only in-process execution is registered.

## Covered Inputs

- R9: Align backend catalog with actually registered/runnable backends.
- R5: Keep checkpoint/resume availability honest.

## Prerequisites

- SB06 plugin governance is completed or blocked with explicit runtime-catalog impact.
- Registered backend services and UI/API surfaces are inspected in current repo state.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowDefinitionValidator.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowRuntimeManager.cs`
- `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowModels.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor.cs`
- `repo://src/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`

## Scope

- Split registered/runnable backend descriptors from planned/unregistered options.
- Add backend availability fields.
- Validate production durable policy against actual registered backend capabilities.
- Update UI/API surfaces if needed so unavailable durable backends are disabled and explained.

## Dependency Impact

- SB08 final proof must show users cannot start unavailable durable backends and cannot silently fall back to in-process.

## Validation Depth

- Unit backend catalog and runtime policy tests, integration/API unavailable-backend start test, and component/UI proof if UI changes.
- Critical proof requires adversarial unavailable-backend negative case.

## Implementation Steps

1. Split backend descriptors into registered runtime backends and planned/unregistered backend options.
2. Add availability fields to backend catalog descriptors.
3. Update runtime policy validation for impossible production durable policies.
4. Update UI/API surfaces to show DurableTask/AzureFunctions as planned/unavailable unless registered.
5. Keep any stub backend clearly unavailable with typed user-friendly exception.

## Do Not Do

- Do not silently substitute InProcess for DurableTask or AzureFunctions.
- Do not mark planned backends as runnable.
- Do not implement a real durable backend unless it remains small and fully testable.

## Acceptance Checklist

- Backend catalog reflects registered runtime state.
- Save/publish validation catches impossible production runtime policies.
- UI/API cannot start unavailable durable backends without explicit registration.
- Tests prove backend honesty.

## Proof Required

- Unit tests for backend catalog and runtime policy validation.
- Integration/API tests for unavailable backend start.
- Component/UI tests and browser proof if UI is touched.
- `bundle://proof/SB07/manifest.md` and `bundle://proof/SB07/semantic-invariants.md`.

## Browser Validation Logging

- Browser proof is required if workflow page UI is changed; otherwise API/component tests must be cited as not-applicable browser proof.

## Progression Gate

- Continue to SB08 only after backend runtime availability is explicit in catalog, validation, and any touched UI/API surfaces.

Result: `Passed`. Backend availability is explicit in catalog descriptors, API responses, workflow editor UI, save/test-run/start validation, and in-process defaults. Proof is recorded in `bundle://proof/SB07/manifest.md` and `bundle://proof/SB07/semantic-invariants.md`.

## Suggested Agent Prompt

Make workflow backend descriptors honest about registered/runnable versus planned/unavailable backends, then prove unavailable durable backends cannot run or be saved as production policy.
