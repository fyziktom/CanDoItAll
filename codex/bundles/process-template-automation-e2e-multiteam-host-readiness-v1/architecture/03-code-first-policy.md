# Code-First Policy

This bundle is an implementation bundle, not another proof-generation exercise.

Rules:
- Do not add new subbundle folders during execution.
- Keep proof concise in `reviews/01-execution-report.md` and at most one manifest per critical gate.
- Final ratio must be computed from `git diff --numstat <start-sha>...HEAD`.
- Count implementation as `src + tests` only.
- Do not count docs toward the implementation ratio.
- Closure fails if `src + tests` changed lines are less than five times `codex/bundles` changed lines.
- Closure fails if tests prove template execution mainly through manual transition helpers.
