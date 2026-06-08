# Source Hotspots

| Area | Files | Risk |
| --- | --- | --- |
| Gateway | `src/CanDoItAll.Processes.Drivers.VerificationGateway/ProcessDriverVerificationGateway.cs` | Currently explicit transcript/runtime only; next work must expand without generic runtime host. |
| Process adapters | `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessTranscriptVerificationReadOnlyAdapter.cs`, `ProcessRuntimeEvidenceVerificationReadOnlyAdapter.cs` | Good pattern but must not become hidden runtime registry. |
| Domain drivers | `ArtifactEvidence`, `OfficeEvidence`, `BusinessAnalysis` packages | Need explicit gateway/adapters and shared policy conformance. |
| Observation aggregation | `ProcessDriverObservationAggregator.cs` | Must remain read-only aggregation, no persistence/eventing. |
| Tests | `ProcessAgentExecutionBoundaryArchitectureTests.cs` | Historical bundle fixture skips must be replaced or retired. |
