# SB17 Red-Team Semantic Proof

## Risk Tested

The highest-risk false positive is a canvas that renders old-looking nodes while commands are stringly typed, stale edits overwrite newer state, or UI code reaches into runtime/persistence internals.

## Negative Cases

- Stale expected version token: `Canvas_rejects_stale_version_tokens` verifies rejected receipt behavior.
- Missing runtime/persistence boundary: `projection-boundary-scan.txt` has no matches in changed production files.
- Old implementation leakage: `old-symbol-scan.txt` has no matches.
- Stubbed command path: component tests assert actual `ProcessDefinitionCanvasCommandKind`, `ProcessDefinitionCanvasToolboxActionKey`, selected node key, and recomposition mode.

## Positive Cases

- Template canvas defaults produce step, branch router, role, artifact, and toolbox projections.
- Toolbox add command creates a new step and accepted receipt.
- Recomposition returns deterministic layout and recomposed receipt.
- Browser proof exercises the real shell route and captures the resulting canvas.
