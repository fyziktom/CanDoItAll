# Process Artifact Recovery, Proof Path, And Blazor Delivery Hardening

## Status

- Bundle status: `Completed`
- Execution status: `SB01-SB07 completed; live PostgreSQL Blazor delivery accepted`
- Live run analyzed: `cf03d392-e86a-440e-a174-8b7daa7d96d3`; final validation run: `f0c184d4-e823-409e-b159-0fca1f911b00`
- Local runtime analyzed: `http://localhost:5032/_dev/runtime`
- Final validation: `Completed on PostgreSQL runtime with Cognitive Memory disabled`

## Mission

Repair the generic process automation failure exposed by the live multi-team software-delivery run where the implementation step became blocked even after it recorded required artifacts, then prove that CanDoItAll agents can deliver a working Blazor app from project-structure requirements without Codex building the app manually. The repair must keep process core generic while making the existing proof behaviors reliable and moving Blazor-specific delivery rules into process templates, agent instructions, tools, and project-structure records.

- current-run product files under managed process output roots count as implementation proof when the agent actually reads them
- non-browser stdout/stderr files cannot be projected as browser console evidence only because their path contains `process-runs`
- downstream steps missing configured upstream artifact inputs reroute recovery to the producing step, then retry after the upstream producer completes
- Blazor app delivery, repair, and feature-addition process definitions require build, test, runtime/browser proof, screenshots, console checks, app cleanup, and project-structure evidence writeback
- HR-selected agents use PostgreSQL-backed runtime data, `gpt-5.4-mini`, and only agents with the required workspace, project-structure, process, dotnet, and browser tools are accepted for live runs
- demo runs write outputs and validation evidence under `C:\programovani\dotnet-demo\output\<run-folder>` and back up current project-structure data before reruns

## Outcome Contract

- The live DB failure is mapped to concrete process records, not inferred from the UI alone.
- Tetris and Blazor remain evidence examples only; no Tetris-specific rule is added to process runtime.
- Missing current-step output artifacts can still produce a targeted same-step rework packet.
- Missing upstream artifact inputs no longer cause repeated downstream execution attempts; the process asks the source step to materialize the missing artifact.
- Browser evidence classification requires a browser tool or a scoped browser evidence path, not arbitrary `.txt` output.
- Generic Blazor app delivery templates exist for new app delivery, repair/fix, backend feature addition, frontend feature addition, and backend+frontend feature addition.
- Blazor templates make Playwright/browser proof, screenshots, console inspection, build/test output, and project-structure result writeback part of process/agent contracts rather than runtime code.
- A PostgreSQL live demo run is launched from API-backed project structure, observed as a user, and checked independently only after agents finish their work.
- If the final app is not satisfactory, the bundle records whether the failure is in skills, agent/tool permissions, staffing, process design, or runtime automation.
- Tests and live evidence cover the expanded behaviors.

## Recommended Execution Order

1. `subbundles/01-proof-path-and-browser-classification`
2. `subbundles/02-upstream-artifact-materialization`
3. `subbundles/03-blazor-process-template-pack`
4. `subbundles/04-agent-model-and-tool-readiness`
5. `subbundles/05-api-backed-demo-backup-and-rerun`
6. `subbundles/06-live-process-observation-and-summaries`
7. `subbundles/07-final-app-validation-and-project-structure-proof`

## Validation Summary

- Bundle preparation status: `Prepared after validator repair`
- Execution status: `Completed`
- Subbundle gate review: `SB01-SB07 passed`
- Final closure gate: `Completed-stage validator pending in final closure step`
- Browser validation analytics: `SB07 accepted; agent evidence includes Playwright/browser screenshot, console log with 0 errors and 0 warnings, build output with 0 errors, and test output with 3 passed tests`
- Live DB inspection: `Completed`
- Targeted process/runtime integration tests: `441 passed`
- Agent policy/metadata unit tests: `92 passed`
- Agent seed integration tests: `23 passed`
- PostgreSQL runtime state: `http://localhost:5032`, Cognitive Memory `false`, selected delivery agents seeded with `gpt-5.4-mini`
- Live output root: `C:\programovani\dotnet-demo\output\codex-live-blazor-20260522-192839`
- Remaining validation gap: `None known before completed-stage bundle validator`
