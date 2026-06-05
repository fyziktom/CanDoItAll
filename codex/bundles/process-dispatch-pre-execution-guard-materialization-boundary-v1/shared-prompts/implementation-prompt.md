# Implementation Prompt

You are implementing `Process Dispatch Pre-Execution Guard & Upstream Materialization Boundary v1` on branch `maf-processes-refactor`.

Follow the subbundles in order. Do not skip gates. Keep all production changes module-local under:

`src/CanDoItAll.Modules.Processes/Automation/Dispatch/`

Do not create Process Core, driver APIs, driver registries, packages, or UI changes.

For every subbundle:

1. Read its README.
2. Verify prerequisites.
3. Implement only the scoped change.
4. Run required tests/scans.
5. Record proof under the bundle proof folder.
6. Update `reviews/01-execution-report.md`.
7. Do not continue if a critical gate fails.
