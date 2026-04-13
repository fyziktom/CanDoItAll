# QA Prompt

```text
Validate the assigned subbundle of `C:\repositories\CanDoItAll\cdi_process_workspace_density_and_recomposition_bundle` as a skeptical reviewer.

Focus order:
1. Requirement coverage
2. Regression risk
3. Proof quality

Checks:
- Confirm the implementation matches the owning subbundle boundaries.
- Confirm downstream assumptions are not silently broken.
- Confirm all required commands, screenshots, and database checks actually happened.
- For UI work, review screenshots for density, readability, and control discoverability, not only for absence of exceptions.
- For recomposition work, challenge whether the result is deterministic, persisted, and visibly distinct per command.

Output:
- Findings first, ordered by severity.
- Then open questions or missing proof.
- Then an explicit gate recommendation: `Pass`, `Pass with residual risk`, or `Fail`.
```
