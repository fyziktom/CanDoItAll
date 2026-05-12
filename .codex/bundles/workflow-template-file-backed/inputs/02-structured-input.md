# Structured Input

## Core Objective

- Externalize every default workflow example from compiled C# into durable text files.

## Success Criteria

- Default workflow examples live under `Templates\Workflows`.
- Template definitions are YAML files.
- The seed service reads templates from files and no longer builds default workflow graphs in code.
- Every loaded template maps into a valid strongly typed `WorkflowDefinition`.
- Targeted build and unit tests pass.

## Hard Constraints

- Do not silently fall back to compiled default graphs when YAML loading fails.
- Do not introduce stringly typed runtime logic where typed ids, enums, or options already exist.
- Do not switch existing CanDoItAll workflow definitions to MAF Foundry `AdaptiveDialog` YAML in this change; that is a broader runtime migration.

## Allowed Side Effects

- Add a workflow-template pack under `Templates\Workflows`.
- Add typed loader and DTO classes in the AgentFramework module/catalog boundary.
- Add a YAML parsing dependency if needed.
- Update focused unit tests and seed service tests.

## Source Artifacts

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Catalog\WorkflowExampleCatalogSeedService.cs`
- `C:\repositories\CanDoItAll\Templates\Processes\manifest.json`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Templates\ProcessTemplatePackLoader.cs`
- `C:\repositories\agent-framework\dotnet\samples\03-workflows`

## Input Coverage Signals

- The phrase "default workflows templates" means all current compiled example workflows, not only one sample.
- The phrase "as text files" means durable repository files, not embedded string constants or resources.
- The MAF reference is a pattern for YAML loading/storage, not an instruction to replace the CanDoItAll workflow domain schema.

## Dependency And Sequencing Signals

- Template pack and loader must be valid before the seed service can be converted.
- Seed conversion must complete before closure validation can prove the compiled defaults are gone.

## Validation Expectations

- Unit tests load every YAML workflow template.
- Unit tests validate each loaded workflow with `WorkflowDefinitionValidator`.
- Unit tests or source inspection prove the seed service no longer contains compiled default workflow graph builders.

## Evidence Contract

- `validate_bundle.py --stage prepared --profile initiative`
- Focused unit test command for workflow template loading and seeding.
- Targeted build for affected AgentFramework projects.
- `validate_bundle.py --stage completed --profile initiative`

## UI Validation Strategy

- N/A. This is a backend/template storage change.

## Browser Validation Analytics

- Record an N/A browser analytics row in the execution report for each completed subbundle.

## Working Assumptions

- `Templates\Workflows` is the correct sibling to `Templates\Processes`.
- Existing default examples should preserve names, descriptions, graph topology, executor settings, and component prompt contract unless validation exposes a defect.

## Primary Risks

- YAML schema can become too generic and stringly typed.
- Missing validator coverage can allow malformed templates into startup seeding.
- Managed seed refresh semantics can regress if template versioning is not explicit.
