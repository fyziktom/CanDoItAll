# Current State

## Workflow Template Pack

- `C:\repositories\CanDoItAll\Templates\Workflows\manifest.yaml` defines the template pack metadata, component defaults, runtime policy, executor policies, node instruction defaults, and a list of `workflowFiles`.
- `C:\repositories\CanDoItAll\Templates\Workflows\workflows\default-workflows.yaml` currently contains many examples in one YAML file, including existing Gmail and Office365 email summary examples.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Catalog\WorkflowTemplatePackLoader.cs` already supports multiple manifest-listed workflow files and rejects duplicate workflow keys.

## Seeding Path

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Catalog\WorkflowExampleCatalogSeedService.cs` loads `WorkflowTemplatePackLoader.Load()`, creates one LLM component per template, creates workflow graphs from data, and saves managed example definitions.
- Existing seed logic skips non-managed definitions with matching names, so adding template files should not overwrite user-managed workflows.
- Updating `seedVersion` is required so already-managed workflows refresh when templates change.

## Executor Support

- Gmail executor IDs are `gmail.messages-by-label` and `gmail.mark-message-processed`.
- Office365 executor IDs are `office365.messages-by-category` and `office365.mark-message-processed`.
- The project-structure executor supports `CreateAsset` and `CreateTaskNodes`; task creation reads `projectId`, `nodeId`, and `tasks` from JSON paths.
- The source ingestion executor can read supplied file/folder sources into bounded text and can be configured with code-file extensions.

## Current Gap

- Gmail and Office365 summary templates exist, but the requested plugin-backed task extraction examples do not exist.
- Mermaid graph generation and source-code summary examples do not exist as first-class workflow templates.
- New examples should be split into own workflow template files and referenced from the manifest, rather than expanding the already-large default workflow file.
