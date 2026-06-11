# SB08 Semantic Invariants

## Invariant SB08_INV_001
- Invariant ID: `SB08_INV_001`
- Source raw note: Determine whether process execution works again across user-facing launch, representative automation, runtime-host readback, and scheduler/workflow trigger paths.
- Expected behavior: Build, unit tests, focused integration matrix, and the SB02 project-structure Playwright launch/readback proof all pass from real command transcripts.
- Disallowed shallow implementation: Do not close from prior reports, static screenshots, skipped tests, or API-only proof for the user-facing launch path.
- Failing-first test: N/A for this final release-matrix invariant because it validates already implemented behavior from SB02-SB07.
- Passing test: `bundle://proof/SB08/transcripts/build.txt`; `bundle://proof/SB08/transcripts/unit-tests.txt`; `bundle://proof/SB08/transcripts/focused-integration-matrix.txt`; `bundle://proof/SB08/transcripts/playwright-sb02.txt`.
- Changed source files: `repo://tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProjectScopedProcessLaunch.cs`; `repo://tests/CanDoItAll.Tests.Integration/ProcessTemplateExecutionE2ETests.cs`; `repo://tests/CanDoItAll.Tests.Integration/ProcessDomainEvidenceReadOnlyAdapterTests.cs`; `repo://tests/CanDoItAll.Tests.Integration/ProcessesServiceIntegrationTests.cs`.
- Production assertions: The matrix proves launch/readback, dispatch/outbox/finalizer/artifact readback, persisted PostgreSQL business-analysis readback, runtime-host readback on real run and step ids, and scheduler/workflow trigger starts through process-owned paths.
- Red-team negative case: `bundle://proof/SB08/transcripts/anti-stub-audit.txt` verifies changed source/test files do not introduce TODO, NotImplemented, skip, or unreviewed stub markers.
- Downstream dependency check: SB08 release decision uses SB01-SB07 manifests plus the SB08 command matrix rather than reopening implementation paths.

## Invariant SB08_INV_002
- Invariant ID: `SB08_INV_002`
- Source raw note: Keep Process Core generic and do not introduce driver self-registration, reflection discovery, fallback selectors, unsafe mutation APIs, secret leakage, or bundle-path coupling.
- Expected behavior: Final source scans pass and distinguish allowed provider fallback/workbench reflection from forbidden process-driver discovery or execution approval.
- Disallowed shallow implementation: Do not rely on absence of compile errors to prove architectural boundaries.
- Failing-first test: N/A for this final scan-only invariant because it is a red-team audit over the completed source state.
- Passing test: `bundle://proof/SB08/transcripts/source-core-drift-scan.txt`; `bundle://proof/SB08/transcripts/driver-registration-reflection-fallback-scan.txt`; `bundle://proof/SB08/transcripts/mutation-api-readonly-scan.txt`; `bundle://proof/SB08/transcripts/secret-leakage-scan.txt`; `bundle://proof/SB08/transcripts/bundle-path-coupling-scan.txt`; `bundle://proof/SB08/transcripts/large-file-growth-scan.txt`.
- Changed source files: `repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentRuntime.cs`; `repo://src/CanDoItAll.Modules.AgentFramework/Hosting/ProcessMockAgentSupport.cs`; `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.cs`; `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`; `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessArtifactProjectionFacetImplementations.cs`.
- Production assertions: No representative template vocabulary appears in Process Core or Contracts; read-only runtime-host paths contain no mutator APIs; no production source bundle-path coupling or secret-like value is present.
- Red-team negative case: The driver scan reviews existing provider fallback and optional Workbench reflection hits and reports zero forbidden process-driver discovery/fallback candidates.
- Downstream dependency check: Scheduler/workflow and runtime-host paths remain behind process-owned services/facades and the execution-capable driver future gate stays dry-run blocked.

## Invariant SB08_INV_003
- Invariant ID: `SB08_INV_003`
- Source raw note: Do not let bundle/proof files dominate source/test changes.
- Expected behavior: Final code-first ratio must pass before the bundle can be considered merge-ready.
- Disallowed shallow implementation: Do not claim final closure from green tests when the hard source/test-to-bundle ratio gate fails.
- Failing-first test: `bundle://proof/SB08/transcripts/final-code-first-ratio.txt` records the blocking gate result under the conservative `HEAD` baseline.
- Passing test: N/A. This invariant is intentionally blocked because the ratio does not pass.
- Changed source files: `repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeHostCodeFirstGuardTests.cs`; `repo://tests/CanDoItAll.Tests.Unit/LocalWorkspaceProcessHostTests.cs`.
- Production assertions: Source/test changed lines are 1390, tracked bundle changed lines are 465, and the required minimum is 2325.
- Red-team negative case: Artifact-inclusive counting is also recorded and fails, proving this is not only a tracked-diff presentation issue.
- Downstream dependency check: The branch is not merge-ready until the ratio policy is satisfied, rebased onto an explicit bundle-start SHA, or the bundle is split to reduce proof/control-file churn.
