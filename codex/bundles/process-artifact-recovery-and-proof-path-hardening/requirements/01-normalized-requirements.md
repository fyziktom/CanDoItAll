# Normalized Requirements

| Requirement | Source | Owner | Acceptance |
| --- | --- | --- | --- |
| `R001` Managed process output implementation proof | Live blocked step 2 | `SB01` | Source/project reads under `output/.../process-runs/<run-id>/...` satisfy current-attempt implementation proof when they are concrete product paths and not `bin`, `obj`, `.git`, or `.playwright-mcp`. |
| `R002` Strict browser evidence refs | Invalid `Browser console log` artifacts | `SB01` | Dotnet stdout/stderr paths under process-run artifacts are not accepted as browser console evidence unless produced by browser tooling or located under the scoped browser evidence path. |
| `R003` Same-step artifact retry remains valid | Existing recovery behavior | `SB01` | Missing current-step required output artifacts may still produce same-step rework because the current step owns those outputs. |
| `R004` Downstream missing upstream input reroutes to source | User request | `SB02` | A downstream step with missing configured upstream artifact inputs does not execute repeatedly; it blocks and requests materialization from the producing agent-owned step. |
| `R005` Downstream retry after source materialization | User request | `SB02` | When the producing upstream step completes, blocked dependents waiting on upstream artifact materialization become ready or waiting approval again. |
| `R006` Generic process core | User constraint | `SB01`, `SB02` | No Tetris, Blazor, canvas, or app-specific process runtime condition is added. |
| `R007` Generic Blazor process templates | Follow-up request | `SB03` | Template pack includes Blazor app delivery, repair/fix, backend feature, frontend feature, and backend+frontend feature processes that are generic across Blazor SSR, WASM, and WASM PWA. |
| `R008` Browser proof and screenshots as process contracts | Follow-up request | `SB03` | Blazor QA/release steps require build/test/runtime proof, screenshots, console inspection, URL/entrypoint, cleanup receipts, and project-structure evidence writeback through step artifact expectations. |
| `R009` Agent model and tool readiness | Follow-up request | `SB04` | HR-assigned agents for live runs use `gpt-5.4-mini` and have required process, project-structure, workspace command, dotnet, browser, image-inspection, and artifact tools before execution is accepted. |
| `R010` API-only project structure mutation | Follow-up request | `SB05` | Demo project-structure backup, clean basic-info reset, process-definition links, process starts, and result notes use HTTP APIs rather than DB writes or test fixtures. |
| `R011` PostgreSQL-only runtime | Follow-up request | `SB05`, `SB06` | Live validation confirms the app host uses the development PostgreSQL profile and cognitive memory is disabled. |
| `R012` Run summaries and UX observations | Follow-up request | `SB06` | Process definitions or manager directives require compact run summaries, evidence indices, and UX/process observations so large runs can be reviewed without reading all raw records. |
| `R013` Agent-owned app delivery validation | Follow-up request | `SB07` | Agents deliver and self-test the app; Codex independently validates only after completion and records screenshots/console proof without editing generated product files. |
| `R014` Failure classification | Follow-up request | `SB07` | If the final app is not working, the bundle records whether the root cause is skills, permissions, staffing, process design, or runtime automation. |

## Non-Requirements

- This bundle does not reset the development DB.
- This bundle does not repair the generated Tetris application itself.
- This bundle does not require browser proof for non-UI process steps.
- This bundle does not add Tetris-specific runtime code or template logic.
