# Requirement Traceability

| Requirement | Bundle destination | Owning subbundle | Proof |
| --- | --- | --- | --- |
| R001 multiple MCP servers | `architecture/01-target-solution.md`, `inventories/01-scope-inventory.md` | 01 | Shared helpers used by multiple `Program.cs` files; focused build. |
| R002 preserve all functions | `requirements/01-normalized-requirements.md`, subbundle checklists | 01, 02, 03, 04 | Tests, build, diff review for no public contract removals. |
| R003 isolate shared helpers | `architecture/01-target-solution.md` | 01 | New Core helper plus unit tests. |
| R004 split long files | `inventories/01-scope-inventory.md` | 02, 03 | Reduced primary file responsibilities and targeted tests. |
| R005 better testability | `shared-prompts/qa-prompt.md` | 01, 02, 03 | Added tests around helpers/splits where practical. |
| R006 best practices | `analysis/02-assumptions-and-risks.md` | 04 | Final review confirms scoped, conservative refactor. |

## Raw Note Coverage

| Raw note | Exact wording | Normalized requirements | Owning subbundle | Planned proof | Exception status |
| --- | --- | --- | --- | --- | --- |
| N001 | "multiple mcp different servers in our solution" | R001, R003 | 01 | Shared helper migration in multiple MCP `Program.cs` files plus build. | None |
| N002 | "detailed refactoring for improvement of the implementation" | R003, R004, R006 | 01, 02, 03 | Code diffs and targeted tests. | First pass targets high-leverage refactors, not every long file. |
| N003 | "You must preserve all functions" | R002 | 01, 02, 03, 04 | Tests/build plus public contract diff review. | None |
| N004 | "proper isolation of shared helpers" | R003 | 01 | Shared Core helper and helper tests. | None |
| N005 | "spliting too long files" | R004 | 02, 03 | Component catalog and DotNetWatch host route split with tests/build. | Very large runtime files inventoried but deferred unless low-risk split emerges. |
| N006 | "better testability" | R005 | 01, 02, 03 | Add/keep targeted xUnit coverage. | None |
| N007 | "use candoitall-bundle-workflow" | R006 | 04 | Bundle validators, subbundle gates, execution report. | None |
