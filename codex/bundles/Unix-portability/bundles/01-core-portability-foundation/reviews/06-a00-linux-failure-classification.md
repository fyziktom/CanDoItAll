# A00 Linux unit failure classification

The sidecar-backed Linux run executed all 5,297 unit tests: 5,181 passed and 116 failed. The full TRX is `artifacts/unix-portability/A00/linux-baseline/test-results/a00-linux-unit-sidecar.trx`.

| Failed | Test class | Owner | Classification |
|---:|---|---|---|
| 17 | `DotNetSolutionSetupRuntimeExecutorTests` | B01/B02 | Runtime execution plan and Windows-shaped workspace aliases. |
| 11 | `WorkspaceExternalTargetAliasTests` | A01 | Test helper explicitly requires a drive-letter path; core logical external aliases are not Unix-capable. |
| 10 | `WorkspaceManagedScriptPlanExecutorTests` | B02 | Script-plan and working-directory semantics are Windows-shaped. |
| 8 | `ProjectStructureProcessLaunchContextBuilderTests` | B02 | Runtime-node launch metadata is not shell/OS neutral. |
| 7 | `AgentChatExternalTargetAccessAttachmentTests` | A01/B01 | External-target logical aliases and runtime authority attachment must share the core contract. |
| 5 | `WorkflowAdoptionHardeningCheckpointTests` | A07 | Source-inspection tests construct repository paths with Windows separators. |
| 4 | `ProcessRuntimeIntegrationMetadataTests` | B06 | Process runtime metadata assumes current Windows representation. |
| 4 | `WorkspaceProductTargetFilesystemStateLaunchVariableContributorTests` | B01/B02 | Physical target resolution and launch-variable semantics differ on Unix. |
| 4 | `ProjectStructureRuntimeLauncherTests` | B02 | PowerShell/activation/project path assumptions. |
| 4 | `ProjectStructureAgentRootAuthorityWriteGuardTests` | A01/B02 | Root authority must use the core logical/physical boundary. |
| 4 | `WorkspaceRuntimeProcessToolsTests` | B03 | Manager/watch/Tailwind paths and process identity are Windows-shaped. |
| 4 | `DotNetProductBaselineLaunchVariableContributorTests` | B01/B02 | Product path projection and execution plan need portable aliases. |
| 4 | `AgentToolInvocationPolicyTests` | B01 | Runtime tool boundary consumes Windows-shaped path evidence. |
| 4 | `AgentWorkspaceToolAccessMetadataTests` | A01/B01 | Logical alias serialization and grounded runtime metadata disagree on Unix. |
| 3 | `WorkspaceFilesystemRuntimePluginTests` | B01/B05 | Plugin filesystem path normalization uses Windows-shaped evidence. |
| 3 | `MafWorkflowAdapterIsolationTests` | A07 | Source-inspection paths contain backslashes. |
| 3 | `ProjectStructureRuntimeLauncherPathResolverTests` | B02 | Physical/logical runtime launcher resolution is platform-coupled. |
| 3 | `FloatingAgentContextBaselineCharacterizationTests` | B01 | Context baseline snapshots embed Windows path semantics. |
| 2 | `WorkflowExecutorFoundationExtractionTests` | A07 | Source/project inspection uses Windows separators. |
| 2 | `ProcessLaunchPromptTests` | B06 | Prompt/evidence output contains Windows-shaped runtime paths. |
| 2 | `WorkspaceRuntimePluginScriptArgumentTests` | B02 | Script argument construction is platform-coupled. |
| 1 | `ManagerStatusResponseFactoryTests` | B03 | Manager status exposes Windows-shaped paths. |
| 1 | `WorkflowFoundationHardeningCheckpointTests` | A07 | Source/project inspection uses Windows separators. |
| 1 | `CanonicalContextContractTests` | B01 | Canonical runtime context contains platform-dependent path text. |
| 1 | `WorkspaceFileServiceTests` | A01/A02 | Workspace physical/logical conversion differs on Unix. |
| 1 | `ProcessRunNarrativeGeneratorTests` | B06 | Narrative evidence exposes platform-dependent runtime path text. |
| 1 | `ProcessModuleBoundaryTests` | A07/B06 | Boundary source-inspection path is Windows-shaped. |
| 1 | `PluginWorkflowExecutorBoundaryTests` | A07/B05 | Boundary source-inspection path is Windows-shaped. |
| 1 | `AgentConversationContextServiceTests` | B01 | Context source/path representation is platform-dependent. |

## Failure patterns

- Drive-letter-only alias construction and hardcoded `src\...` source paths.
- Backslash-based repository/project path construction on `/repo`.
- Windows command, script activation, and working-directory expectations.
- Platform-dependent context, prompt, receipt, and metadata snapshots.
- Test-only source graph checks that are themselves not portable.

All failures are assigned to an existing core or runtime phase. Gate C0 does not claim they pass; it establishes their baseline and ownership. A07 must make core/source-inspection tests portable, and B01-B06 own the runtime failures after exact C4 handoff.
