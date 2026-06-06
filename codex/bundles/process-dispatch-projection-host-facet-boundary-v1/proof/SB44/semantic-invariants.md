# SB44 Semantic Invariants

- Invariant ID: SB44-INV-001
- Source raw note: Continue safe dispatcher isolation without Process Core or production process-driver APIs, preserve behavior, and narrow the projection host into module-local facets.
- Expected behavior: Projection coordinators consume only small internal facet interfaces; the dispatcher-backed services implementation is nested and module-local; source-family order remains execution, process mock, workspace-written, existing managed, response text, provider-native browser, completed decision; candidate mutation remains centralized.
- Disallowed shallow implementation: Keeping IProcessArtifactProjectionHost, keeping DispatcherArtifactProjectionHost, injecting ProcessRunAutomationDispatchService into source coordinators, duplicating candidate mutation, introducing Process Core, introducing production driver APIs, or changing UI files.
- Failing-first test: bundle://proof/shared/transcripts/adversarial-negative-broad-host.txt records the rejected broad-host shallow case with non-zero exit proof.
- Passing test: bundle://proof/shared/transcripts/unit-projection-tests.txt, bundle://proof/shared/transcripts/integration-projection-tests.txt, and bundle://proof/shared/transcripts/build.txt passed.
- Changed source files: repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionFacets.cs, repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionOrchestrator.cs, and projection source coordinators listed in bundle://proof/SB44/manifest.md.
- Production assertions: bundle://proof/shared/source-assertions/projection-facet-boundary.md plus source scans under bundle://proof/shared/transcripts/ prove no core, driver, UI, broad-host, or stub drift.
- Red-team negative case: bundle://proof/shared/transcripts/source-scan-no-broad-host.txt and bundle://proof/shared/transcripts/source-scan-no-core-driver.txt reject the main shallow shortcuts.
- Downstream dependency check: All later SBxx phases depend on this same facet boundary and are covered by the final build, focused projection tests, and SB72 closure proof.

## Referenced Changed Files

- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionFacets.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjectionServices.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactProjection.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionOrchestrator.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExecutionArtifactProjectionCoordinator.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessMockArtifactProjectionCoordinator.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessWorkspaceWrittenArtifactProjectionCoordinator.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessExistingManagedArtifactProjectionCoordinator.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessResponseTextArtifactProjectionCoordinator.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessProviderNativeBrowserArtifactProjectionCoordinator.cs
- repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessCompletedDecisionArtifactCoordinator.cs
- repo://tests/CanDoItAll.Tests.Unit/ProcessAgentExecutionBoundaryArchitectureTests.cs

