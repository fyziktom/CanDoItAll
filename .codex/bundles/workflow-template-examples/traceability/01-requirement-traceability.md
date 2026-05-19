# Requirement Traceability

| Requirement | Source input | Owning subbundle | Planned proof |
| --- | --- | --- | --- |
| R1 external template files and manifest loading | Original request "not hard coded in code" | `01-template-pack-file-loading-foundation` | Manifest diff plus loader test. |
| R2 preserve existing examples | Existing `default-workflows.yaml` state | `01-template-pack-file-loading-foundation` | Existing Gmail/Office365 summary keys still present. |
| R3 Gmail task extraction into project structure | Original request "identify and create tasks from email" | `02-email-plugin-workflow-examples` | Template key assertion, graph construction, executor/settings assertions. |
| R4 Office365 task extraction into project structure | Original request "default plugins like gmail and office365" | `02-email-plugin-workflow-examples` | Template key assertion, graph construction, executor/settings assertions. |
| R5 email summary examples | Original request "process emails to get summary" | `02-email-plugin-workflow-examples` | Existing summary keys still present and graph construction succeeds. |
| R6 Mermaid graph from input file | Original request "creating of some mermaid graphs based on some input file" | `03-file-analysis-workflow-examples` | Template key assertion and source-ingestion/project-structure graph validation. |
| R7 source-code file summary | Original request "create summary of some source code file" | `03-file-analysis-workflow-examples` | Template key assertion and source-ingestion/project-structure graph validation. |
| R8 validation coverage | Bundle outcome contract | `03-file-analysis-workflow-examples` | Targeted `dotnet test` command and bundle validator output. |
