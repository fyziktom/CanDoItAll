# Structured Input

## Bundle Identity

- Bundle folder: `C:\repositories\CanDoItAll\CanDoItAll_CrmHr_CodexBundle_Final`
- Legacy architect bundle format is preserved under `00_INPUTS` through `08_QA`.
- Workflow overlay for validation and execution lives under `inputs`, `analysis`, `plan`, `subbundles`, `reviews`, and `scripts`.

## Requested Outcome

- Add a merged CRM/HR module named `CanDoItAll.Modules.CrmHr`.
- Keep one shared identity layer across CRM, HR, projects, workbench, and AI-agent usage.
- Improve the stale bundle first, then validate and execute it against the latest repo state.

## Current Repo Drift That Must Be Honored

- Workbench participant, meeting, and work-item metadata already exist and must be upgraded instead of replaced.
- Project structure dependency readiness is now governed by `ProjectStructureDependencyAnalyzer` and checklist rules.
- File and asset handling now depends on `IStoragePlacementService`, storage drivers, and `StorageObjectReference`, not legacy single-path assumptions.
- There is no `src/CanDoItAll.Modules.CrmHr` project yet.

## Non-Negotiable Constraints

- Use BaseLib-first UI only for CRM/HR pages.
- Do not break project-local participants or the existing Workbench structure flows.
- Reuse Workspace AI provider profiles instead of creating a disconnected AI runtime registry.
- Require browser proof for UI-visible subbundles and deeper proof for critical foundations.
