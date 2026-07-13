# Live Repository Re-entry Alignment

## Re-entry status

- Current re-entry date: 2026-07-12.
- Main repository: `C:\repositories\CanDoItAll`.
- External Cognitive Memory repository: `C:\repositories\CanDoItAll.CognitiveMemory`.
- Both repositories were clean when the current audit began.
- Bundle disposition: `REOPENED`.
- Current progression gate: `SB35 PASSED; SB36 ACTIVE`.
- SB01-SB34 are historical implementation records. Their previous pass labels do not establish that the current source meets R22-R29.

This file supersedes its 2026-07-05 statement that the external repository contained only a README. The external repository now contains domain, application, persistence, HTTP service, worker, MAF, UI, projection, and test projects. The repair must analyze and test that real implementation rather than scaffold an empty repository.

## Current repository reality

The main repository contains a generic memory stack under:

- `repo://src/Memory/CanDoItAll.Memory.Abstractions`
- `repo://src/Memory/CanDoItAll.Memory.Application`
- `repo://src/Memory/CanDoItAll.Memory.Http`
- `repo://src/Memory/CanDoItAll.Memory.Mcp`
- `repo://src/Memory/CanDoItAll.Memory.Persistence`
- `repo://src/Modules/CanDoItAll.Modules.Memory`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework`

The external repository contains a working service shape under sibling-repository paths including:

- `C:\repositories\CanDoItAll.CognitiveMemory\src\CanDoItAll.CognitiveMemory.Domain`
- `C:\repositories\CanDoItAll.CognitiveMemory\src\CanDoItAll.CognitiveMemory.Application`
- `C:\repositories\CanDoItAll.CognitiveMemory\src\CanDoItAll.CognitiveMemory.Persistence`
- `C:\repositories\CanDoItAll.CognitiveMemory\src\CanDoItAll.CognitiveMemory.Service`
- `C:\repositories\CanDoItAll.CognitiveMemory\src\CanDoItAll.CognitiveMemory.Workers`
- `C:\repositories\CanDoItAll.CognitiveMemory\tests\CanDoItAll.CognitiveMemory.Tests`

The extraction goal is therefore no longer project creation. It is architecture repair, protocol/runtime completion, security hardening, and conformance proof across the two repositories.

## Architecture findings that reopened the bundle

### Capability-grouping partial classes

The live implementation uses partial files to group unrelated capabilities instead of extracting cohesive collaborators. Current examples include:

- `repo://src/Memory/CanDoItAll.Memory.Application/MemoryOperationHandler.cs`
- `repo://src/Memory/CanDoItAll.Memory.Application/MemoryOperationHandler.Helpers.cs`
- `repo://src/Memory/CanDoItAll.Memory.Application/MemoryOperationHandler.Status.cs`
- `repo://src/Memory/CanDoItAll.Memory.Application/MemoryOperationHandler.SourceCapture.cs`
- `repo://src/Memory/CanDoItAll.Memory.Application/MemoryOperationHandler.FeedbackEvent.cs`
- `repo://src/Memory/CanDoItAll.Memory.Http/HttpMemoryProviderDriver.cs` and its request/response partials
- `repo://src/Memory/CanDoItAll.Memory.Mcp/McpMemoryProviderDriver.cs` and its request/response partials
- `repo://src/Memory/CanDoItAll.Memory.Application/MemoryProviderEventWorker.cs` and `MemoryProviderEventWorker.Outbox.cs`
- `repo://src/Memory/CanDoItAll.Memory.Persistence/EfMemoryRetentionProjectionStore.cs` and `EfMemoryRetentionProjectionStore.Apply.cs`

Generated regex partials and Blazor component code-behind remain allowed. The files above require real responsibility extraction, with dependencies and tests moving to top-level services. Moving the same methods to another partial file is not an acceptable repair.

### Misplaced runtime ownership

Agent memory configuration, policy resolution, tool exposure, and context contribution currently live inside the broad Agent Framework module:

- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/AgentMemoryAccessMetadata.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/MemoryAgentRuntimeToolProvider.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Context/MemoryAgentContextContributor.cs`

The repair must assign an explicit owner to typed agent-memory configuration and generic MAF runtime integration. Project dependencies must point toward generic contracts/application ports; application code must not absorb MAF, transport, persistence, UI, or external-service concerns.

### Agent settings and invocation are incomplete

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Editors/EditorModels.cs` exposes typed settings for several agent capabilities but not memory.
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Pages/Components/AgentDetailsDialog.razor` has no memory settings surface.
- The current memory metadata uses booleans plus preferred/default provider strings. It has no typed `Disabled`, `Automatic`, or `ExplicitDirective` mode and no typed alias binding collection.
- No production parser or authorization path exists for `/mem:<alias>`.
- The context contributor queries one resolved provider per eligible invocation. It does not implement one-agent/many-provider routing or deterministic, provider-labelled merge behavior.

SB37 must add typed settings persistence and UI, invocation modes, aliases, directive parsing/sanitization, and multi-provider orchestration through the current MAF seams. Compatibility behavior for existing agents must be explicit and tested; malformed settings must not silently become permissive defaults.

### Provider selection and operation authorization fail closed only on paper

The registry currently can select the first enabled compatible provider when an explicit request, assignment, or default does not resolve, without consistently honoring deny-fallback intent or the agent's allowed provider set. Registry order is not a valid policy.

Operation status and cancellation are addressable by operation ID without a complete persisted requester/agent/session ownership check. A caller must not learn status or cancel work merely by possessing a GUID. SB36 owns explicit selection policy, allowed-provider enforcement, ambiguity/no-provider results, and operation ownership authorization.

### Runtime context is dropped or reconstructed through tags

The current agent tool provider expects workflow/process identity in optional string tags, while production runtime composition does not populate all of those keys. The context contributor does not carry the complete runtime intent. HTTP and MCP request mapping can emit `MemoryWorkspaceContext.None` or null project identity even when the invocation is project scoped.

This breaks strict external project filtering and makes authorization unreliable. SB37-SB39 must propagate typed requester, agent, session, workspace, project, process, workflow, node, and step identity through the real MAF invocation and protocol envelope. Missing or malformed required scope must fail explicitly instead of degrading to global memory.

### Transport configuration and capability claims are unsafe

Provider management is concentrated in `repo://src/Modules/CanDoItAll.Modules.Memory/Services/MemoryProviderManagementUiService.cs`. Editing a profile does not reliably preserve all driver-specific extensions and selection metadata. Raw connection credentials can be represented in extension payloads. MCP exists as a driver project but is not proven through production composition. Some asynchronous ingestion, feedback, event, status, and provider-specific UI claims are not backed by an end-to-end implementation.

SB38 must preserve configuration losslessly, use secret references, register supported drivers explicitly, and make manifests/capabilities honest. Unsupported operations must be omitted or rejected with typed diagnostics rather than advertised optimistically.

### External Cognitive Memory is not yet a secure external provider

The service routes are defined in `C:\repositories\CanDoItAll.CognitiveMemory\src\CanDoItAll.CognitiveMemory.Service\CognitiveMemoryProtocolApi.cs`. The current seam lacks sufficient authentication, caller/project authorization, request constraints, and enforced access/redaction policy. Malformed project input must not map to global scope. Recall must not return restricted, unapproved, review-pending, or redacted records outside an explicit policy decision.

The external solution also retains direct sibling-repository project references for shared contracts/runtime types. That is usable for local development but is not proof of an independently versioned protocol boundary. SB39 must secure the hosted service, align its manifest with real behavior, and run the main `CanDoItAll.Memory.Http` driver against the hosted external application.

## Dependency and composition findings

- The generic application project currently depends on Agent Framework Core for source snapshot contracts. The target dependency direction must be documented and either repaired now or recorded as an owned, bounded migration debt with a guard against further leakage.
- Generic application service registration is partly owned by the persistence layer. Application registration belongs with application services; persistence may offer only persistence-specific wiring or a clearly marked compatibility bridge during migration.
- `repo://src/App/CanDoItAll.Composition/CanDoItAll.Composition.csproj` and `repo://src/App/CanDoItAll.Composition/ModuleAssemblies.cs` still contain native Cognitive Memory references discovered by the baseline dependency-removal tests. Base composition must compile and test without the native module.
- Legacy workspace-memory attachment currently exists alongside the new provider path. Its compatibility condition must be explicit so configured provider modes do not receive hidden duplicate memory behavior.

## Baseline proof at current re-entry

The initial audit established these baselines before repair:

- Main generic memory test project: 98 passed, 2 failed. Both failures are host-composition dependency-removal guards detecting retained native module references.
- Focused main agent-memory unit tests: 45 passed.
- External Cognitive Memory tests: 28 passed.
- CodeAnalytics loaded the relevant main and external solutions without a blocking analysis error and found no project-reference cycle in the scoped dependency graph. That does not waive the misplaced dependency direction, large responsibility clusters, module/type cycles, or generated duplicate diagnostics recorded by the audit.

Passing focused tests demonstrate existing behavior, not R22-R29 acceptance. The missing negative authorization, multi-provider, mode/directive, project-context, transport-preservation, external-security, and real cross-seam scenarios are mandatory repair proof.

## Current progression rules

- SB35 must create and pass the C# architecture artifacts and characterize current failures before any production code is edited.
- SB36-SB39 implement the repair in dependency order and may not claim success with shallow tests or wrapper-only extractions.
- SB40 must run current builds/tests in both repositories, dependency/partial/secret guards, real composition, agent runtime scenarios, external hosted conformance, and independent architecture review.
- Historical proof manifests may be cited as context but may not be copied forward as current proof.
- Any unavailable component-catalog or browser transport must be recorded as a validation gap and replaced with the strongest available local source/runtime evidence; it must not be silently marked passed.
