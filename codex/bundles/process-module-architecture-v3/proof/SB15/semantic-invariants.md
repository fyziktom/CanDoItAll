# SB15 Semantic Invariants

## Invariants Preserved

- Definition editor UI reads and writes `ProcessDefinitionEditorProjection` and `ProcessDefinitionEditorCommand`; it does not mutate EF entities, template files, runtime entities, or old dispatcher state directly.
- Save, publish, archive, and delete are explicit typed commands with version-token checks and command receipts. Publish failures return rejected receipts with lint issues; they are not hidden behind fallback behavior.
- Identity, governance, contracts, and simulation readiness are separate projection sections. Governance now includes a manager override summary field without reviving old manager-agent lookup from the UI.
- Template JSON remains the canonical source for default definitions. Markdown and Mermaid are not read by the editor flow.
- Template metadata enters the editor through `ProcessTemplatePackLoader` and application projection services. The UI module does not reference `CanDoItAll.Processes.Templates`, JSON APIs, or file-system APIs.
- Project/runtime persistence for authored definitions is not claimed in SB15. The implementation uses scoped in-memory authoring session snapshots until a durable project-specific definition store is introduced by a later bundle.

## Negative Proof

- `scans/ui-forbidden-runtime-persistence-scan.txt` has 0 matches for runtime, persistence, EF, DbContext, observation, dispatcher, manager, outbox, and claim symbols in the Process UI module.
- `scans/ui-no-template-or-file-dependency-scan.txt` has 0 matches for direct template-loader, JSON, file, or directory dependencies from `src/CanDoItAll.Modules.Processes`.
- `scans/anti-stub-scan.txt` has 0 matches for TODO, HACK, `NotImplementedException`, or stub markers in the owned SB15 source/test surface.
- Publish strict lint rejects missing required fields and keeps the definition unpublished; proof is in `Publish_rejects_blocking_lint_and_returns_actionable_projection`.
- Archive and delete reject stale version tokens after authored state exists; proof is in `Archive_and_delete_reject_stale_version_tokens`.

## Positive Proof

- `test-unit-definition-editor-sb15.txt` passed 12/12 and covers template metadata projection, strict publish lint rejection, save/publish/archive/delete status transitions, stale archive/delete version-token rejection, and existing boundary tests.
- `test-components-process-shell-sb15.txt` passed 12/12 and covers editor section rendering, typed save command dispatch including manager override, publish blocking lint display, search/scope/catalog behavior, refresh, agent context, feed defaults, and shell navigation.
- `test-playwright-process-shell-sb15.txt` passed 1/1 and proves the real host renders `/processes`, searches/selects the architecture definition, edits definition fields including manager override, saves, publishes, screenshots the editor, and still renders the project-scoped process route.
- Browser MCP proof shows desktop publish with manager override, clear lint, no visible Blazor error UI, console warning count 0, network requests 200 OK, and narrow viewport overflow count 0.

## Shallow-Pass Trap Rejected

A shallow implementation could render static form fields and directly mutate component-local state. SB15 avoids that by adding typed projection contracts, a typed command service with version-token validation and receipts, lint projections, template metadata loading through the application boundary, focused unit/component tests, Playwright proof, Browser MCP proof, and UI dependency scans.
