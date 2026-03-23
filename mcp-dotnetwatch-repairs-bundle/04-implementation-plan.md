# Implementation Plan

## Stream 1: Codex startup contract
- add a PowerShell wrapper to start `CanDoItAll.Mcp.DotNetWatch`
- make the wrapper rebuild shadow artifacts when missing or stale
- build into versioned shadow folders so loaded DLLs cannot block refresh
- point `config.toml` to the wrapper instead of the raw shadow DLL
- update the config instructions so transport failure is a repair obligation

## Stream 2: bootstrap diagnostics
- add a bootstrap diagnostics path derived from `.mcp-state/logs`
- record stdio startup failures to that file
- improve backend startup timeout exceptions with registration and assembly context
- keep bootstrap logging compatible with the PowerShell runtime Codex actually uses

## Stream 3: runtime behavior hardening
- remove the backend proxy `HttpClient` timeout ceiling so long waits honor MCP cancellation
- reuse `dotnet watch` artifacts per compatible app template instead of per session id
- raise the default app wait timeout to fit the real CanDoItAll web app startup profile

## Stream 4: validation coverage
- add an integration test that starts the MCP server through the wrapper
- keep existing direct-start integration tests
- validate that wrapper startup still serves `workspace_info`
- keep a lifecycle test that proves stdout stays protocol-clean while app start and wait still work

## Stream 5: operator evidence
- create a new repair bundle with findings, failure scenarios, architecture, prompts, and QA review
- capture validation evidence from shell and focused tests

## Checklist

### A. Bundle
- [x] write findings
- [x] write failure matrix
- [x] write architecture changes
- [x] define implementation plan
- [x] define checklists and validation criteria
- [x] perform QA review of the plan
- [x] update the bundle after real implementation findings

### B. Config
- [x] update `config.toml` to launch the wrapper
- [x] strengthen instructions so MCP transport failure triggers repair work

### C. Wrapper
- [x] add repo-local wrapper script
- [x] detect stale or missing shadow output
- [x] switch to versioned shadow build roots
- [x] fix PowerShell-compatible source hashing
- [x] emit bootstrap log lines to `.mcp-state/logs`
- [x] launch the shadow DLL with the configured settings path

### D. Server
- [x] add persistent bootstrap diagnostics file support
- [x] log stdio startup exceptions
- [x] log backend startup exceptions
- [x] improve timeout details in backend connection startup
- [x] remove hidden backend proxy timeout limits
- [x] reuse app artifacts across compatible sessions

### E. Tests and validation
- [x] add wrapper-based integration coverage
- [x] run focused MCP unit tests
- [x] run focused MCP integration tests
- [x] manually prove wrapper launch and backend reachability
- [ ] revalidate the live Codex MCP tool path after host reload
