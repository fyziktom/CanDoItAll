# Implementation Agent Prompt

You are implementing `process-dispatch-execution-retry-provider-boundary-v1` on branch `maf-processes-refactor`.

Execute subbundles in numeric order. Do not skip critical gates. Do not create Process Core, production driver APIs, driver registries, or driver packages. Preserve behavior. Keep helper/coordinator classes module-local under `CanDoItAll.Modules.Processes`.

For each subbundle:

1. Read the subbundle README.
2. Open the exact source references.
3. Implement only that slice.
4. Run the required proof.
5. Record proof transcript paths.
6. Update `reviews/01-execution-report.md`.
7. Stop if a critical gate fails.
