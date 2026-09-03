# CanDoItAll Agent Instructions

Read [the engineering instructions](.github/copilot-instructions.md) for code style,
architecture, ownership, and integration rules.

Use `$apply-candoitall-shared-standards` for CI/test repairs and repository conventions.
Canonical shared guidance and reusable skills live in `CanDoItAll.SharedInfo`; keep
repository-specific scanners, baselines, and test commands here.

## Validation Closure

Before repairing tests or changing source, build/configuration, or validation tooling,
read [Testing](docs/testing.md) and the current [CI workflow](.github/workflows/ci.yml).

Run the `portability-static` procedure in [Testing](docs/testing.md#portability-static-gate)
when repairing tests or changing protected files, including supporting production edits
and changes brought in by a merge. Running only affected tests or leaving the full suite
to CI does not waive this static gate.

Review all added/stale findings, repair genuine portability defects, and regenerate the
scan after source edits. Refresh intentional, reviewed baseline deltas in the same
change, inspect the diff, and require final enforcement without `--write-baseline`.
Report that result alongside the focused tests; do not claim completion while it fails.
