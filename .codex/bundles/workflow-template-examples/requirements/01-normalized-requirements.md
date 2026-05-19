# Normalized Requirements

| ID | Requirement | Observable success criteria | Owning subbundle |
| --- | --- | --- | --- |
| R1 | Workflow templates must be stored in external template files and loaded by the workflow template pack. | `manifest.yaml` references the new workflow files; `WorkflowTemplatePackLoader` loads their keys. | `01-template-pack-file-loading-foundation` |
| R2 | Preserve existing default workflow examples while adding new examples. | Existing keys such as `gmail-label-email-summary-to-project` and `office365-category-email-summary-to-project` remain loadable. | `01-template-pack-file-loading-foundation` |
| R3 | Add Gmail email task extraction into project structure. | New Gmail task template downloads labeled mail, extracts concrete tasks, creates task nodes under `projectId`/`nodeId`, stores fallback summaries, and marks the message processed. | `02-email-plugin-workflow-examples` |
| R4 | Add Office365 email task extraction into project structure. | New Office365 task template mirrors the Gmail task flow using category download and mark-processed executors. | `02-email-plugin-workflow-examples` |
| R5 | Keep email summary examples available for Gmail and Office365. | Existing summary template keys are still present and graph creation succeeds. | `02-email-plugin-workflow-examples` |
| R6 | Add a Mermaid graph generation example based on input files. | New template ingests source files, instructs the LLM to produce Mermaid fenced Markdown, and stores it as a project-structure markdown asset. | `03-file-analysis-workflow-examples` |
| R7 | Add a source-code file summary example. | New template ingests source code files and stores a concise code summary asset under the selected project-structure node. | `03-file-analysis-workflow-examples` |
| R8 | Add validation coverage for the template pack. | Targeted tests prove the new keys load and graph construction succeeds. | `03-file-analysis-workflow-examples` |
