# SB006 Semantic Invariants

## Invariant SB006-GATEWAY-CONSTRUCTION-PATH
- Invariant ID: `SB006-GATEWAY-CONSTRUCTION-PATH`
- Source raw note: `Stable Process Core with domain drivers`
- Expected behavior: Process read-only adapters do not construct concrete alpha verifiers or the observation aggregator directly; default construction enters through the explicit verification gateway.
- Disallowed shallow implementation: Adding a gateway project reference while leaving `new TranscriptVerificationAlphaVerifier`, `new RuntimeEvidenceConsistencyAlphaVerifier`, `new ArtifactEvidenceAlphaVerifier`, `new OfficeEvidenceAlphaVerifier`, `new BusinessAnalysisAlphaVerifier`, or `new ProcessDriverObservationAggregator` in process adapter defaults.
- Failing-first test: `N/A - no prior failing transcript was captured before the edit; bundle://proof/SB006/transcripts/p02-source-scans.txt is the adversarial denial proof for the shallow direct-construction implementation.`
- Passing test: bundle://proof/SB005/transcripts/focused-p02-unit-tests.txt, bundle://proof/SB005/transcripts/focused-p02-integration-tests.txt, and bundle://proof/SB006/transcripts/full-unit-p02.txt
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessTranscriptVerificationReadOnlyAdapter.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRuntimeEvidenceVerificationReadOnlyAdapter.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDomainEvidenceReadOnlyAdapters.cs, repo://src/CanDoItAll.Modules.Processes/CanDoItAll.Modules.Processes.csproj
- Production assertions: Process adapter defaults use `ProcessDriverVerificationGateway.CreateDefault()` method groups; package references are explicitly documented as typed request-contract references; Core has no driver-package reference in the captured scan.
- Red-team negative case: bundle://proof/SB006/transcripts/p02-source-scans.txt denies direct alpha verifier and aggregator construction in the process adapter target files.
- Downstream dependency check: P03 can split adapter files because construction is already routed through the gateway and no downstream phase needs to trust hidden direct verifier construction.
