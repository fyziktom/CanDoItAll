# Processes Post-Live-Run Hardening + Docs v1

## Status

Prepared for Codex execution.

## Branch context

- Repository: `fyziktom/CanDoItAll`
- Branch: `processes-hardening`
- Reviewed head: `small fix` / `85b91aaa8c1745c98a78d0c5eeb787962eab6949`

## Why this bundle exists

A real Blazor app delivery process has now completed. That is a major milestone, but the recent work also exposed new process-runtime concerns:

1. the process created the app successfully, but output grounding initially missed the requested project-structure output folder;
2. manager chat needed selected-run manager resolution;
3. project-structure projection produced too many per-artifact subfolder nodes;
4. earlier proof debt still exists around broad integration timeouts, artifact hash/race proof, manager recovery proof, and live invalid-artifact proof;
5. documentation and skills are not up to date with the last several rounds of process runtime changes.

This bundle does not focus on one bug. It is a post-live-run hardening pass.

## Execution intent

Codex must:

- audit what actually happened in the successful live run and recent local bundles;
- close proof debt left by previous NO-GO reports;
- refactor high-risk logic into maintainable services;
- strengthen generic process/runtime behavior;
- update documentation, API skill, template docs, and agent skills;
- leave the system ready for broader real process testing across software and non-software processes.

## Non-negotiable boundaries

- Do not hard-code Blazor, Tetris, project ids, run ids, local paths, or user-specific paths into production code.
- Keep Processes above Workflows.
- Keep process runtime generic for software delivery, business analysis, agent improvement/training, incident response, governance, and other process types.
- Do not weaken artifact validation.
- Do not use docs-only changes to satisfy runtime proof requirements.
- Keep PostgreSQL-only runtime assumptions.
