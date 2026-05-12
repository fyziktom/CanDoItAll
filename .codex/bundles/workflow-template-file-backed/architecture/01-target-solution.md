# Target Solution

## Architecture

- Add a `Templates\Workflows` pack with a manifest and per-workflow YAML files.
- Add typed workflow-template DTOs and a loader in the AgentFramework module/catalog boundary.
- Keep application seeding in `WorkflowExampleCatalogSeedService`; remove compiled default example graph construction from that service.
- Convert file-backed templates into existing `LlmCallComponentSaveRequest` and `WorkflowDefinitionSaveRequest` objects before saving.

## Format Choice

- Use YAML files for default workflow templates.
- Use CanDoItAll workflow graph terminology (`nodes`, `edges`, `routing`, `executor`) rather than MAF's Foundry `AdaptiveDialog` YAML schema.
- Use a reserved component placeholder in YAML for default LLM component nodes, then replace it with the actual saved component id while seeding.

## Boundaries

- No UI workflow catalogue in this bundle.
- No MAF declarative runtime migration in this bundle.
- No silent fallback to compiled defaults.
