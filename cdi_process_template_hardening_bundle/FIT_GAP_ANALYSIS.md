# Fit-gap analysis

| Category | Gap | Severity | Evidence | BundleResponse |
| --- | --- | --- | --- | --- |
| Template materialization | 477 prior apply-manifest targets are missing from the current repository. | Critical | analysis/bundle-application-audit.md | Materialize the full pack and add audit scripts/tests. |
| Truthfulness of prior completion claim | The earlier in-repo bundle documentation claims completion and validation even though the file-driven pack is absent. | Critical | analysis/bundle-application-audit.md; analysis/architecture-weak-spots.md | Make the new bundle explicitly honest and add a final QA truthfulness gate. |
| SQLite safety | Import and delete paths need explicit SQLite-first review. | High | analysis/sqlite-hardening-review.md | Dedicated hardening subbundle and process-focused tests. |
| Maintainability | Core process-module files are oversized and mix unrelated responsibilities. | High | analysis/long-file-refactor-plan.md | Three decomposition subbundles plus review gates. |
| Loader explicitness | Template loading still has hidden static construction paths. | High | analysis/architecture-weak-spots.md | DI and path hardening subbundle. |
| Regression coverage | The prior run did not prevent pack-loss from slipping through. | High | tests/CanDoItAll.Mcp.Processes.Tests/*.cs; tests/CanDoItAll.Tests.Integration/*.cs | New materialization, sidecar-parity, and import-metadata tests. |