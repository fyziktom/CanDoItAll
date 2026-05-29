# SB02 Semantic Invariants

- Invariant ID: SB02-INV-001
- Source raw note: Harden workflow templates and domain validation so repository-owned graphs are rejected before runtime or persistence when they are invalid.
- Expected behavior: Template pack loading validates converted graphs with source context, and catalog/API save rejects semantically invalid definitions before storing them.
- Disallowed shallow implementation: Only checking YAML shape, only validating during publish, or allowing invalid definitions to persist.
- Failing-first test: `WorkflowTemplatePackLoaderTests.Load_rejects_semantically_invalid_template_graph_with_source_context` failed before the loader called semantic validation.
- Passing test: `WorkflowTemplatePackLoaderTests.Load_rejects_semantically_invalid_template_graph_with_source_context`, `WorkflowCatalogTests.CatalogRejectsInvalidDefinitionOnSave`, and `WorkflowApiIntegrationTests.Workflow_api_rejects_invalid_definition_on_save`.
- Changed source files: `repo://src/CanDoItAll.Modules.AgentFramework/Catalog/WorkflowTemplatePackLoader.cs`, `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowCatalogServices.cs`, `repo://src/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs`.
- Production assertions: `ValidateTemplateGraphs` converts template YAML to `WorkflowGraph`, uses `WorkflowDefinitionValidator`, and save paths call `ThrowIfValidationFailed` before records are stored.
- Red-team negative case: An edge targeting `missing-node` is rejected with the template key, source file, and edge id in the error context.
- Downstream dependency check: Workflow API integration confirms invalid definitions are rejected consistently through HTTP save.
