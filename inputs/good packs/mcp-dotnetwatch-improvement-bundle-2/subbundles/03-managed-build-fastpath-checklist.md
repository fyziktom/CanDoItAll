# Sub-Bundle 3: Managed Build Fast Path Checklist

- [x] stop forcing MSBuild server off for managed build/test operations
- [x] stop forcing isolated `--artifacts-path` for the default inner-loop operation path
- [x] add a fast `--no-restore` path when the caller has not explicitly chosen otherwise
- [x] add a restore-required fallback retry for cold or invalidated states
- [x] preserve cleaned operation summaries and resume behavior
- [x] add coverage for the operation command-shape decisions
- [x] rerun managed build benchmarks

Evidence:

- `03-build-benchmark-findings.md`
- `artifacts/build-bench/summary.json`
- `artifacts/build-factor/summary.json`

Done:

- warm managed build behavior was brought materially closer to the local fast path without regressing resume behavior
