# Assumptions And Risks

## Working Assumptions

- The bundle scope is a follow-up hardening pass after phase7, not a request to run the full Tetris UI scenario end to end.
- The repository already contains PostgreSQL migrations and no SQLite runtime path should be introduced.
- `Templates/Processes/manifest.json` is the authoritative template inventory.
- Existing process API, MAF tool, template, and runtime tests should be extended before adding new test projects.

## Critical Path Risks

- SB01 compile integrity blocks meaningful downstream proof if the solution cannot build.
- SB04, SB06, SB07, and SB10 are critical foundations because later template, UI, and API tests rely on their policy semantics.
- SB13 can affect active Codex skill behavior; if it changes skills, active skill-root synchronization proof is required before dependent validation is trusted.

## Validation Risks

- Source-text assertions alone can pass while runtime behavior remains wrong; behavior-changing subbundles need positive and negative execution proof.
- Template audits can pass on exact fixture names while missing new templates; manifest-driven enumeration is required.
- Browser screenshots without action assertions do not prove the Tetris preflight can be debugged.

## Reopen Triggers

- Reopen SB01 if any later build or enum/default assertion fails.
- Reopen SB04 or SB06 if a later template step can mutate product files outside implementation or repair roles.
- Reopen SB07 if project-structure mutation tools bypass `ExecuteExternalAction`.
- Reopen SB10 or SB11 if API/manual transitions can complete a required-artifact step with weak or unrelated artifacts.
- Reopen SB15 if UI preflight cannot expose enough run, artifact, block, or recovery data to debug the planned Tetris process run.
