# Source Hotspots

| Area | Current file(s) | Concern | Bundle action |
| --- | --- | --- | --- |
| Gateway | `src/CanDoItAll.Processes.Drivers.VerificationGateway/ProcessDriverVerificationGateway.cs` | Explicit single-lane methods exist, no batch orchestration yet. | Add typed batch without generic dispatch. |
| Process adapters | `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDomainEvidenceReadOnlyAdapters.cs` | Broad file with adapters, mappers, payloads, observations. | Split into narrow files and add orchestrator. |
| Process project refs | `src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj` | Direct refs to all driver packages. | Evaluate gateway-only reference strategy or explicit allow-list. |
| Domain packages | `src/CanDoItAll.Processes.Drivers.*` | Read-only packages must remain runtime-free. | Source scans and focused tests. |
| Tests | `tests/CanDoItAll.Tests.Unit`, `tests/CanDoItAll.Tests.Integration` | Need full-unit and focused matrix proof. | Gate every phase with targeted proof. |
