# Structured Input

## Objective

Add basic, discoverable workflow examples for common agent workflow scenarios, using the existing template pack loader and external workflow definition files.

## Constraints

- Workflow templates must live as files under `Templates\Workflows`.
- The manifest must load those files.
- Existing summary examples must remain available.
- Email task examples must create `WorkItem`/`task` nodes under the specified project-structure node.
- Gmail and Office365 examples must use their plugin executor IDs instead of generic file-only mocks.
- File-analysis examples must consume input files through source ingestion and store project-structure markdown assets when a project/node is supplied.

## Success Signals

- New workflow keys are present in `WorkflowTemplatePackLoader().Load().Workflows`.
- New workflow graphs compile through `WorkflowTemplatePack.CreateGraph`.
- Email task workflows include plugin download, LLM classification, project-structure task creation, summary fallback, and processed-message marking.
- Mermaid and source-code workflows include source ingestion, LLM transformation, and project-structure asset creation.
- No new C# code constructs the workflow graphs directly.
