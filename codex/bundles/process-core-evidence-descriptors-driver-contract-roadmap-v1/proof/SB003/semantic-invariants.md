# SB003 Semantic Invariants

## Invariant SB003-WARN-001
- Invariant ID: SB003-WARN-001
- Source raw note: Current build warnings must be fixed or explicitly classified before clean build gates.
- Expected behavior: The solution build completes with 0 warnings after targeted fixes, without broad suppressions.
- Disallowed shallow implementation: Hide warnings with blanket `NoWarn`, remove behavior, or leave obsolete/nullability/unread-parameter warnings in the baseline.
- Failing-first test: bundle://proof/SB003/transcripts/failing-first-warning-gate.txt.
- Passing test: bundle://proof/SB002/transcripts/post-warning-cleanup-build.txt.
- Changed source files: repo://src/CanDoItAll.AgentFramework.Persistence/Validation/SandboxWorkspaceDocumentInvariantValidator.cs; repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowEventNormalizer.cs; repo://src/CanDoItAll.Modules.AgentFramework/Providers/WorkspaceBackedAgentProviderProfileRegistry.cs.
- Production assertions: Warning fixes keep existing validation, MAF event identity fallback, and provider profile behavior intact.
- Red-team negative case: A blanket suppression or behavior removal would fail source assertion review and focused tests.
- Downstream dependency check: SB004-SB006 may rely on a clean warning baseline for Core descriptor work.

## Invariant SB003-BOUNDARY-001
- Invariant ID: SB003-BOUNDARY-001
- Source raw note: Keep Core pure and keep driver work out of production source.
- Expected behavior: Core contains no forbidden runtime, storage, AgentFramework, logger, service-provider, or process-driver dependencies; changed production files add no stubs.
- Disallowed shallow implementation: Pass the build while leaking side-effect dependencies into Core or adding placeholder production code.
- Failing-first test: bundle://proof/SB003/transcripts/failing-first-warning-gate.txt.
- Passing test: bundle://proof/SB002/transcripts/focused-unit-tests.txt.
- Changed source files: repo://src/CanDoItAll.AgentFramework.Persistence/Validation/SandboxWorkspaceDocumentInvariantValidator.cs; repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowEventNormalizer.cs; repo://src/CanDoItAll.Modules.AgentFramework/Providers/WorkspaceBackedAgentProviderProfileRegistry.cs.
- Production assertions: Core forbidden-token scan, production process-driver token scan, no UI/media drift scan, and anti-stub audit all passed.
- Red-team negative case: Production `IProcessDriver*`/registry/selector tokens or Core forbidden dependencies would be reported by Gate A scans.
- Downstream dependency check: Later Core descriptor and driver-proposal phases can proceed only while these scans remain clean.
