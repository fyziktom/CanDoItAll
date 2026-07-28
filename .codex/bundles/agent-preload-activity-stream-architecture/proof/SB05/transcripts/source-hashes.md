# SB05 Direct Source Hash Transcript

## Run metadata

- Class: `Direct`
- Algorithm: `SHA-256`
- Working directory: `C:\repositories\CanDoItAll`
- Run date: `2026-07-27`
- Command: `Get-FileHash -Algorithm SHA256 -LiteralPath <path>`
- ExitCode: 0
- Meaning: current working-tree evidence identity, not an assertion that SB05 changed
  these production/test files

## Production sources

| SHA-256 | Repository path |
| --- | --- |
| `619c68ffd3d24ec4559112056ca46efc4d3f54b78d714091d8a3e0b28e53a8c3` | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentExecutionActivityCoordinator.cs` |
| `5de4dda18157fe59036a89991b1f846579933fede84b4228e16d77c7936e420d` | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Workspace/CurrentProfileAgentFrameworkWorkspaceService.cs` |
| `4634a8aed2cc98a5237e739d8c8b0caa0cda66029629ffbbf0c439bf312c31cc` | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` |
| `dc55ff6b7a1e8ff1d0412bc3b306cc81541e85baee5401df6052380f2ef38260` | `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Providers/ProviderRuntimeProfileSnapshotService.cs` |
| `e6b270079a4defa8a54ae625786b0bba5fc1f149503eab32166236321f05ab1f` | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceStore.cs` |
| `e1ae9e9af5f28ac4a656f7604f1f6253ff272ff186e5bf10e337c5fc49bb5078` | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceExecutionSliceStore.cs` |
| `1f480d75d62eb4e06f1b10db2d3494222ce75c661479e12cf4c6fb9e43a3387c` | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Persistence/Storage/FileSandboxWorkspaceJsonStore.cs` |
| `346702d3088230558448d308d87c29f79877ea2276d7289f332436de575f88e0` | `repo://src/Processes/CanDoItAll.Processes.Application/ProcessRuntimeProjectionQueryService.cs` |
| `eff591252ec987cc7c2cc0d07bf78e81aa1ed6512ffe32b759834b82b324bc8b` | `repo://src/Processes/CanDoItAll.Processes.Persistence/EfProcessRuntimeUnitOfWork.cs` |
| `72f843145b916885df82c3506601c93f1f7e467b7b23496dbee70660ff078625` | `repo://src/Processes/CanDoItAll.Processes.Persistence/EfProcessRuntimeStepAssignmentStore.cs` |
| `56ed0bee083bac063a7d7fa7de3498765a8c489337309d00abe9ee2e4501f98b` | `repo://src/Foundation/CanDoItAll.Infrastructure/Persistence/DatabaseRuntimeSwitching.cs` |

## Test sources

| SHA-256 | Repository path |
| --- | --- |
| `c8d6573d917ff20fb7073602676a7afcf832201013f7da083b6e411296726c94` | `repo://tests/Integration/CanDoItAll.Tests.Integration/AgentFrameworkExecutionRunTrackingIntegrationTests.cs` |
| `2cdc733583cfcc9da7a16e83911b8022ea30bead02dbe341cdd1550450d7e0ef` | `repo://tests/Integration/CanDoItAll.Tests.Integration/FileSandboxWorkspaceAdmissionReadScalingIntegrationTests.cs` |
| `7b3837889f11824acc2447d87c6aef2595327d4dfe7c4b3fd22c3a014b19c225` | `repo://tests/Integration/CanDoItAll.Tests.Integration/FileSandboxWorkspaceGenericNewRunCommitRecoveryIntegrationTests.cs` |
| `47b77a556f3006e1edc87395afda22f164d9dfa0f8c6ba4bbd4a6fc76aaedf3e` | `repo://tests/Integration/CanDoItAll.Tests.Integration/FileSandboxWorkspaceChatRunCommitRecoveryIntegrationTests.cs` |
| `3266bbda50622fea913afb12a4d541a6818bc6c621063e6fa9ecef507f989451` | `repo://tests/Integration/CanDoItAll.Tests.Integration/FileSandboxWorkspaceExistingRunUpdateRecoveryIntegrationTests.cs` |
| `bef58172567c0e73977bcdfa97a4ed0195ba30fd8fd9a36b253bf3947620d10e` | `repo://tests/Integration/CanDoItAll.Tests.Integration/FileSandboxWorkspaceUsageProjectionIntegrationTests.cs` |
| `652c591705902cebbea9c0ed7c948c9263920a42ac9d28fac6e7e9a2bb886aa5` | `repo://tests/Unit/CanDoItAll.Tests.Unit/ProviderRuntimeProfileSnapshotServiceTests.cs` |
| `c0491bbe49a95ce089c5686393bafb9417beb9ddaf0d1199612031e17bb7d835` | `repo://tests/Unit/CanDoItAll.Tests.Unit/ProcessProjectionPipelineTests.cs` |
| `d63098574803f4bc08da7b5679f20a5b68a85c6e26b4e6c8ef70719a422f82e6` | `repo://tests/Unit/CanDoItAll.Tests.Unit/CurrentProfileAgentExecutionActivityAdmissionTests.cs` |
| `0b4eb74df88866fe92fe2d218c532088a835e6864f5991d6f17380d843159ed8` | `repo://tests/Unit/CanDoItAll.Tests.Unit/AgentExecutionActivityDependencyInjectionTests.cs` |

## Command form

```powershell
Get-FileHash -Algorithm SHA256 -LiteralPath <path>
```
