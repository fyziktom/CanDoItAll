# SB16 Proof Manifest

## Implementation Proof

- `semantic-invariants.md`
- `story-coverage.md`
- `browser-validation.md`
- `codeanalytics-snapshot-summary.txt`
- `subbundle-closure-gate-sb16.md`
- `source-assertions.txt`

## Browser Artifacts

- `browser/processes-definition-role-editor.png`
- `browser/processes-global-definition-catalog.png`
- `browser/processes-project-shell.png`
- `browser/browser-proof.json`

## Raw Scans

- `scans/ui-forbidden-runtime-persistence-template-scan.txt`
- `scans/anti-stub-scan.txt`
- `scans/performance-scan-counts.txt`

## Build And Test

- `build-process-module.txt`
- `build-solution-sb16.txt`
- `test-unit-role-editor-sb16.txt`
- `test-components-process-shell-sb16.txt`
- `test-playwright-process-shell-sb16.txt`
- `bundle-validator-prepared-sb16.txt`

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative / Boundary Proof |
| --- | --- | --- | --- | --- |
| `ProcessDefinitionRoleEditorProjection` | `ProcessDefinitionRoleEditorProjectionService` | `ProcessWorkspaceShellProjectionService`, role editor UI | Built per selected definition from template role metadata or scoped authoring-session snapshot. | `test-unit-role-editor-sb16.txt`; `scans/ui-forbidden-runtime-persistence-template-scan.txt` |
| `ProcessDefinitionRoleEditorCommand` | Role editor UI / projection client | `ProcessDefinitionRoleEditorProjectionService` | Carries scope, definition key, typed command kind, version token, draft role payload, and optional template action key. | `test-components-process-shell-sb16.txt`; stale version-token unit test |
| `ProcessDefinitionRoleDraftProjection` | Application projection service and role editor UI | Role commands, launch-planning handoff | Holds identity, purpose, executor kind, workflow preference, project assignment, fallback, approval, allocation, template source, snapshot, and override metadata. | Unit invalid executor/allocation rejection test |
| Role template actions | `ProcessTemplatePackLoader` | Role editor projection service and UI | Loaded from template pack `toolbox/role-templates.json` through source-generated JSON metadata. | Unit template-action apply test; UI no-template-file scan |
| Step-role bindings | `ProcessTemplatePackLoader` | Role editor projection and future SB18 step editor | Parsed from definition step role assignments and projected with typed responsibility/fallback/rebind data. | Unit/component step binding assertions |
| `/processes` role authoring flow | Process route and shell component | Browser users | Search selects architecture definition, edits role fields, saves role, applies template, and preserves project route smoke coverage. | `test-playwright-process-shell-sb16.txt`; `browser/browser-proof.json` |

## Story Coverage

See `story-coverage.md`.

## File Integrity

- `changed-file-hashes.txt`
- `line-counts.txt`
