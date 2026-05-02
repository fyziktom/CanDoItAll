Use this skill for any process step that creates, repairs, validates, or summarizes a concrete deliverable. A deliverable can be an app, service, API, command-line tool, script, document, spreadsheet, presentation, report, data file, configuration package, generated asset set, or another durable artifact.

Start from the current project-structure node, process step contract, work brief, attached files, and tool outputs. Treat those sources as authoritative for scope, product root, output paths, evidence paths, acceptance checks, and validation tools. Do not reuse sample topics, older generated apps, old output roots, remembered source files, or prior-run acceptance criteria unless the current run explicitly names them. Do not cite files, paths, examples, source artifacts, or tool results as evidence unless they were grounded by this run, inspected by current-run tools, supplied by upstream artifacts, or loaded from attached skill/template resources.

When the process gives a concrete product root or output file, work in that location. If the root is outside the managed workspace, use the mapped alias format required by the active workspace tools. Do not substitute managed evidence folders such as `artifacts/...`, `output/...`, execution-run folders, or the bare workspace root for the product deliverable unless the process explicitly says those folders are the product.

Choose the narrowest real deliverable structure that fits the request. For greenfield work, create a conventional scaffold or file structure for the detected deliverable type instead of loose fragments. For existing work, inspect and repair in place. Do not force-regenerate, recursively delete, or overwrite a non-empty deliverable root just to make a scaffold command succeed.

Use technology-specific skills and tools only after the current files or step contract justify them. For .NET, Blazor, JavaScript, documents, spreadsheets, presentations, browser validation, security review, or architecture review, apply the matching capability as an addition to this generic delivery contract. If no specialized skill exists, use the available workspace tools and the deliverable's native validation path.

Deliver the requested product, not only a scaffold, notes, or test harness. Replace starter content, placeholder copy, stock navigation, fake data claims, example worksheets, default slides, and boilerplate reports with content and behavior grounded in the current request. A serious user-facing app must expose a product-specific primary screen or workflow; a document, workbook, or deck must contain the requested substantive content, not a template shell.

Keep implementation logic explicit and reviewable. Use strongly typed contracts where the target language supports them, clear file boundaries, and minimal necessary abstractions. Do not add silent fallback behavior that hides real failures. Do not create local fake package, framework, runtime, browser, document, spreadsheet, or test-tool shims to make validation pass.

Validation is part of delivery. After the last product mutation, run the narrowest relevant validation that could catch failure for the changed deliverable:
- For code, run the matching restore/install, build, typecheck, lint, unit test, integration test, publish, startup, or package command.
- For runnable apps, services, APIs, scripts, and command-line tools, start or execute the deliverable with representative inputs after the last source/configuration mutation.
- For browser-facing work, navigate to the real route, exercise a representative workflow, inspect console output when relevant, and capture screenshot or DOM evidence showing a meaningful filled, selected, changed, or completed state.
- For documents, render/export/open the produced file and verify required sections, layout-critical content, and missing-placeholder risks.
- For spreadsheets, inspect workbook structure, sheets, formulas, computed values, and any requested charts or exports.
- For presentations, render or export the deck and inspect slides that carry the key message or visual risk.
- For data/configuration deliverables, validate schema, parse/load behavior, representative records, and consumer compatibility when tooling is available.

Failure repair must be evidence-driven. If a validation, build, test, startup, render, export, or inspection tool fails and returns stdout, stderr, receipts, screenshots, logs, trace files, or artifact references, inspect those diagnostics before changing files or rerunning the same command. Do not repair by guessing from exit code alone. After reading diagnostics, make the smallest cause-focused change, then rerun the exact failing validation and any affected broader validation.

Final delivery order is strict: complete the last product mutation, rerun every affected validation, read back or inspect representative changed product files/artifacts, write durable evidence artifacts at the requested evidence paths, then submit the governed outcome. If you mutate another product file or artifact after validation or read-back, repeat the affected validation and read-back before finalizing.

Do not claim completion with chat-only evidence. Required implementation notes, QA notes, summaries, screenshots, exported files, or release artifacts must be written at the exact requested durable paths. If required validation cannot be run with available tools, return a concrete blocker naming the missing tool, command, file, credential, runtime, or safe execution boundary.
