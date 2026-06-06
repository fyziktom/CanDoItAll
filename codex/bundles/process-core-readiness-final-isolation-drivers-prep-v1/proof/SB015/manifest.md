# SB015 Proof Manifest

## Status

- Status: Completed
- Gate: subprocess runtime and projection proof
- Semantic invariant contract: bundle://proof/SB015/semantic-invariants.md

## Changed File Hashes

- 1C2F1CBA0D0A74AB8F6C45BB2F98A6D73572CEE4289FB091C7491CB72720C0DD repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHydrationService.cs
- BDA83627D8D1C647DFDE51B4E04CD13151B3F7C854577B567D4F6FB8A27DBB5E repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCompetingExecutionGuardService.cs
- 8372AB7D3F7026A391C98912ED91E9D6DF50BB5788423ABEC8D5E56A81383F7F repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentRuntimeService.cs
- D10063E14DEC5E2CF63222FB68D3DFC2D81D7FD88FCFB8AF69FC24D100D1EA12 repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFailureClosureService.cs
- 8D5F0B44E032FBD6B16287F89E94DA4B051F518CBF9476CF4E32D720C05E31A3 repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerApplicationService.cs
- D78E6B2B77F26EF11CCC09D4077125950B29B8B0A8EAF9874747A36B00754DB3 repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchPreExecutionGuardHandler.cs
- EC495E8FCDF77F546137161608DE851429B39655A15248FF8EC76C45F94BCFD1 repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRecoveryRuntimeService.cs
- CE5C277D32540B93E361D85E6C74F3453EA2230870130B9950981C9F0955F2D8 repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModelAdapters.cs
- 125D1CA1CAE60928596BB2B6B02149F607307087E8D12348507324E5BFF81897 repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModels.cs
- E496F34226AE1E8989342C01DB3CE088785B140F3FD5DFBF9F451B97FEECB3E4 repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs
- 2A978038A05D3FEC554EC1E0A1D103E790F1890A3B71324C1A27774798861F9D repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchStepTransitionService.cs
- 338CD5E9ADF7AC6926FAD5292BF8EFC924FD0A72345CA9CB7F8C7E0D253F1D04 repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchSubprocessRuntimeService.cs
- 91E4C54629634685D8E6B6993E407650E9A0B419D80F698B64F4B838B98088B5 repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessMissingUpstreamArtifactMaterialization.cs
- 8E39C16A18F8CA43D0D1856E8CB87E4EFF9FA272AB0CEDEE555DF1A0C9635FC3 repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExceptionClosure.cs
- CA3388F64212C0E44017CBE5221E4227ABAC315000E98E5CAEEF742CBC46687D repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs
- FF7875B482322F67617A6DA8579B6CC9506BDBEE43A274ED070D20798D3F2C3C repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- 4C532960C0DCCDF2046E56CBB41032BC1E507956C091D0401B25F16E2E3C043D repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs

## Source Proof

- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCandidateHydrationService.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchCompetingExecutionGuardService.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchDirectAgentRuntimeService.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFailureClosureService.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchFinalizerApplicationService.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchPreExecutionGuardHandler.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRecoveryRuntimeService.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModelAdapters.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteModels.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchRouteServices.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchStepTransitionService.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessDispatchSubprocessRuntimeService.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessMissingUpstreamArtifactMaterialization.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ExceptionClosure.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.RouteHandlers.cs
- repo://tests/CanDoItAll.Tests.Integration/ProcessRunAutomationDispatchServiceTests.cs
- repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs

## Command Proof

- Build proof: bundle://proof/SB027/transcripts/build-slnx.txt
- Focused unit test proof: bundle://proof/SB027/transcripts/unit-architecture-tests.txt
- Focused integration test proof: bundle://proof/SB027/transcripts/integration-dispatch-tests.txt
- Semantic positive proof: bundle://proof/SB027/transcripts/source-scan.txt
- Anti-stub audit: no stubs found by bundle://proof/SB027/transcripts/source-scan.txt
- Failing-first: N/A process proof gate; no production behavior was added solely for this proof manifest.

## Result

SB015 is closed. The route-service layer is adapter-free, hydration and route models carry the materialization facts needed by pre-execution services, subprocess/finalizer/failure closure paths use module-local collaborators, and final proof kept Process Core and process-driver APIs out of production source.
