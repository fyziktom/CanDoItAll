# SB10 Semantic Invariants

## Result

- Status: `Passed`
- Subbundle: `SB10 - Workflow Template And Descriptor Loading`
- Closure date: `2026-06-29`

## Invariants

| Id | Invariant | Proof |
| --- | --- | --- |
| SB10-I01 | Workflow template loading is owned by `CanDoItAll.AgentFramework.Workflows.Templates`, not the Blazor module. | `transcripts/static-ownership-and-responsibility-check.txt`; old `Modules.AgentFramework/Catalog/WorkflowTemplatePackLoader.cs` absent; `WorkflowTemplatePackLoader` class exists only in the template project. |
| SB10-I02 | Current `Templates/Workflows` template keys, runtime policy, input parameters, and preview simulations load with parity. | `transcripts/all-template-and-negative-tests.txt`; `WorkflowTemplatePackLoaderTests.Load_default_pack_materializes_every_current_template_and_preview_fixture`. |
| SB10-I03 | Template graph materialization uses SB02 workflow builders rather than a duplicate hand-built graph path. | `transcripts/semantic-source-assertions.txt`; `WorkflowTemplateGraphMaterializer` uses `WorkflowDefinitionBuilder`, `WorkflowNodeBuilder`, `WorkflowEdgeBuilder`, and `WorkflowPortBuilder`. |
| SB10-I04 | Template executor references validate against the executor descriptor catalog when a catalog is supplied. | `transcripts/all-template-and-negative-tests.txt`; missing executor and invalid settings tests fail with typed template diagnostics. |
| SB10-I05 | Malformed template and preview fixture failures are explicit, typed, repairable, and include known file/key/YAML path/node/executor context. | `transcripts/semantic-source-assertions.txt`; `WorkflowTemplatePackException` and `WorkflowTemplateDiagnostic`; malformed YAML/JSON/routing/input/runtime/settings tests. |
| SB10-I06 | The moved template code is split by responsibility instead of copied as a monolith. | `transcripts/static-ownership-and-responsibility-check.txt`; parsing, DTO, input, model, graph, preview, validation, and diagnostic files are separate. |
| SB10-I07 | Scheduler input consumers remain coupled to model input contracts, while SB10 materializes those descriptors in the template project. | `transcripts/semantic-source-assertions.txt`; workbook row `R10/R12`; scheduler scans show model input contracts rather than UI-owned template DTOs. |
| SB10-I08 | No UI fallback path or silent invalid-template skip was introduced. | `transcripts/anti-stub-audit.txt`; UI-owned loader file absent; invalid YAML/JSON and descriptor failures throw typed exceptions. |

## Browser Scope

SB10 did not intentionally change visible workflow template selection behavior. Browser proof remains owned by SB12/SB13/SB14. Small and medium viewport UI tests are skipped for this initiative per the user instruction that the app is large-screen-only.
