# SB15 Proof Manifest

## Implementation Proof

- `semantic-invariants.md`
- `story-coverage.md`
- `browser-validation.md`
- `codeanalytics-snapshot-summary.txt`
- `subbundle-closure-gate-sb15.md`
- `source-assertions.txt`

## Browser Artifacts

- `browser/processes-definition-editor-desktop-mcp.png`
- `browser/processes-definition-editor-narrow-mcp.png`
- `browser/processes-definition-editor-narrow-lint-actions-mcp.png`
- `browser/processes-definition-editor-published-playwright.png`
- `browser/processes-global-definition-catalog-playwright.png`
- `browser/processes-project-shell-playwright.png`
- `browser/desktop-editor-assertions.json`
- `browser/narrow-editor-assertions.json`
- `browser/desktop-console-warnings.md`
- `browser/desktop-network.md`
- `browser/desktop-editor-snapshot.md`
- `browser/narrow-editor-snapshot.md`

## Raw Scans

- `scans/ui-forbidden-runtime-persistence-scan.txt`
- `scans/ui-no-template-or-file-dependency-scan.txt`
- `scans/anti-stub-scan.txt`
- `scans/performance-scan-counts.txt`

## Build And Test

- `build-process-module.txt`
- `build-solution-sb15.txt`
- `test-unit-definition-editor-sb15.txt`
- `test-components-process-shell-sb15.txt`
- `test-playwright-process-shell-sb15.txt`
- `bundle-validator-prepared-sb15.txt`

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative / Boundary Proof |
| --- | --- | --- | --- | --- |
| `ProcessDefinitionEditorProjection` | `ProcessDefinitionEditorProjectionService` | `ProcessWorkspaceShellProjectionService`, `ProcessWorkspaceShell.razor` | Built per selected definition from template metadata or the scoped authoring session snapshot. | `test-unit-definition-editor-sb15.txt`; `scans/ui-forbidden-runtime-persistence-scan.txt` |
| `ProcessDefinitionEditorCommand` | UI projection client | `ProcessDefinitionEditorProjectionService` | Carries scope, definition key, version token, command kind, and draft payload for save/publish/archive/delete. | `test-components-process-shell-sb15.txt`; `browser/desktop-editor-assertions.json` |
| `ProcessDefinitionEditorLintProjection` | Application editor projection service | Definition editor UI | Created from draft validation; publish runs strict lint and rejects blocking issues with actionable receipt text. | `test-unit-definition-editor-sb15.txt`; `test-components-process-shell-sb15.txt` |
| Authoring defaults | `ProcessTemplatePackLoader` | Catalog/editor projection services | Reads JSON template identity, governance, contract, simulation, role, step, and artifact counts through source-generated JSON metadata. | `test-unit-definition-editor-sb15.txt`; `scans/ui-no-template-or-file-dependency-scan.txt` |
| Definition editor UI | `ProcessWorkspaceShell.razor` | Browser users | Renders identity, governance including manager override, contracts, simulation, lint, receipts, and typed command buttons. | `test-components-process-shell-sb15.txt`; `browser-validation.md` |
| `/processes` edit/publish flow | Process route and shell component | Browser users | Search selects architecture definition, edits fields, saves draft, publishes, and preserves project route smoke coverage. | `test-playwright-process-shell-sb15.txt`; `browser/processes-definition-editor-published-playwright.png` |

## Story Coverage

| Story | Result | Proof |
| --- | --- | --- |
| US-005 create/save/publish/archive/delete/lint a definition | Covered | Save/publish/archive/delete transition and stale version-token tests in `test-unit-definition-editor-sb15.txt`; component command-boundary and lint tests in `test-components-process-shell-sb15.txt`; browser save/publish proof in `browser-validation.md`. |
| US-006 edit definition identity fields | Covered | Identity projection test in `test-unit-definition-editor-sb15.txt`; browser proof edits name and owner in `browser/desktop-editor-assertions.json`. |
| US-007 configure governance fields | Covered | Governance projection includes criticality, autonomy, operating mode, working status, manager override, notes, change summary, and policy summary; browser proof edits manager override. |
| US-008 review contracts and simulation readiness | Covered | Contract/simulation projection tests and browser screenshots show contract summaries, step/artifact counts, simulation readiness, and clear lint. |

## File Integrity

- `changed-file-hashes.txt`
- `line-counts.txt`
