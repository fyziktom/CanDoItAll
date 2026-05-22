# Normalized Requirements

| Requirement | Source | Owner | Acceptance |
| --- | --- | --- | --- |
| `R001` Managed process output implementation proof | Live blocked step 2 | `SB01` | Source/project reads under `output/.../process-runs/<run-id>/...` satisfy current-attempt implementation proof when they are concrete product paths and not `bin`, `obj`, `.git`, or `.playwright-mcp`. |
| `R002` Strict browser evidence refs | Invalid `Browser console log` artifacts | `SB01` | Dotnet stdout/stderr paths under process-run artifacts are not accepted as browser console evidence unless produced by browser tooling or located under the scoped browser evidence path. |
| `R003` Same-step artifact retry remains valid | Existing recovery behavior | `SB01` | Missing current-step required output artifacts may still produce same-step rework because the current step owns those outputs. |
| `R004` Downstream missing upstream input reroutes to source | User request | `SB02` | A downstream step with missing configured upstream artifact inputs does not execute repeatedly; it blocks and requests materialization from the producing agent-owned step. |
| `R005` Downstream retry after source materialization | User request | `SB02` | When the producing upstream step completes, blocked dependents waiting on upstream artifact materialization become ready or waiting approval again. |
| `R006` Generic process core | User constraint | `SB01`, `SB02` | No Tetris, Blazor, canvas, or app-specific process runtime condition is added. |

## Non-Requirements

- This bundle does not reset the development DB.
- This bundle does not repair the generated Tetris application itself.
- This bundle does not require browser proof for non-UI process steps.
