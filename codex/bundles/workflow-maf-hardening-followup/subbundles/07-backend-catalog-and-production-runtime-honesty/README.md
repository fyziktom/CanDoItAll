# 07-backend-catalog-and-production-runtime-honesty

## Objective

Align backend catalog, runtime policy, UI/API visibility, and registered backend implementations so users cannot accidentally believe production durable workflows are supported when only in-process execution is registered.

## Current problem

The catalog lists InProcess, DurableTask, and AzureFunctions descriptors. Reviewed service registrations only register the in-process backend. The runtime manager fails when a backend is requested and not registered, which is good, but catalog/UI can still imply unavailable backends are normal choices.

## Exact source references

- `src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowDefinitionValidator.cs`
- `src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowRuntimeManager.cs`
- `src/CanDoItAll.AgentFramework.Models/Workflows/*Runtime*`
- `src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor*`
- `src/CanDoItAll.Modules.AgentFramework/Pages/Components/*`
- `src/CanDoItAll.AgentFramework.Hosting/AgentFrameworkServiceCollectionExtensions.cs`
- `src/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`

## Implementation steps

1. Split backend descriptors into:
   - registered runtime backends,
   - planned/unregistered backend options.
2. Add availability fields to backend catalog descriptors.
3. Update runtime policy validation:
   - production durable requirement cannot save/publish if no durable backend is registered,
   - in-process preview can be allowed explicitly.
4. Update UI/API surfaces:
   - show DurableTask/AzureFunctions as planned/unavailable unless registered,
   - disable run buttons for unavailable production backend,
   - provide operational notes.
5. If a stub backend is kept, make it a clear unavailable backend that throws a typed exception with user-friendly message.

## Do not do

- Do not silently substitute InProcess for DurableTask/AzureFunctions.
- Do not mark planned backends as runnable.
- Do not implement a real durable backend in this subbundle unless it remains small and fully testable.

## Acceptance checklist

- Backend catalog reflects registered runtime state.
- Save/publish validation catches impossible production runtime policies.
- UI/API cannot start unavailable durable backends without explicit registration.
- Tests prove backend honesty.

## Proof required

- Unit tests for backend catalog and runtime policy validation.
- Integration/API tests for unavailable backend start.
- Component/UI tests for disabled/unavailable backend presentation if UI touched.
