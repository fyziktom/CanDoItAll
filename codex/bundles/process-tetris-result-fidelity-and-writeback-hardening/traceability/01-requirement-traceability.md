# Requirement Traceability

| Requirement | Bundle Files | Subbundle | Proof |
| --- | --- | --- | --- |
| R001 | `requirements/01-normalized-requirements.md`, `analysis/01-current-state.md`, `architecture/01-target-solution.md` | `01-writeback-tool-failure-receipts` | Focused tests and `proof/SB01/manifest.md` |
| R002 | `requirements/01-normalized-requirements.md`, `analysis/01-current-state.md` | `01-writeback-tool-failure-receipts` | Tool/gateway tests and safe diagnostic assertions |
| R003 | `requirements/01-normalized-requirements.md`, `architecture/01-target-solution.md` | `02-contract-fidelity-and-static-output` | Prompt/policy tests and source assertions |
| R004 | `requirements/01-normalized-requirements.md`, `plan/01-phase-plan.md` | `02-contract-fidelity-and-static-output` | Root/path validation proof |
| R005 | `requirements/01-normalized-requirements.md`, `analysis/01-current-state.md` | `03-browser-semantic-game-proof` | Playwright keyboard/status/localStorage proof |
| R006 | `requirements/01-normalized-requirements.md`, `shared-prompts/qa-prompt.md` | `03-browser-semantic-game-proof` | Negative proof against captured bad app |
| R007 | `requirements/01-normalized-requirements.md`, `reviews/01-execution-report.md` | `04-rerun-and-project-structure-closure` | API run detail and project-structure read proof |
| R008 | `requirements/01-normalized-requirements.md`, `architecture/01-target-solution.md` | `04-rerun-and-project-structure-closure` | Final app build/static/browser proof |

## Raw Input Coverage

| Raw Note | Bundle Destination | Owning Subbundle | Status |
| --- | --- | --- | --- |
| N001 | `inputs/00-original-request.md`, `requirements/01-normalized-requirements.md` | SB04 | Planned |
| N002 | `inputs/02-structured-input.md`, `analysis/01-current-state.md` | SB02, SB04 | Planned |
| N003 | `inputs/00-original-request.md`, `requirements/01-normalized-requirements.md` | SB03, SB04 | Planned |
| N004 | `inputs/00-original-request.md`, `analysis/01-current-state.md` | SB01, SB04 | Planned |
| N005 | `inputs/01-source-artifacts.md`, `evidence/06-project-structure-result-writeback-summary.md` | SB01 | Planned |
| N006 | `evidence/01-blazor-delivery-contract.md`, `analysis/01-current-state.md` | SB02 | Planned |
| N007 | `evidence/tetris-rerun-independent-snapshot.md`, `analysis/01-current-state.md` | SB03 | Planned |
