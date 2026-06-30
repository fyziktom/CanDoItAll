# 02 Tooling Abstractions And Implementations

## Status

- `Completed`

## Objective

- Build the dedicated tool abstraction and implementation layer for internal and external tools, grouped by domain, with testable call mechanisms and explicit policy metadata.

## Success Criteria

- Internal tools can be resolved by implementation key and invoked through a mockable interface.
- External process/http tools can be invoked through bounded, schema-validated generic calls with deterministic failure categories.
- Tool metadata includes operation kind, side effects, approval defaults, target scope, process operation requirements, and receipt ownership.
- Every tool exposes the common capability exposure descriptor required by the access policy evaluator, including typed tags, operation classifications, runtime tool name, implementation key, and side-effect profile.
- External tool failures preserve executable/endpoint context, exit/status details, bounded output, masked secrets, correlation ID, and repair hints.

## Covered Inputs

- R01, R02, R04, R07, R08, R09, R11, R12, R13, R14, R15.
- User requirement for internal class/service tools and external python/exe/http-style generic tool calls.

## Prerequisites

- SB01 contracts and naming validation pass.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities/MafAgentRuntime.Capabilities.Tools.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolContractCatalog.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/ToolPolicy/ToolCapabilityRegistry.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/AgentTools/ImageGenerationAgentRuntimeToolProvider.cs`
- `repo://src/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs`
- `repo://src/CanDoItAll.Tools.Documents/CanDoItAll.Tools.Documents.csproj`
- `bundle://architecture/03-error-and-diagnostics-model.md`
- `bundle://architecture/04-implementation-quality-guardrails.md`
- `bundle://architecture/05-capability-access-policy.md`
- `bundle://analysis/03-codeanalytics-and-performance-review.md`

## Deliverables

- Tool implementation project with folders such as `Workspace`, `DotNet`, `Documents`, `Images`, `Processes`, `ProjectStructure`, `ProviderNative`, and `External`.
- Internal tool registry/resolver.
- Tool exposure descriptor factory that maps internal, external, and provider-native tools into the common access policy descriptor without raw string comparisons.
- External process/http invokers with schema validation, timeout, working directory policy, secret binding, logging, and masked diagnostics.
- Tool setup-test service that can execute deterministic fake external calls.
- Compatibility bridge from existing `IAgentRuntimeToolProvider` where needed.
- External call diagnostic mapper for process start, non-zero exit, timeout, invalid JSON, schema mismatch, HTTP status, cancellation, and cleanup failures.

## Dependency Impact

- SB05 hardens tool implementations before template/runtime consumption.
- SB06 uses tool descriptors for template materialization.
- SB08 uses tool services to replace MAF hardcoded switches.
- SB10 uses setup-test services for UI/API.
- SB11 depends on tool behavior parity for processes/workflows.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Map every existing runtime tool name, policy metadata, operation requirement, and side-effect classification to descriptors.
2. Implement internal resolver and fake test tool.
3. Implement tool exposure descriptor factory and tests for key, runtime name, tags, operation classifications, implementation key, and side-effect profile.
4. Implement external process invoker with bounded execution, cancellation, stdout/stderr size limits, explicit working-directory policy, and JSON result contract.
5. Implement external HTTP invoker with bounded request/response contract, `IHttpClientFactory` or equivalent managed client reuse, and masked header binding.
6. Move or wrap existing internal tool implementations into domain folders.
7. Split large providers into focused helpers where wrapping would otherwise create files over the guardrail threshold.
8. Add unit tests for successful calls, executable missing, command rejection, invalid working directory, non-zero exit, invalid JSON, schema mismatch, timeout, cancellation, masking, approval metadata, and exposure descriptor access-policy participation.
9. Add integration tests composing current representative tool families through the new services.
10. Record focused performance scan deltas for dispatch/materialization paths that replace existing MAF switches.

## Scope Exceptions

- Do not connect MAF runtime composition yet; that is SB08.
- Do not add UI editor surfaces yet; that is SB10.

## Do Not Do

- Do not execute arbitrary unbounded shell strings.
- Do not store raw secrets in template or catalog JSON.
- Do not remove `ToolContractCatalog` or policy behavior before parity tests pass.
- Do not read unbounded process output or HTTP responses into memory.
- Do not convert external invocation failures into one generic setup error.
- Do not create a separate tool-only suppressor that bypasses the shared capability access policy evaluator.

## Acceptance Checklist

- Internal fake tool call can be mocked in tests.
- External fake process/http tool calls return typed success/failure.
- External failures include category, capability key, implementation/transport context, bounded masked detail, and repair hint.
- Existing workspace, dotnet, browser, provider-native, finalizer, process, project-structure, and image-generation names remain stable.
- Tool policy metadata parity is covered by tests.
- Tool exposure descriptors can be denied by key, tag, operation classification, and runtime tool name through the shared policy evaluator.
- New tool implementation files follow domain folders and do not create new large orchestration files without an accepted-risk note.

## Proof Required

- Focused unit tests for internal and external tool invocation.
- Integration tests for representative existing tool families.
- Diagnostics samples for one process failure and one HTTP failure.
- Access-policy participation tests for internal, external, and provider-native tool descriptors.
- Focused performance/static scan summary for dispatch and serialization hotspots.
- `proof/SB02/manifest.md` with failing-first, passing, and anti-stub transcripts.
- `proof/SB02/semantic-invariants.md` covering name and policy parity.

## Execution Proof

- Manifest: `bundle://proof/SB02/manifest.md`
- Semantic invariants: `bundle://proof/SB02/semantic-invariants.md`
- Failing-first transcript: `bundle://proof/SB02/transcripts/failing-first-tool-implementation-contracts.txt`
- Passing targeted tests: `bundle://proof/SB02/transcripts/passing-tool-implementation-contracts.txt`
- Full build: `bundle://proof/SB02/transcripts/dotnet-build-solution.txt`
- Source assertions: `bundle://proof/SB02/transcripts/source-assertions.txt`
- Anti-stub audit: `bundle://proof/SB02/transcripts/anti-stub-audit.txt`
- Static/performance scan: `bundle://proof/SB02/transcripts/static-performance-scan.txt`
- Changed file hashes: `bundle://proof/SB02/changed-file-hashes.txt`

## Browser Validation Logging

- N/A for implementation project work. UI setup proof is SB10.

## Progression Gate

- Result: `Passed`
- SB02 proved internal tool resolution, external process/HTTP invocation contracts, setup-test propagation, access-policy participation, timeout/bounded-output diagnostics, and current tool metadata parity without connecting MAF runtime composition yet.

## Suggested Agent Prompt

```text
Implement subbundle SB02 only. Build the tool abstraction and implementation layer after reading SB01 proof. Preserve all existing runtime tool names, policy semantics, and operation classifications. Add exposure descriptor and fake internal/external tool tests before wiring any production path to MAF.
```

