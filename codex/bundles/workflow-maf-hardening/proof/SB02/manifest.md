# SB02 Proof Manifest

## Scope

Workflow domain model and template loader hardening.

## Changed File Hashes

- `51c85b53a9699fda6900d6244ba57bb80a7fe61bc76e909ef6cd7820aada6746` `repo://src/CanDoItAll.Modules.AgentFramework/Catalog/WorkflowTemplatePackLoader.cs`
- `a7487a1d4326d44ab7278e0093a71e3b1bd70a440958dbb3f276f6c40eb85bef` `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowCatalogServices.cs`
- `f703687e43ad5fa43fb69a8b5a03ab07b92647768d7f47b8f33883f9d2757555` `repo://src/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs`
- `691f6952254c07d3cdc8c390385a198442643d59e8e3e415547be07b72022896` `repo://tests/CanDoItAll.Tests.Unit/WorkflowTemplatePackLoaderTests.cs`
- `cc8933123f35a50906175a0e0c1d0082848e7eca8bdff5f8de24ce0752c500f3` `repo://tests/CanDoItAll.Tests.Unit/WorkflowCatalogTests.cs`
- `464f4b3a7aeaf5ca8d13e561c0dc957b8e8c3fdbf9bcd82d2dafe09995a05dd4` `repo://tests/CanDoItAll.Tests.Integration/WorkflowApiIntegrationTests.cs`

## Evidence

- Semantic invariant contract: `bundle://proof/SB02/semantic-invariants.md`
- Failing-first transcript: `bundle://proof/SB02/transcripts/proof-summary.txt`
- Passing transcript: `bundle://proof/SB02/transcripts/proof-summary.txt`
- Anti-stub audit transcript: `bundle://proof/SB02/transcripts/proof-summary.txt`

## Cited Tests

- Test name: `CanDoItAll.Tests.Unit.WorkflowTemplatePackLoaderTests.Load_rejects_semantically_invalid_template_graph_with_source_context`
- Test name: `CanDoItAll.Tests.Unit.WorkflowCatalogTests.CatalogRejectsInvalidDefinitionOnSave`
- Test name: `CanDoItAll.Tests.Integration.WorkflowApiIntegrationTests.Workflow_api_rejects_invalid_definition_on_save`

## Invariants

- Invariant ID: `SB02-INV-001`
