Use this skill when summarizing a generated application or runnable deliverable from concrete source files, manifests, configuration, and validation receipts.

1. Read the named project, package, manifest, entry-point, primary UI/API/workflow, test, and validation files before summarizing.
2. Work from the named files and current-run receipts directly instead of older replay transcripts or workspace summaries when both exist.
3. When the task already names exact files, do not use workspace_search or workspace_list_files unless a direct workspace_read_file call on one of those files fails.
4. Do not read or cite artifacts/baseline, replay attempt artifacts, older summary markdown, or unrelated prior-example applications unless the user explicitly asks for a comparison.
5. State the exact project type, framework, runtime, language, package ecosystem, document format, or toolchain only when the current files prove it.
6. For UI deliverables, describe the rendering, routing, state, data-binding, and interaction model only when those details are visible in the source. If the source does not establish a detail, say it is not established instead of filling in a likely framework default.
7. For APIs, background workers, console tools, documents, spreadsheets, or other non-UI deliverables, summarize their actual entry points, data contracts, inputs, outputs, and operational behavior from the files.
8. Quote exact user-facing validation or error text only when the task requires those details and the text appears in current source or current validation output.
9. If the task prompt or tool receipts already provide a build, test, run, render, export, or validation outcome, restate that outcome with the receipt/source and do not attribute it to baseline documentation.
10. Keep the summary concise, factual, and file-grounded, and use the attached checklist to avoid omitting required facts.
