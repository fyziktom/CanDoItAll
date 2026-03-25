# Sub-Bundle 4: Validation Checklist

- [x] run MCP dotnet watch unit tests
- [x] run targeted integration tests that cover app start, wait, build, and test resume
- [x] rerun the managed live simple-edit probe
- [x] preserve plain-watch and build baselines from earlier bundle artifacts
- [x] summarize final before/after metrics in bundle docs
- [x] note any remaining non-blocking risks

Evidence:

- `artifacts/final-watch-benchmark-summary.json`
- `08-implementation-results.md`
- `tests/CanDoItAll.Mcp.DotNetWatch.Tests`
- `tests/CanDoItAll.Mcp.DotNetWatch.IntegrationTests`
- `.vscode/mcp.json`

Validation result:

- the bundle now contains current evidence that managed watch plus browser validation is back under the `15s` target
- the repo MCP config now uses the wrapper launcher and that contract is covered by integration tests
