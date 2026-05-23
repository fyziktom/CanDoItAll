# Source Artifacts

Saved artifacts under `bundle://evidence/`:

| Artifact | Why It Matters |
| --- | --- |
| `bundle://evidence/run-0cca729a-detail.json` | API snapshot of the failed process run, including step statuses, execution detail, tool receipts, and escalation reason. |
| `bundle://evidence/01-blazor-delivery-contract.md` | Upstream contract selected `WASM (Blazor WebAssembly)`, no backend, and product root `output/process-runs/.../main-app`. |
| `bundle://evidence/03-blazor-runtime-evidence-pack.md` | Workflow's own validation summary accepted the app despite later independent proof showing non-interactivity and mode mismatch. |
| `bundle://evidence/06-project-structure-result-writeback-summary.md` | Final writeback step reported `project_structure_asset_create` failed with only `Function failed`; no asset/node was created. |
| `bundle://evidence/06-run-evidence-index.md` | Index of evidence files the writeback step tried to register before failing. |
| `bundle://evidence/tetris-rerun-independent-snapshot.md` | Independent Playwright snapshot from the delivered app showing the rendered game still at `Status Loading`. |
| `bundle://evidence/tetris-rerun-independent-console.txt` | Independent browser console capture; console was clean, proving console-clean alone is insufficient. |
| `bundle://evidence/tetris-rerun-independent.png` | Independent full-page screenshot of the delivered app. |

Live/API facts captured during preparation:

- Failed run id: `0cca729a-e9bc-47e7-89aa-bef9b88dbf1c`.
- Project id: `7330105d-8450-4c80-923b-5c27d8e63d6c`.
- Target node id: `custom:7404d4fd10624f468c2524ba618d747b` (`Main app`).
- Process completed 3 of 8 steps and failed at step sequence `5`, `Record Blazor results and evidence index`.
- The final escalation reason was: blocked by required tool failure but no failed receipt was recorded for `project_structure_asset_create` / `project_structure_node_create`.
- Generated app root inspected locally: `C:\Users\lucys\AppData\Local\CanDoItAll\workspace\output\scopes\organization\e5df9ad633dbc6974a0678a74976013c\process-runs\0cca729a-e9bc-47e7-89aa-bef9b88dbf1c\MainApp`.
- Independent build after fresh restore passed; independent test run passed 3 tests with MSTest analyzer warnings.
- Independent browser check showed the page rendered but remained `Status: Loading`; keyboard events did not change the score; `localStorage['tetrisgame-high-score']` stayed null.
