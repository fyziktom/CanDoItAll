# QA Prompt

Validate the workflow template examples bundle.

Check:

- The manifest references every new workflow file.
- `WorkflowTemplatePackLoader` loads every new key without duplicate-key errors.
- Existing Gmail and Office365 summary keys still load.
- Gmail and Office365 task templates include download, LLM classification, task creation, fallback summary storage, and mark-processed nodes.
- Task creation nodes preserve input payloads so processed-message IDs are still available.
- Mermaid and source-code templates use `source.ingest` before LLM transformation and `project-structure` asset creation after.
- Targeted tests and bundle validators pass, or failures are recorded with a concrete follow-up.
