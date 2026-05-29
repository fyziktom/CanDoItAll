# Executor Inventory And Gap Analysis

## Current executor inventory

| Area | Current executor/node | Status | Notes |
| --- | --- | --- | --- |
| Workspace files | `storage.file` | Implemented | Supports list/stat/read/write/append/search/diff. Missing create directory, copy, move, delete, tree, binary, zip. |
| Source ingestion | `source.ingest` | Implemented | Can ingest project/workspace file/folder sources; reads text, PDF text, xls/xlsx summaries. Needs DOCX/HTML/ZIP/CSV structure improvements and better local folder UX. |
| HTTP | `http.fetch` | Implemented | Supports HTTP methods and bounded response. Needs `IHttpClientFactory`, allowlist/SSRF controls, download-to-workspace, binary/file references, retry/backoff policy integration. |
| Spreadsheet | `spreadsheet` | Implemented | Good start for workbook read/write/range operations. Needs CSV interop, table metadata, formula safety, and artifact content proof. |
| Project structure | `project-structure` | Implemented | Reads and creates assets/tasks. Needs batch update, move/copy node, link/source attachment operations. |
| Image | `image.generate` | Implemented | Needs provider failure artifactization and generated file existence proof. |
| JSON transform | `json.transform` | Planned | Should be implemented in next bundle. |
| Markdown render | `markdown.render` | Planned | Should be implemented in next bundle. |
| Delay | `utility.delay` | Planned | Needed for waits/retries/schedules. |
| Human approval | `human.approval` | Planned | HITL exists as node kind; explicit reusable approval executor still planned. |
| Command process | `command.process` | Planned | Needed, but high risk. Must be sandboxed and approval-gated. |
| Agent step | `AgentStep` node kind | Pass-through/undefined | Needs explicit policy: implement, convert to executor, or block as active node. |
| Subworkflow | `Subworkflow` node kind | Pass-through/undefined | Needed for reuse/composition; requires runtime bridge and recursion limits. |
| Artifact | `Artifact` node kind | Pass-through/undefined | Needed for writing/referencing workflow outputs; should be explicit. |
| Strict logic / triage | Node kinds | Mostly route/pass-through | Should be formalized as deterministic data-shaping nodes or kept visual-only with validation. |

## Expected user needs not yet covered

### Local workspace/folder workflows

Users will likely want workflows such as:

1. Select a local folder.
2. Recursively list relevant files.
3. Filter by extension/date/size/name.
4. Read bounded content from each file.
5. Summarize with LLM.
6. Write results to a Markdown report.
7. Save extracted tasks/assets into a project structure.
8. Optionally archive/copy/move processed files.

The current building blocks handle only parts of this. Folder mutation and report generation are not yet complete.

### Data shaping workflows

Users will need to:
- pick fields from JSON,
- merge multiple executor outputs,
- split arrays,
- map/filter arrays,
- deduplicate,
- sort,
- compute simple counts/stats,
- convert JSON to Markdown tables,
- create a stable schema for downstream LLMs.

This should be deterministic and not rely on LLM prompts.

### File/document conversion workflows

Users will need:
- DOCX/PPTX/HTML/CSV extraction,
- PDF metadata/page boundaries,
- image references,
- ZIP/folder import/export,
- file hash and duplicate detection,
- MIME/content-type handling.

### Control and composition workflows

Users will need:
- delay/wait,
- explicit approval checkpoint,
- retry/backoff at node level,
- batch/for-each over folder files or JSON arrays,
- fan-in/collect outputs,
- subworkflow call,
- agent step call.
