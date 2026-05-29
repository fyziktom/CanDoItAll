# SB01 MAF Version Decision

Decision date: 2026-05-28.

Decision: keep the current MAF package line during this hardening bundle:

- Stable packages stay at `1.6.2`.
- A2A preview packages stay at `1.6.2-preview.260521.1`.
- `Microsoft.Agents.AI.Mem0` stays at `1.0.0-preview.251028.1`.

Rationale:

- `dotnet restore CanDoItAll.slnx` and `dotnet build CanDoItAll.slnx --no-restore` passed on the current package graph.
- The current MAF workflow APIs already support the existing `MafWorkflowCompiler`, `WorkflowBuilder`, `BindAsExecutor`, `InProcessExecution`, workflow event mapping, and runtime backend.
- NuGet flat-container metadata shows newer `1.8.0` stable packages and `1.8.0-preview.260528.1` A2A preview packages, superseding the prepared bundle's `1.7.0` comparison point.
- Upgrading the package line would widen this bundle from runtime hardening into a package/API migration. That should be a separate follow-up after SB02-SB07 strengthen the local validation and execution proof.

Source proof:

- `bundle://proof/SB01/transcripts/package-scan.txt`
- `bundle://proof/SB01/transcripts/nuget-version-scan.txt`
- `bundle://proof/SB01/transcripts/restore-build.txt`
