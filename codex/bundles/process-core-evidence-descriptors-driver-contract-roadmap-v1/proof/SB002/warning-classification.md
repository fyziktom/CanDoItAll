# SB002 Warning Classification

## Baseline
- Failing-first proof: bundle://proof/shared/transcripts/baseline-build.txt.
- Baseline result: solution build passed with 3 warnings and 0 errors.

## Fixes
- `CS8629`: repo://src/CanDoItAll.AgentFramework.Persistence/Validation/SandboxWorkspaceDocumentInvariantValidator.cs now pattern-captures the nullable execution run id before membership checks.
- `CS0618`: repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowEventNormalizer.cs uses `ExecutorId` directly and reads legacy `SourceId` reflectively only as compatibility fallback.
- `CS9113`: repo://src/CanDoItAll.Modules.AgentFramework/Providers/WorkspaceBackedAgentProviderProfileRegistry.cs removes the unused constructor dependency.

## Passing Proof
- Clean build: bundle://proof/SB002/transcripts/post-warning-cleanup-build.txt.
- Focused unit and architecture tests: bundle://proof/SB002/transcripts/focused-unit-tests.txt.
