# Requirement Traceability

## Requirement Matrix

| Requirement | Inputs | Bundle files | Subbundle | Closure status |
| --- | --- | --- | --- | --- |
| REQ-001 | NOTE-001 | `requirements/01-normalized-requirements.md`, `architecture/01-target-solution.md` | SB01 | Solved |
| REQ-002 | NOTE-002, NOTE-005 | `requirements/01-normalized-requirements.md`, `analysis/01-current-state.md` | SB01 | Solved |
| REQ-003 | NOTE-004, NOTE-006 | `requirements/01-normalized-requirements.md`, `architecture/01-target-solution.md` | SB01 | Solved |
| REQ-004 | NOTE-003 | `requirements/01-normalized-requirements.md`, `subbundles/01-mcp-reinstall-build-pipeline-and-proof/README.md` | SB01 | Solved |
| REQ-005 | NOTE-007 | `requirements/01-normalized-requirements.md`, `reviews/01-execution-report.md` | SB01 | Solved |

## Raw Note Closure

| Raw note | Exact wording | Normalized requirements | Surface | Planned proof | Owner | Sequencing | Exception |
| --- | --- | --- | --- | --- | --- | --- | --- |
| NOTE-001 | Moving agent templates into `Templates` was correct. | REQ-001 | Repository template layout | Source diff confirms no template move/delete. | SB01 | Must hold throughout. | None |
| NOTE-002 | MCP server installation should not need those templates. | REQ-002 | MCP installer/shadow artifacts | Reinstall transcript and artifact scan. | SB01 | Must pass before closure. | None |
| NOTE-003 | MCP reinstall must build MCPs, setup them, and setup also skills as it does. | REQ-004 | Reinstall script | Full reinstall transcript and manifest assertion. | SB01 | Must pass before closure. | None |
| NOTE-004 | Shorten the hash may help but is not full solution. | REQ-003 | DotNetWatch shadow wrapper | Source assertion proves standard build plus copy, not only shorter names. | SB01 | Must pass before closure. | None |
| NOTE-005 | MCP projects do not have strong dependency that would load Templates. | REQ-002 | Shared MSBuild target | Source assertion identifies opt-out target. | SB01 | Must pass before implementation. | None |
| NOTE-006 | Build standard Release in repo and copy final MCP build outputs into artifacts. | REQ-003 | DotNetWatch shadow wrapper | Wrapper transcript and manifest path. | SB01 | Must pass before closure. | None |
| NOTE-007 | Validate that it is working. | REQ-005 | Host script | Passing full reinstall transcript. | SB01 | Final closure input. | None |
