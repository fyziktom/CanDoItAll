# Runtime Invariants

1. Processes own lifecycle, dependencies, artifacts, recovery, and closure.
2. Workflows are executors under Processes, not replacements for Processes.
3. A completed step must have required artifacts whose validation status is acceptable for the step contract.
4. A recorded-but-invalid artifact must never render as fully satisfied.
5. A final product delivery step must respect grounded external target constraints when the project structure provides them.
6. Manager chat must resolve from configured manager or selected-run assignment before any fallback.
7. Fallback manager resolution must be explainable and must not silently choose an ambiguous manager.
8. Project structure projection should expose navigable run/product folders, not every artifact subdirectory.
9. Agent tool access must match operation contracts and required skills.
10. Documentation must describe the runtime that is actually implemented.
