# Filesystem Agent Tools

This bundle adds a maintainable, agent-visible filesystem tool surface for common workspace file and folder operations while preserving existing workspace path and external-target safety checks.

## Profile

- `initiative`

## Mission

- Move filesystem runtime behavior out of the broad `WorkspaceRuntimePlugin` responsibility cluster into a cohesive filesystem plugin boundary.
- Expose the existing unregistered file-service capabilities (`HashPath`, `ZipPath`, `UnzipArchive`) as agent tools.
- Add clearer folder listing semantics for top-level versus recursive directory reads.
- Keep all file and folder operations routed through `IWorkspaceFileService` and `WorkspacePathPolicy`; do not bypass the existing allowed-area enforcement.
- Update capability templates and tests so agents can discover and use the new filesystem commands consistently.

## Outcome Contract

- Requested outcome: agents can list folders non-recursively or recursively, copy folders, create folders, hash paths, zip folders/files, and unzip archives through organized workspace filesystem tools.
- Hard constraints: no new `MafAgentRuntime` partials; no direct raw filesystem access from agent tool methods except through existing workspace file services; write/mutation tools stay approval-protected; external-target alias safety remains enforced.
- Evidence required before closure: CodeAnalytics snapshot evidence, direct unit tests for the extracted filesystem plugin/tool behavior, capability template assertions, focused runtime composition tests, affected builds, and updated bundle proof notes.
- Known blockers or explicit scope exceptions: this phase does not replace git, dotnet, script, document, spreadsheet, image, browser, project-structure, or memory tools; it isolates and improves only the workspace filesystem tool family.

## Recommended Execution Order

1. `subbundles/01-01-filesystem-service-capabilities`
2. `subbundles/02-02-runtime-tool-provider-wiring`
3. `subbundles/03-03-tests-and-runtime-proof`

## Dependency And Validation Map

- Keep the mermaid dependency map, critical-subbundle notes, and phase gates current in `plan/01-phase-plan.md`.
- If the bundle is resumed after compaction or by a different agent, use this README, the current subbundle README, and `reviews/01-execution-report.md` as the durable state.

## Validation Summary

- Bundle preparation status: `Prepared`
- Execution status: `Implemented`
- Subbundle gate review: `Passed`
- Final closure gate: `Passed with unrelated full-suite failures documented`
- Browser validation analytics: `N/A - runtime/tooling backend change`
