# Branch Definition Model And Publish Guardrails

## Status

- `Completed`

## Objective

- Extend the canonical process definition model so a step can own multiple typed branch outcomes and an explicit decision-maker role, while publish, clone, import, and export remain deterministic.

## Covered Inputs

- `U003` Decision or if node support with explicit decision-maker role input.
- `U004` Multiple switch-style outputs instead of yes or no only.
- `A001` Legacy audit finding that branch semantics are missing.

## Prerequisites

- `subbundles/01-bundle-repair-and-live-gap-reconciliation` closure gate passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessCanvasTemplateCatalog.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.Sqlite\Migrations\AppDbContextModelSnapshot.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Migrations.PostgreSql\Migrations\AppDbContextModelSnapshot.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProcessesServiceIntegrationTests.cs`

## Deliverables

- Canonical branch outcome entity or equivalent typed branch data stored with process definitions.
- Explicit decision-maker role ownership for branching source steps.
- Definition save, load, clone, import, and export support for branch data.
- Publish validation that rejects invalid branch definitions deterministically.
- Additive migrations for both providers if persistence changes require them.

## Dependency Impact

- Runtime orchestration cannot be trusted until the definition contract is correct.
- UI authoring cannot be finished until the persisted model and publish rules are stable.
- Weak proof here invalidates every later runtime and browser-proof claim.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Add the typed branch data to the canonical definition model and editor model.
2. Persist and reload the new data through save, get-editor, clone, import, and export paths.
3. Add publish-time validation for invalid branch references and missing decision ownership.
4. Add migrations or snapshot updates for both providers.
5. Add targeted validation coverage for definition-side behavior.

## Scope Exceptions

- This phase does not yet change runtime branch activation or workspace browser behavior.

## Do Not Do

- Do not route branch selection through raw strings or free-text labels.
- Do not leave publish validation permissive when branch references are broken.
- Do not hide unsupported join semantics inside vague comments.

## Acceptance Checklist

- Branch outcomes are persisted canonically and survive round trips through editor and import or export.
- A branching source step stores a typed decision-maker role reference.
- Publish rejects invalid branch graphs or missing required branch ownership.
- Existing linear definitions remain valid.

## Proof Required

- Targeted .NET validation covering definition save or publish behavior.
- Successful build of the affected process projects.
- Evidence that invalid branching definitions fail deterministically.

## Browser Validation Logging

- N/A. Browser-visible authoring proof is owned by subbundle 04 after the definition contract is stable.

## Progression Gate

- Definition-side validation passes and the branch model is stable enough that runtime work no longer depends on guessing or temporary compatibility hacks.

## Suggested Agent Prompt

```text
Implement only the canonical definition-side branch model and publish guardrails. Keep the process definition canonical, use typed identifiers for outcomes, and stop before runtime or browser work.
```
