# Current Source Hotspots

| Area | File | Risk |
| --- | --- | --- |
| Domain process adapters | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDomainEvidenceReadOnlyAdapters.cs | Too many adapters/mappers/payloads/records/enums in one file. |
| Payload builders | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationPayloadBuilder.cs | All-lane payload construction can become a large utility. |
| Batch orchestration | repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessReadOnlyVerificationBatchOrchestrator.cs | Repeated lane-specific mapping; future lanes may increase complexity. |
| Gateway | repo://src/CanDoItAll.Processes.Drivers.VerificationGateway/ProcessDriverVerificationGateway.cs | Must remain explicit typed methods, not runtime host. |
| Process project references | repo://src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj | Multiple driver package references require allow-list governance. |
