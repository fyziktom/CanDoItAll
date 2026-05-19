# Source Artifacts

| Artifact | Path or source | Purpose |
| --- | --- | --- |
| User request | `C:\repositories\CanDoItAll\.codex\bundles\workflow-template-examples\inputs\00-original-request.md` | Raw scope and hard constraints. |
| Workflow template manifest | `C:\repositories\CanDoItAll\Templates\Workflows\manifest.yaml` | Existing data-pack entry point and seed metadata. |
| Existing workflow examples | `C:\repositories\CanDoItAll\Templates\Workflows\workflows\default-workflows.yaml` | Current template format and existing examples. |
| Template loader | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Catalog\WorkflowTemplatePackLoader.cs` | Confirms templates can be loaded from manifest-listed files. |
| Seed service | `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Catalog\WorkflowExampleCatalogSeedService.cs` | Confirms definitions are seeded from template pack data, not hard-coded graphs. |
| Plugin executors | `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Gmail\GmailWorkflowExecutor.cs`, `C:\repositories\CanDoItAll\src\plugins\CanDoItAll.Plugin.Office365\Office365WorkflowExecutor.cs` | Confirms Gmail and Office365 executor IDs, payload shape, and preview simulations. |
| Project-structure executor | `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows\ProjectStructureWorkflowExecutor.cs` | Confirms CreateAsset and CreateTaskNodes settings and JSON paths. |
| Source ingestion executor | `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workflows\SourceIngestionWorkflowExecutor.cs` | Confirms file/folder source ingestion and allowed-extension settings. |
