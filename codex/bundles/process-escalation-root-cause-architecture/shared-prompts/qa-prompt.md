# QA Prompt

Review the assigned subbundle against the bundle contracts.

Check:

- The implementation stayed within the subbundle scope.
- Required characterization tests were added before behavior changes where requested.
- Generic runtime/application/adapter/MAF workspace layers did not gain domain-specific .NET, Blazor, Calculator, Tetris, screenshot, or Playwright rules.
- Capability readiness is typed and testable, not prompt-only.
- Manager recovery records a typed failure category and recovery decision before retrying.
- Template changes are parseable, fixture-agnostic, and do not force browser proof on non-UI steps.
- Browser validation evidence exists only for subbundles/scenarios that require UI/browser proof.
- `reviews/01-execution-report.md` includes commands, outcomes, proof paths, and progression decisions.

Required closure checks:

- Run the tests named by the subbundle.
- Run the architecture/domain leak checks when the subbundle touches generic layers.
- Inspect process API/readback proof for any process-runtime behavior.
- Reopen the responsible earlier subbundle if E2E proof fails without typed diagnostics.
