# Observed Weak Spots For Later Hardening

These are input observations, not architecture proposals.

## 1. Large Responsibility Centers

Several files carry enough responsibility that they should be reviewed for maintainability, test seam quality, and semantic drift:

- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ArtifactValidation.cs`
  - 3,913 lines.

- `src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor.cs`
  - 3,515 lines.

- `src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor`
  - 3,170 lines.

- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs`
  - 2,347 lines.

- `src/CanDoItAll.AgentFramework.Core/ToolPolicy/AgentToolInvocationPolicy.cs`
  - 2,115 lines.

- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Dispatch.cs`
  - 1,994 lines.

- `src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.ToolValidation.cs`
  - 1,897 lines.

- `src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.AgentFactory.cs`
  - 1,766 lines.

- `src/CanDoItAll.Modules.Processes/Runtime/ProcessesService.Runtime.RunStart.cs`
  - 1,692 lines.

- `src/CanDoItAll.Modules.Processes/Launch/ProcessesService.Launch.Staffing.cs`
  - 1,653 lines.

Input concern:

These files likely contain several subdomains each: validation, projection, state transitions, tool policy, artifact trust, runtime evidence, UI state, and storage concerns. The later bundle should distinguish accidental size from necessary orchestration complexity.

## 2. Canonicity Drift Across Runtime, Templates, And Skills

The same rules are encoded in many places:

- process template JSON
- process template markdown
- dispatch prompt text
- agent template instructions
- API skill docs
- seed catalog resources
- UI editor behavior
- runtime validation code
- test fixtures and baseline scenarios

Drift-prone concepts:

- allowed process operations
- operation target scopes
- artifact expectation satisfaction status
- workflow output mapping
- subprocess child artifact mapping
- browser evidence requirements
- runtime command ownership
- external-target alias boundaries
- current-run evidence freshness
- provider/capability proof status

Input concern:

The successful Tetris run indicates the rules can work together, but the stale `49fd...` lineage and QA recovery show that current-run evidence identity is still fragile enough to merit focused analysis.

## 3. String-Key And JSON-Path Surface

Scans found heavy use of string comparisons, JSON paths, action ids, executor ids, and tool names around:

- workflow canvas editor
- workflow executor catalog
- MAF capability builders
- workspace tool mapping
- process dispatch artifact validation/projection
- template JSON
- agent capability setup UI

Examples of string-driven identifiers:

- `workspace_dotnet_run`
- `workspace_dotnet_new`
- `browser_take_screenshot`
- `office365.messages-by-category`
- `office365.mark-message-processed`
- `workflow-executor:create:...`
- JSON paths like `$.status`, `$.route`, `$.inputPayload.runContext.office365Processing.messageIds[0]`

Input concern:

The project already uses typed wrappers in parts of the workflow/process model. The later hardening bundle should inspect where string identifiers are external protocol boundaries versus internal magic strings that should be pulled behind source constants or typed wrappers.

## 4. Threading, Cancellation, And Long-Running Processes

Searches found relevant hotspots:

- `SecretStoreAgentProviderCredentialResolver.cs` uses `Task.Run` to call async secret resolution from a synchronous resolver path.
- `WorkspacePathAliasSession.cs` uses `process.WaitForExit()` and synchronous `ReadToEnd()`.
- `ProcessRunAutomationDispatchService.DotnetRunCleanup.cs` uses `process.WaitForExit(5_000)`.
- `ProcessRunAutomationDispatchService.StepCompletionFinalizer.cs` uses `CancellationToken.None` for storage read/copy operations.
- `ProcessesService.Launch.cs` and `ProcessWorkspace.RuntimeOperations.cs` include `CancellationToken.None` paths.
- `ProcessRunAutomationDispatchService.Dispatch.cs` uses static `ConcurrentDictionary<Guid, SemaphoreSlim>` dispatch guards.
- `ProcessOutbox.cs` uses bounded parallelism via `SemaphoreSlim`.
- `WorkflowRuntimeManager.cs` uses multiple in-memory `ConcurrentDictionary` and `ConcurrentQueue` structures.
- `LiveProcessesDashboard.razor` has multiple refresh cancellation-token paths.

Input concern:

The current tool and browser-proof improvements directly relate to deadlocks, locked build outputs, and long-running host cleanup. These are not theoretical issues; the prep run encountered a locked-output build failure from an existing host process.

## 5. Runtime Host And Database Profile Drift

Observed during preparation:

- Port 5032 was expected but initially unavailable.
- Port 5034 had a running app against `candoitall_codex_graphs_20260601`.
- The requested development database evidence required starting a separate 5032 host.
- A `dotnet run` attempt failed because existing process output locks prevented rebuild.

Input concern:

Run validation and API evidence can be wrong if the caller hits a different port/profile than expected. The later hardening bundle should include runtime profile identity and database profile verification as first-class evidence concerns.

## 6. Browser Proof Policy Mismatch Risk

The process steps and agent instructions now emphasize:

- `workspace_dotnet_run`
- `keepAlive`
- lifetime scope
- browser proof
- screenshots
- console messages
- current-run artifact paths
- cleanup receipts

The Tetris run succeeded with browser proof, but the metadata observed during earlier inspection included a mismatch pattern: process-level allowed operations may include runtime/browser proof while some agent process-browser-tool flags can still be false depending on dispatch context.

Input concern:

The later bundle should verify the actual policy path from process step allowed operations to agent tool availability, especially for browser proof steps.

## 7. Workflow External Side Effects

The Office365 workflow:

- read one message from `CanDoItAllSummaryTest`
- summarized it
- stored a project-structure summary
- moved it to `CanDoItAllSummaryTestProcessed`

Input concern:

This is correct behavior for the tested workflow, but workflow hardening must separate:

- discovery/listing
- preview/dry-run
- processing with side effects
- idempotent retry
- duplicate prevention
- processed-marker policy

The existing workflow should not be rerun accidentally during analysis.

## 8. Post-Release Learning Evidence

The post-release process step itself noted weak structured telemetry/support-observation inputs.

Input concern:

The process can close successfully even when post-release observation is mostly qualitative. That may be acceptable for local demo apps, but ChatGPT Pro should consider this an input for future process hardening.

