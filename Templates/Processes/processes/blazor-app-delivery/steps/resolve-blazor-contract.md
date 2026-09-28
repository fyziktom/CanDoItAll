# Resolve Blazor delivery contract

Read project structure and run prompt. Resolve Blazor mode (SSR, WASM, or WASM PWA), product root, requested external output root, managed process artifact root, process run id, acceptance criteria, routes/screens, backend/API expectations, exclusions, and required evidence writeback targets. Keep product files and evidence separate: write contract/handoff proof under the managed process artifact root shown in the run context (`artifacts/process-runs/<run-id>` or a child path), not under `output/` unless `output/` is the approved product root and this step is authorized to mutate product files. Keep the app description generic and do not add sample-specific assumptions.

Resolve the validation environment as part of this contract, before handing work to implementation or QA. For static WebAssembly/PWA delivery, distinguish proof of the published files from deployment to an external service. Unless the source explicitly names an external deployment target or requires a production deployment, choose a disposable loopback static host for the published output, record its startup/cleanup plan, and state the cache/update expectations that follow from the required PWA behavior. A local proof server does not add an application backend. Do not invent a cloud provider, production credentials, or an operator decision as a prerequisite for local delivery. Preserve an explicitly requested external deployment as a separate requirement; local proof cannot stand in for that requirement.

## Evidence

Record commands, files, URLs, screenshots, console messages, errors, assumptions, and project-structure writeback references as applicable.

