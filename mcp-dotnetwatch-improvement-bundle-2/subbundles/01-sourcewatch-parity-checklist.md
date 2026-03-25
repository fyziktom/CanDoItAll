# Sub-Bundle 1: SourceWatch Parity Checklist

- [x] remove `--artifacts-path` from the `SourceWatch` launch path
- [x] stop forcing MSBuild server off for `SourceWatch`
- [x] keep non-watch lanes unchanged in this phase
- [x] add or update unit tests for `SourceWatch` launch arguments
- [x] benchmark `ProjectsPage.razor` simple edit
- [x] benchmark `PageHeader.razor` simple edit
- [x] record before/after timing deltas

Evidence:

- `artifacts/managed-mcp-pageheader-fullflow.json`
- `artifacts/managed-mcp-projects-page-fullflow.json`
- `artifacts/final-watch-benchmark-summary.json`

Done:

- managed watch visible-change timing is now `8.145s` for `PageHeader.razor` and `11.703s` for `ProjectsPage.razor`
