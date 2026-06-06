# SB48 Semantic Invariants

- Invariant ID: SB48-PROJECTION-BOUNDARY-001
- Source raw note: Projection boundary decoupling must proceed without Process Core, production driver APIs, or UI proof drift.
- Expected behavior: Projection coordinators and facets consume module-local projection models while behavior, source-family order, identity keys, lineage, storage paths, trust, and validation semantics remain preserved.
- Disallowed shallow implementation: Moving ProcessRunAutomationDispatchService.* nested aliases into projection coordinators or adding Core, driver, Razor, CSS, JavaScript, TypeScript, or viewport proof artifacts.
- Failing-first test: N/A - process/non-production boundary guard; source scans and architecture tests reject shallow drift.
- Passing test: dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore --filter "FullyQualifiedName~ProcessAgentExecutionBoundaryArchitectureTests&FullyQualifiedName~Artifact_projection" and dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter FullyQualifiedName~Projection.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProjectionModels.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionFacetImplementations.cs, and repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs.
- Production assertions: Build and projection tests preserve the existing runtime behavior surface without adding new product-facing routes or artifacts.
- Red-team negative case: Forbidden-token scans reject nested dispatcher models, Process Core, production driver APIs, UI file drift, and placeholder markers in touched production dispatch files.
- Downstream dependency check: dotnet build CanDoItAll.slnx --no-restore and focused integration projection tests passed.
