# Checklists

## Implementation checklist

- config launches the wrapper, not the raw shadow DLL
- wrapper can run from any current working directory inside the repo
- wrapper keeps stdout reserved for the MCP protocol
- wrapper writes human-readable events to stderr and bootstrap log
- wrapper rebuilds shadow artifacts without touching the live stdio stream format
- wrapper exits nonzero on build failure
- server writes persistent diagnostics before rethrowing startup failures
- backend startup timeouts mention actionable file paths and state

## Regression checklist

- direct server DLL startup still works for tests
- wrapper startup works for Codex-style execution
- detached backend registration is still reused when compatible
- incompatible backend registration is still rejected
- no protocol noise is written to stdout
- Playwright-driven app validation still works after managed app start

## Release checklist

- shadow bootstrap log path is documented
- config comment no longer requires a manual shadow refresh as the normal path
- repair bundle reflects the implemented behavior
