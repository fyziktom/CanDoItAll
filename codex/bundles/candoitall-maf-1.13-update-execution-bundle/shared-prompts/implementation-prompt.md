# Implementation Prompt

You are implementing the CanDoItAll MAF 1.13 conservative update from this bundle. Execute one subbundle at a time in order. Do not skip gates.

Before editing, read:

- `bundle://README.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://architecture/00-csharp-current-state-inventory.md`
- `bundle://plan/01-phase-plan.md`
- the current subbundle README
- `bundle://checklists/maf-1.13-phase-checklists.xlsx`

Hard constraints:

- Do not start package changes until `SB01` baseline is recorded.
- Do not introduce `ProcessAgentRuntimeToolProvider` or direct `processes_*` runtime tools.
- Do not expand `/api/processes`.
- Do not adopt new MAF features as product features.
- Do not create central package management.
- Do not use broad warning suppression.
- Do not add final runtime partial classes as an architecture fix.
- Do not hide package incompatibilities with silent fallback behavior.

Implementation posture:

- Prefer the smallest correct package and adapter change.
- Keep MAF SDK-specific code inside adapter projects.
- Add typed helpers only when direct call-site fixes would be unclear or duplicated.
- Add tests for behavior when adding helpers or changing behavior.
- Record exact command output summaries and artifact paths under `reviews/01-execution-report.md`.

Stop and repair the bundle if:

- a new project reference is required;
- Mem0 or A2A compatibility requires disabling a production surface;
- a compile fix touches process runtime or process API behavior;
- focused tests cannot be mapped to current test files;
- any proof requirement cannot be satisfied.
