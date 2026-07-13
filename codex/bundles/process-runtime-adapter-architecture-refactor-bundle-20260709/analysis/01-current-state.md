# Current State

See:

- `analysis/00-current-problem.md`
- `architecture/00-csharp-current-state-inventory.md`
- `evidence/00-codeanalytics-snapshot.md`

Summary:

- `AgentFrameworkProcessExecutionAdapter` is a large partial-class cluster with more than 6500 lines across 20 partial files.
- The adapter owns orchestration, MAF invocation, completion gates, receipt matching, managed artifacts, subprocess handling, result conversion, recovery issue creation, and .NET setup.
- `WorkspaceCommandReceiptWriter` contains a direct `.NET` lifecycle special case.
- Existing driver abstractions provide a better seam, but current code still bypasses them for some domain behavior.

