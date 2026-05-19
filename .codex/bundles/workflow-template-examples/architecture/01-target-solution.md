# Target Solution

## Shape

- Keep the template system data-driven by adding new YAML files under `C:\repositories\CanDoItAll\Templates\Workflows\workflows`.
- Update `C:\repositories\CanDoItAll\Templates\Workflows\manifest.yaml` to reference those files through `workflowFiles` and bump `seedVersion`.
- Add tests that load the manifest and compile representative graphs through `WorkflowTemplatePack.CreateGraph`.

## New Template Files

- `email-plugin-task-workflows.yaml`: Gmail and Office365 task-extraction examples.
- `file-analysis-workflows.yaml`: Mermaid graph generation and source-code summary examples.

## Boundaries

- Do not add C# switch statements or literals for these workflow definitions.
- Do not change plugin OAuth behavior or executor implementation unless template validation proves a settings mismatch.
- Do not remove or rename existing workflow keys unless a duplicate-key validation failure forces a repair.

## Runtime Contracts

- Email task templates use plugin executor output fields `projectId`, `nodeId`, `runContext.gmailProcessing`, and `runContext.office365Processing`.
- Project-structure `CreateTaskNodes` consumes `$.tasks` and writes `WorkItem` nodes with subtype `task`.
- Project-structure `CreateAsset` consumes the LLM `markdown` field when `contentFromInput: true`.
- Source ingestion consumes input `sources`, project node media paths, selected nodes, or parent subtree paths and returns `sourceDocuments` for LLM processing.
