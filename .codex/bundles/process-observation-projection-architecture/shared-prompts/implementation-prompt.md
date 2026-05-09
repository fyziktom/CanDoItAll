# Implementation Prompt

Use this prompt when executing any subbundle in this bundle.

```text
Implement the selected subbundle only.

Before editing:
- Read the root README, `analysis/01-current-state.md`, `architecture/01-target-solution.md`, the selected subbundle README, and `reviews/01-execution-report.md`.
- Confirm all prerequisites and progression gates from earlier subbundles are satisfied.
- Check the worktree and do not revert unrelated user changes.

Hard constraints:
- Preserve current Processes page behavior unless the selected subbundle explicitly owns the change.
- Keep process runtime logic generic. Specific app/process behavior belongs in process definitions, step instructions, tools, skills, or agents.
- Observation APIs are read-only. Do not add process mutation methods to the observation boundary.
- Cache is a projection only. It must expose staleness/errors and must not become source of truth.
- Use strongly typed contracts, keys, enums, and records. Avoid magic strings and stringly typed UI commands.
- Do not add XML documentation comments.
- Use existing BaseLib/CanvasLib patterns. Do not introduce Radzen.

Implementation approach:
- Make the smallest correct change set for the selected subbundle.
- Preserve existing lazy loading, active-run summary behavior, and analytics visibility rules.
- Add tests at the level required by the selected subbundle.
- For UI changes, use granular state, `@key`, virtualization/windowing where needed, `InvokeAsync` for background updates, and dispose subscriptions.

Proof:
- Run the exact commands listed in the subbundle README unless blocked.
- Record commands, outcomes, changed files, screenshots, performance notes, and residual risks in `reviews/01-execution-report.md`.
- Stop if the progression gate cannot honestly pass.
```
