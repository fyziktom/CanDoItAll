# Post-Phase Validation Skill Pack

## Required Skills By Review Area

| Review area | Required skills |
| --- | --- |
| Bundle and subbundle gate checks | `candoitall-bundle-validator`, `candoitall-subbundle-validator` |
| Repo and symbol analysis | `candoitall-codeanalytics-mcp` |
| Shared component compliance | `candoitall-components-mcp` |
| Browser and visual proof | `playwright`, `candoitall-watch-playwright-loop`, `frontend-skill` when layout critique is needed |
| ASP.NET and Blazor implementation review | `aspnet-core` |
| Test execution proof | `run-tests` |

## Minimum Expectations

- Always use `candoitall-codeanalytics-mcp` before making source-of-truth or dependency claims.
- Always use `candoitall-components-mcp` before accepting new page structure that could have used shared components.
- Always use Playwright MCP for UI-owning repair subbundles.
- Treat missing browser proof, missing seed coverage, or duplicated registry drift as reopen conditions.
