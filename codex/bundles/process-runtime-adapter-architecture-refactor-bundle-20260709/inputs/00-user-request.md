# User Request

The user requested a new bundle only, with no implementation yet.

Key instructions:

- Act as senior C# architect/developer.
- Use `candoitall-bundle-workflow`.
- Use `csharp-architecture-governor` and other C# architecture skills.
- Refactor `src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.cs` properly because the current partial-class approach is wrong architecture.
- Split the too-large adapter into smaller, testable, responsibility-focused parts.
- Avoid leaking domain-related code into generic processes runtime and dispatcher.
- Process templates may contain domain relation at the correct level, for example writing a .NET app, but the generic runtime must support any app, not Tetris or Calculator only.
- Address domain leaks such as `IsDotNetRuntimeLifecycleTool` in `src/MAF/Common/CanDoItAll.AgentFramework.Core/Workspace/Commands/WorkspaceCommandReceiptWriter.cs`.
- Use domain-related drivers where needed, but isolate them properly in process drivers.
- Use information from GPTPro root-cause analysis in:
  - `C:\repositories\CanDoItAll\codex\bundles\tetris-process-rootcause-workflow-bundle-20260709`
  - `C:\repositories\CanDoItAll\codex\bundles\escalation_root_cause_bundle`
- Prepare bundle only now. Do not implement.

## Assumption

This bundle may add files under `codex/bundles/...` only. Production source code remains unchanged during preparation.

