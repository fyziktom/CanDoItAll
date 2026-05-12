# Assumptions And Risks

## Assumptions

- The Visual Studio PostgreSQL database is reachable through existing app configuration and is allowed to be mutated during validation.
- Workflow runs started from project structure can use the existing workflow runtime manager and run store.
- Workflow input preview can be generated before the run without executing any workflow nodes.
- `gpt-5-mini` and local Ollama `gptoss20b64k` providers are configured or can be configured through existing provider profile mechanisms.
- If adding new `ProjectObjectType` enum members creates broad migration blast radius, a typed workflow subtype under an existing compatible type is acceptable only as a short-lived implementation compromise documented in the execution report.

## Critical Path Risks

- Subbundle 01 is a critical foundation because weak workflow-node identity or metadata contracts will corrupt UI, API, status, and result projection work.
- Subbundle 03 is a critical foundation because run start/status projection is the source of truth for selection status and 20-scenario validation.
- Subbundle 05 is a critical foundation because result-node parentage and execution summaries are core user-visible outcomes, not reporting extras.
- If workflow start is implemented only through generic `/api/workflows/runs/start`, the project-structure canvas will not have enough ownership information to update nodes or place results correctly.
- If input composition is stringly typed, later scenario workflows will become fragile and hard to validate.

## Validation Risks

- Provider runs may be slow or environment-dependent. The validation report must distinguish environment configuration failure from product behavior failure.
- PDF/XLS extraction may require workflow executors that are already present but not tailored to Mouser/SEAMARK; shallow summaries are not sufficient proof.
- UI dialogs and floating windows can pass functional tests but still clip or hide important status; Playwright screenshots must be reviewed.
- PostgreSQL scenario harnesses can accidentally pass with in-memory/SQLite configuration if the DB target is not asserted explicitly.
- Workflow run completion may be fast enough that intermediate "started" state is hard to observe in browser proof; backend tests still need to prove the state transition.

## Reopen Triggers

- Reopen subbundle 01 if any UI or API work needs untyped metadata keys for workflow id/version/input settings.
- Reopen subbundle 02 if scenario input previews omit project details, parent node details, folder paths, or selected file details.
- Reopen subbundle 03 if workflow node progress/markers do not update for completed, failed, cancelled, or waiting states.
- Reopen subbundle 05 if any workflow-created node lands under the original parent instead of under the workflow node.
- Reopen subbundle 06 if fewer than 20 scenarios run, if the cases are trivial duplicates, or if Mouser/SEAMARK data is not used.
- Reopen the relevant implementation subbundle if real provider output is superficial, missing required file paths, or not grounded in the supplied input.
