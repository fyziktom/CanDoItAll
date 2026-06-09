# Real Test Results And Gaps

## Real tests that passed
- Full unit rerun after transient path cleanup: 1,134 passed.
- Startup smoke: web app starts current composition, `/health` is OK, process templates are visible, and `ProcessesService`, `ProcessTemplateCatalogService`, and `IProcessRunAutomationDispatchService` resolve.
- Large desktop `/processes` UI proof: template selection, launch plan creation, execution into a process run, run selected.
- Project-scoped process route proof.
- Project-structure node process start API proof.
- Project-structure output folder quick action opens selected project-scoped run.

## Real tests not yet done
- no completed dispatch/finalizer E2E;
- no completed MAF workflow/direct-agent E2E;
- no completed `.NET` app scenario;
- no completed business-analysis scenario;
- no live OpenAI smoke;
- no scheduler/workflow-origin execution beyond launch path;
- no run detail artifact/recovery browser proof.

## OpenAI live test interpretation
The user now has OpenAI API credits, but the live test must remain opt-in and bounded. A live smoke should verify the minimal current provider path, not replace deterministic tests. It should record model/provider/request metadata and output hashes without logging secrets, raw API keys, or excessive prompt/output text.

## Required repair
The current execution report must not be marked completed until SB013-SB048 are finished or explicitly re-scoped with a new completion report. This bundle continues the incomplete runtime proof.
