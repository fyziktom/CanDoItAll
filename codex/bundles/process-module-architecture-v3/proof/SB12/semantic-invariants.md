# SB12 Semantic Invariants

- `definition.json` remains canonical for template migration analysis.
- Markdown, Mermaid, and current-module projection files are treated as generated sidecars, not canonical source.
- The migration scanner performs dry-run analysis and does not mutate files.
- Invalid manifest entries fail predictably instead of being skipped.
- Branch outcomes without typed route targets are diagnostics, not auto-routed guesses.
- Legacy runtime history is read-only unless full migration is explicitly selected.
- Old runtime services are not referenced by active process source.
- Runtime action attempts against legacy history return a typed denial.
