# Code vs Bundle Churn Analysis

## Compared Range
- Base: `b5149b5a647ea78f367174303b9ba161de53e413`
- Head: `09d155bc696d15e3bd8d25824f1c321951f4a55a`

## Observed Diff Pattern
The latest code-first attempt still added a full new bundle directory with many subbundle README files.

Approximate line totals from the compare output:

| Area | Approx changed lines | Notes |
| --- | ---: | --- |
| `codex/bundles/process-driver-runtime-host-codefirst-integration-gate-v1` | ~1319 | New bundle/proof/planning files, including 12 subbundle README files. |
| `src` + `tests` | ~701 | Real production/test implementation. |
| `docs` | ~709 | Large documentation file, useful but not implementation. |
| `src` + `tests` + `docs` | ~1410 | Barely above bundle churn, but this hides the fact that source/test code alone is much smaller than bundle churn. |

## Why The Previous Ratio Gate Was Insufficient
The previous gate appears to have counted docs heavily and/or ignored the amount of new bundle boilerplate. For implementation progress, `src + tests` must dominate. Docs are useful, but they should not compensate for weak production/test changes.

## New Gate
Final closure must compute and record:

```text
source_test_lines = changed lines under src/ + tests/
bundle_lines = changed lines under codex/bundles/
docs_lines = changed lines under docs/

source_test_lines >= 3 * bundle_lines
```

A secondary target is:

```text
bundle_lines <= 15% of total changed lines
```

If these are not met, the bundle is not completed. Codex should either keep implementing code/tests or stop as blocked.

## Bundle Churn Budget
During implementation, Codex may update:

- `reviews/01-execution-report.md`
- one concise `proof/SBxx/manifest.md` per critical phase
- one final code-vs-bundle numstat transcript

Codex must not generate new 70+ line README boilerplate files per subbundle during execution.
