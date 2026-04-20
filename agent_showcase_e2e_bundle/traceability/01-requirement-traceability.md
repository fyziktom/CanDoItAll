# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| `U001` CRM-HR shows 0 agents while Agents page shows existing agents. | `analysis/01-current-state.md`, `requirements/01-normalized-requirements.md` | `subbundles/01-cross-module-agent-source-alignment` | Targeted component or integration tests plus browser proof that `/agents` and `/crm-hr/agents` converge. | CRM-HR must remain a consumer of the technical source of truth. |
| `U002` Cannot scroll in Processes page. | `requirements/01-normalized-requirements.md` | `subbundles/02-processes-workspace-and-database-profile-ux-fixes` | Browser proof on `/processes` plus targeted regression coverage. | Desktop containment regression is suspected. |
| `U003` Add copy button to each database profile path. | `requirements/01-normalized-requirements.md` | `subbundles/02-processes-workspace-and-database-profile-ux-fixes` | Component or browser proof for copy affordances and JS interop call path. | Cover both active selection path and selectable profile path surfaces. |
| `U004` Full showcase using requested database. | `analysis/01-current-state.md`, `architecture/01-target-solution.md`, `plan/01-phase-plan.md` | `subbundles/03-template-driven-showcase-provisioning-and-agent-capability-wiring`, `subbundles/04-live-showcase-execution-bug-harvest-and-closure` | Template-driven provisioning proof, runtime or process evidence, browser screenshots, and execution-report closure. | Final bundle closure depends on this passing. |
