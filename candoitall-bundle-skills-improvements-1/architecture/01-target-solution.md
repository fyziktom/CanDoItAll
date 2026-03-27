# Target Solution

## Shape

- Strengthen the validator script so bundle preparation catches broken source references and missing feedback execution-report scaffolding before a bundle is marked ready.
- Tighten the workflow and execution skill instructions so final bundle-state synchronization is mandatory rather than implied.
- Make `mtp-hot-reload` guidance explicit in all three bundle skills as an acceleration tactic that never replaces a clean confirmation run.

## Boundaries

- Do not add repository-specific assumptions to the validator; it must work across future bundles.
- Do not require screenshot artifacts during preparation; those still belong to execution.
- Do not invent a separate execution validator tool in this pass unless the existing validator cannot be safely extended.
