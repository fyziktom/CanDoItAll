# seed-service-conversion

## Status

- `Completed`

## Objective

- Convert `WorkflowExampleCatalogSeedService` so default workflow seeding is driven by the file-backed YAML template pack rather than compiled example specs and graph builders.

## Covered Inputs

- R1 default workflows must not be compiled in code.
- R4 seeding must preserve managed refresh semantics, sample assets, names, descriptions, component settings, and graph behavior.
- R5 no silent fallback to compiled defaults.

## Prerequisites

- Subbundle 01 closure gate passed.
- All default templates load and validate from YAML.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Catalog\WorkflowExampleCatalogSeedService.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Services\AgentFrameworkModuleServiceCollectionExtensions.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowCatalogServices.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workflows\WorkflowCatalogContracts.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\WorkflowCatalogTests.cs

## Deliverables

- `WorkflowExampleCatalogSeedService` loads `WorkflowTemplateDefinition` data from the template loader.
- Managed seed marker/version behavior remains explicit.
- Sample workspace asset seeding remains in the service or is moved to typed file-backed data if that is smaller and clearer.
- Tests prove seed service persists file-backed workflows and components.

## Dependency Impact

- Subbundle 03 depends on this phase to prove the user request is actually implemented, not only planned in files.
- Weak proof here would leave the system still dependent on compiled default templates.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Inject or construct the workflow-template loader in the seed service using existing DI patterns.
2. Replace `BuildExampleSpecs()` iteration with loaded template iteration.
3. Create or update LLM components from template metadata and provider selection.
4. Save workflow definitions from loaded graph templates while preserving the managed marker/version conflict behavior.
5. Remove obsolete compiled graph-builder helpers and default example spec records.
6. Add focused tests for file-backed seeding and managed refresh behavior.

## Scope Exceptions

- No UI catalogue or sharing API is added.
- No change to process templates or process-template importing.

## Do Not Do

- Do not leave old compiled graph builders as fallback behavior.
- Do not weaken conflict protection for non-managed user definitions.
- Do not create a new persistence service when existing catalog interfaces already handle definitions and components.

## Acceptance Checklist

- Seed service no longer contains compiled default workflow graph builders.
- Seeded workflow count comes from loaded templates.
- Existing managed definition refresh behavior is preserved.
- User-owned definitions with the same name are still skipped with a warning.

## Proof Required

- Unit tests for seeding from the file-backed pack.
- Targeted build of the affected module project.
- Execution report row with closure gate result.

## Browser Validation Logging

- N/A. Backend/template storage change with no browser-visible behavior.

## Progression Gate

- Subbundle 03 may start only after source inspection and tests prove the default seed path is file-backed.

## Suggested Agent Prompt

```text
Implement subbundle 02 only. Convert workflow example seeding to consume the file-backed YAML templates, preserve managed seed semantics, remove compiled default graph builders, and add focused regression tests.
```
