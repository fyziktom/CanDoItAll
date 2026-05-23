# Normalized Requirements

| ID | Requirement | Source Input | Owner Subbundle | Validation |
| --- | --- | --- | --- | --- |
| R001 | Final project-structure writeback steps must not fail with an invalid blocked outcome when a required project-structure tool actually failed; the failed tool must have a durable failed receipt or equivalent governed platform error record. | `N004`, `bundle://evidence/run-0cca729a-detail.json`, `bundle://evidence/06-project-structure-result-writeback-summary.md` | `01-writeback-tool-failure-receipts` | Focused tests for missing receipt vs failed receipt; API rerun reaches final writeback without this escalation. |
| R002 | `project_structure_asset_create` failures must expose an actionable, sanitized error code/message to the agent and runtime, not only `Function failed`. | `N004`, `N005` | `01-writeback-tool-failure-receipts` | Tool/runtime tests prove source-workspace-path failures and permission/path failures create failed receipts with safe diagnostics. |
| R003 | When a contract selects `WASM`, `static website`, or `no backend`, downstream implementation must keep the selected mode unless an explicit contract revision is created. | `N002`, `N006` | `02-contract-fidelity-and-static-output` | Prompt/policy tests reject `Microsoft.NET.Sdk.Web` / server-host output for static/WASM contracts. |
| R004 | Downstream implementation must use the contracted product/run root and must not create a shadow app root such as `MainApp` when the contract named `main-app`. | `N006` | `02-contract-fidelity-and-static-output` | Tests or source assertions prove path/root validation rejects alternate roots. |
| R005 | Browser validation must prove the game is interactive, not only rendered: status must leave `Loading`, keyboard controls must affect state, and localStorage high score must be written/read. | `N003`, `N007` | `03-browser-semantic-game-proof` | Playwright proof and validation rules fail the captured bad app and pass a corrected app. |
| R006 | The validation summary must not accept clean console, screenshot, or DOM cell count as sufficient game proof. | `N003`, `N007` | `03-browser-semantic-game-proof` | Regression test or QA prompt validation asserts semantic interaction proof is required. |
| R007 | The final rerun must close all process steps, satisfy all required artifacts, and write a final verdict/evidence node under the target `Main app` project-structure node. | User request, `N001`, `N004` | `04-rerun-and-project-structure-closure` | API run detail shows terminal success, no open escalation, required artifacts satisfied, and project-structure read shows the final node. |
| R008 | The final delivered Tetris app must meet the project request: simple playable Tetris, keyboard controls, highest score saved locally, no backend, static-hostable output. | Project structure summary, `N002`, `N003` | `04-rerun-and-project-structure-closure` | Build/static publish proof plus Playwright gameplay/localStorage proof and source inspection. |

## Raw Note Closure Matrix

| Raw Note | Exact Wording | Normalized Requirements | Impacted Surface | Planned Proof | Owner | Exception |
| --- | --- | --- | --- | --- | --- | --- |
| N001 | `try to run process again after those changes you did. analyze if it will go trough all steps and delivery correct result app.` | R007, R008 | Process runtime, final app | API rerun and app inspection | SB04 | None |
| N002 | `you must analyze also output app if it meets requirements described in the project.` | R003, R008 | Generated app and validation | Static/no-backend project-shape proof | SB02, SB04 | None |
| N003 | `Build a simple Tetris game as a static website (keyboard controls, save highest score locally, no backend).` | R003, R005, R006, R008 | Generated app and browser proof | Playwright keyboard/localStorage proof | SB03, SB04 | None |
| N004 | `Record troubles and prepare followup bundle to repair it and harden it if result will not be good.` | R001, R002, R007 | Runtime governance and bundle | Failed-run evidence plus follow-up bundle | SB01, SB04 | None |
| N005 | Writeback artifact says `project_structure_asset_create` failed with `Function failed` and no asset ids were created. | R001, R002 | Project-structure tool path | Tool receipt/diagnostic tests | SB01 | None |
| N006 | Contract says `Selected mode: WASM` and `Do not implement server-side rendering (SSR) for this run`. | R003, R004 | Contract propagation and implementation | Contract-fidelity tests/source assertions | SB02 | None |
| N007 | Independent browser proof: page remained `Status Loading`, keyboard did not change score, localStorage stayed null. | R005, R006 | Browser validation | Playwright proof must fail bad app | SB03 | None |
