# Proof Strategy

Every migrated area needs:

1. Failing-first or adversarial source scan where practical.
2. Focused unit/helper tests.
3. Focused integration tests through `ProcessRunAutomationDispatchServiceTests`.
4. Source scans proving no forbidden dependencies.
5. Line-count tracking for `ToolValidation.cs`.
6. Bundle validator at prepared and completed stages.
7. No prohibited viewport proof paths.

Expected test slices:

- `ProcessAgentExecutionBoundaryArchitectureTests`
- `ProcessRunAutomationDispatchServiceTests` focused on required tools, missing tools, critical failures, carried proof, process mock, completion status, retry/recovery summaries.
- Existing provider/tool policy tests as entry/final smoke.
