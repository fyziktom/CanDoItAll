# Current State

## Summary

The codebase no longer hides `MafAgentRuntime` behind multiple partial files, but the runtime is still a responsibility-heavy object graph. The previous phase reduced one visible smell while leaving the core architectural problem: runtime execution, capability composition, hosted-agent construction, and workspace tools are still concentrated in a few broad classes that must be edited for unrelated reasons.

## Large Types

| Type | Evidence | Main responsibilities currently mixed |
| --- | --- | --- |
| `MafAgentRuntime` | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`; local count 1779 lines; CodeAnalytics 59 members | `IAgentRuntime` facade, provider diagnostics, input preparation delegation, runtime build invocation, provider streaming loop, repeated tool guard, finalizer repair, finalizer JSON fallback, process artifact recovery response, session serialization, pending approval cache, usage diagnostics, background continuation. |
| `RuntimeCapabilityComposer` | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/RuntimeCapabilityComposer.cs`; partial cluster; CodeAnalytics 106 members | access planning, capability descriptor building, catalog descriptor mapping, workspace plugin creation, storage plugin creation, context provider attachment, skill attachment, configured workspace tool attachment, runtime tool provider attachment/filtering, A2A tools, compaction, path resolution. |
| `MafRuntimeAgentFactory` | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/MafRuntimeAgentFactory.cs`; local count 886 lines; CodeAnalytics 31 members | hosted-agent creation, runtime build, handoff runtime build, instrumented agent creation, finalizer capture/tool injection, approval tool filtering, script policy inspection, managed root path policy, credential environment promotion, chat history provider construction. |
| `WorkspaceRuntimePlugin` | `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Workspace/WorkspaceRuntimePlugin.cs`; local count 922 lines; CodeAnalytics 93 members | file read/write, workspace search, path stat, git tools, dotnet tools, script execution, document conversion, spreadsheet inspection, image inspection, image analysis, external-target policy, delete protection, current-run artifact path normalization. |

## Current Positive Work

- `MafAgentRuntime` partial files have been removed.
- Several collaborators already exist: `InputAttachmentPreparer`, `InputAttachmentSupport`, `RequestScopedSessionContentScrubber`, `RuntimeToolProviderComposer`, `ProviderRuntimeDiagnostics`, `ProcessArtifactRecoveryService`, `WorkspaceSearchSupport`, `StorageRuntimePlugin`.
- `MafRuntimeArchitectureServicesTests` already asserts several collaborators are top-level and prevents `MafAgentRuntime` partial reintroduction.

## Remaining Architectural Smells

- `MafAgentRuntime` still uses `IServiceProvider` and constructs major collaborators directly in its constructor.
- `RuntimeCapabilityComposer` uses partial classes as a final architecture boundary.
- `RuntimeCapabilityComposer.CreateDefault` and tests still require a real `ServiceProvider` for broad capability behavior.
- `MafRuntimeAgentFactory` receives `IServiceProvider`, passes it into provider construction, and owns too much runtime build policy.
- `WorkspaceRuntimePlugin` puts unrelated tool families behind one object and shares policy/path logic through private methods.
- Architecture tests guard against one previous smell but do not yet prove thin-runtime delegation, direct extracted-unit tests, or extension without editing old large types.

## Performance Impact Hypothesis

The runtime can load slowly because the constructor and runtime build path resolve broad dependency graphs and prepare broad capability/tool surfaces even when a run needs a narrower set. This bundle does not claim a measured regression yet. It requires timing proof around runtime construction, capability composition stages, and turn execution before and after each critical extraction.

## Testability Impact

The existing shape forces many tests to instantiate `RuntimeCapabilityComposer.CreateDefault(...)`, `MafAgentRuntime`, or real `ServiceCollection` instances. That makes unit tests slower, encourages broad integration tests for local behavior, and hides negative cases behind full runtime setup.
