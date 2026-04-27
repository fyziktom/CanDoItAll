# Normalized Requirements

| Requirement | Source wording | Normalized requirement | Owning subbundle | Proof method |
| --- | --- | --- | --- | --- |
| R001 | "multiple mcp different servers" | Apply the refactor to shared MCP server infrastructure used by multiple server projects, not a one-off cleanup in only one server. | 01 | Build affected MCP projects and inspect updated `Program.cs` files. |
| R002 | "preserve all functions" | Preserve existing public MCP tool methods, request/response contracts, startup modes, and route behavior. | 01, 02, 03, 04 | Targeted tests plus focused build; no intentional public contract removal. |
| R003 | "proper isolation of shared helpers" | Move repeated host configuration/logging/options setup into shared helpers under `CanDoItAll.Mcp.Core` without making Core depend on server-specific tool classes. | 01 | Unit tests for helper behavior and build of all migrated hosts. |
| R004 | "spliting too long files" | Split selected long files only where the split follows a clear responsibility boundary and preserves behavior. | 02, 03 | Component catalog tests, DotNetWatch tests, and focused build. |
| R005 | "better testability" | Add or preserve targeted tests around the newly isolated helpers and refactored long-file boundaries. | 01, 02, 03 | New/updated xUnit tests in existing MCP test projects. |
| R006 | "other best pracitce techniques" | Use conservative C# maintainability practices: explicit ownership, small helpers, no global rewrites, no unrelated package or UI changes. | 01, 02, 03, 04 | Code review, git diff inspection, and final closure audit. |

## Scope Exceptions

- Security advisory remediation is out of scope for this bundle because the request is maintainability refactoring and the advisories are solution-wide dependency concerns.
- UI/browser validation is out of scope because the planned changes are server-side C# refactors with no browser-rendered behavior.
- Deep rewrites of every long MCP file are out of scope for this first bundle pass; it targets the highest leverage shared helper extraction and two low-risk split boundaries.
