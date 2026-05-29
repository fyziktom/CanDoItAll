# Normalized Requirements

R1. Workflow definition validation must reject unknown, planned, disabled, unavailable, or schema-invalid executors at save/import/publish/test time.

R2. Workflow artifact records must reference retrievable content, or the system must clearly mark metadata-only artifacts and never imply full content exists.

R3. Workspace file/folder operations must cover common user workflows: create directory, ensure directory, delete, copy, move/rename, tree, glob, existence check, file hash, and bounded binary/file-reference support.

R4. Source ingestion must support folder scenarios clearly and safely, including workspace folder selection, recursive scanning, include/exclude filters, file counts, truncation summaries, and document-type expansion.

R5. Implement deterministic JSON transformation/data-shaping executor without arbitrary code execution.

R6. Implement Markdown/report rendering executor with file output and artifact integration.

R7. Implement delay/wait and explicit approval helper capabilities with clear runtime semantics.

R8. Non-executor helper node kinds must be either implemented, converted to executor-backed nodes, or blocked from active publish/run.

R9. HTTP workflows must support safe download-to-workspace and content artifact behavior while maintaining network/secret guardrails.

R10. Add workflow templates and UI catalog entries demonstrating local folder/file workflows.

R11. Add scenario harness coverage for folder ingestion, JSON transform, Markdown output, artifact retrieval, approval flow, and invalid executor validation.

R12. Do not implement DurableTask/AzureFunctions production runtime in this bundle unless required by a failing invariant. Keep backend honesty.
