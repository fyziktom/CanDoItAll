# ChatGPT Pro Handoff

## Task Boundary

Use this bundle as raw input for the real repair bundle. Do not treat this package as implementation-ready. It intentionally contains evidence and context only.

## What Happened

The live process run `9bbc0667-9d12-4506-ba81-654ef924cad6` failed on its first step, `Resolve Blazor delivery contract`. The agent execution run for that step completed and reported success, and a `Blazor delivery contract` artifact record exists. The process runtime then failed the step with `ArtifactContractUnsatisfied` because final contract validation classified the candidate artifact as `StaleOrWrongRun`.

## Most Important Files To Open First

1. `inputs/03-api-evidence-index.md`
2. `inputs/api-evidence/11-run-detail-full.json`
3. `inputs/api-evidence/13-step-00-detail.json`
4. `inputs/api-evidence/16-artifact-delivery-contract-detail.json`
5. `inputs/api-evidence/31-agent-execution-run-91e6a078-ac63-43e6-9901-6f8364539c42-detail.json`
6. `inputs/api-evidence/34-agent-execution-run-91e6a078-ac63-43e6-9901-6f8364539c42-tool-receipts.json`
7. `inputs/api-evidence/37-agent-execution-run-d38da822-a980-44ce-952b-6e86c0b74bbb-approvals.json`
8. `inputs/api-evidence/43-project-structure-read-full-project.json`

## Non-Negotiable Observed Facts

- API authorization was disabled.
- The failed run is the only recent run returned by `GET /api/processes/runs?take=50`.
- Run status is `Failed`; no steps completed.
- Step 0 status is `Failed`.
- The raw agent execution for step 0 is `Completed / Succeeded`.
- The process-visible status for the same attempt is `Process failed`.
- One process artifact record exists for `Blazor delivery contract`.
- That artifact record is linked to the current step run and artifact expectation id.
- That artifact record lineage has an empty `contentHash`.
- The failure classification is `StaleOrWrongRun`.
- A later manager-chat run is waiting on approval for a `processes_artifact_record` call that would record an operator decision.
- Project structure contains a current process-run output node for this run, but one selected QA evidence node still mentions old run id `0cca729a-e9bc-47e7-89aa-bef9b88dbf1c`.

## Explicit Non-Goals For This Input Bundle

- No code changes were made.
- No repair strategy was selected.
- No tests were run.
- No bundle validator was run.
- No process rerun or transition was triggered.
- No pending approval was accepted or rejected.

## Suggested Use By ChatGPT Pro

Use the evidence to prepare a proper implementation bundle that starts from the API facts, then performs source diagnosis and proof planning. The actual fix design belongs in that later bundle, not here.
