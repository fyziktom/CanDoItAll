# 16 Maf Memory Tool Provider Selection And Policy

## Status

- `Completed`

## Objective

- Add generic MAF memory tool definitions, provider selection settings, policy enforcement, result shaping, and capability failure handling.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R09
- R10
- R11

## Prerequisites

- SB15 completed

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/AgentTools/ImageGenerationAgentRuntimeToolProvider.cs`
- `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tools.Abstractions/CanDoItAll.AgentFramework.Tools.Abstractions.csproj`
- `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/IAgentRuntimeToolProvider.cs`
- `repo://src/MAF/Tools/CanDoItAll.AgentFramework.Tooling/AgentRuntimeToolProviderContext.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderModels.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Providers/Contracts/ProviderCapabilityContracts.cs`
- `bundle://requirements/03-non-negotiable-boundaries.md`
- `bundle://analysis/04-live-repo-reentry-alignment.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Add generic MAF memory tool definitions for context query, ingestion, feedback, operation status, cancellation, and optional event acknowledgement where allowed.
- Add provider selection settings at agent/template/process/workflow levels with policy-enforced allowed provider ids or capability filters.
- Shape tool results for agent use with context summary, sections, citations/source refs, warnings, confidence, feedback handle, and async operation status.
- Add guardrails for tool use: disabled provider, unavailable capability, denied source scope, timeout, and async accepted result.
- Update agent template examples to assign business vs programming memory providers without hardcoding native Cognitive Memory.
- Implement registration through the current `IAgentRuntimeToolProvider` / `AgentRuntimeToolProviderContext` pattern; use existing runtime tool provider examples as the local convention.
- Define the no-provider behavior for tool exposure explicitly: either hide provider-backed tools by policy or expose tools that return typed no-provider diagnostics, but never dispatch to a hidden default provider.

## Dependency Impact

- Agent-level provider selection and tool invocation depend on this phase.

## Validation Depth

- `MAF integration foundation`

## Implementation Steps

1. Review existing tool registration patterns and place memory tool registration in a generic MAF-memory package/module.
2. Implement tool input/output records using Memory Protocol v1 and shared operation handler.
3. Add provider selection resolver for agent identity, process context, explicit override, and fallback policy.
4. Add tests for two agents using different providers and for denied capability/source scope.
5. Add documentation and examples for configuring memory providers per agent/workflow/template.
6. Add tests for current MAF runtime tool provider registration and no-provider tool behavior.

## Scope Exceptions

- No known scope exceptions for this subbundle at preparation time.
- If implementation discovers an exception, document it in `reviews/01-execution-report.md` and stop before downstream work if the exception affects a phase gate.

## Do Not Do

- Do not implement downstream subbundles early.
- Do not introduce direct generic-memory or MAF references to native Cognitive Memory implementation types.
- Do not add Qdrant as a base runtime dependency.
- Do not expose host EF entities or DbContext instances to memory providers.
- Do not duplicate memory operation dispatch logic outside the shared handler.

## Acceptance Checklist

- The implemented surface is observable through focused tests or explicit proof artifacts.
- Dependency boundaries from `requirements/03-non-negotiable-boundaries.md` remain intact.
- No downstream subbundle work is silently implemented or assumed.
- Execution report is updated with proof paths, command transcripts, and gate result.
- MAF memory tools depend only on generic memory abstractions/application contracts.
- Different agents can select different memory providers in the same process run.
- Tool responses include feedback handles and async status when the provider does not return immediate context.

## Proof Required

- Create `proof/SB16/manifest.md` or an execution-report proof row with changed files, validation commands, and source assertions for this subbundle.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Run MAF tool tests with two mock providers, disabled provider, unsupported capability, and async accepted result.
- Run dependency audit proving no MAF tool project references native Cognitive Memory namespaces.
- Run registration tests proving the generic memory tools flow through the current `IAgentRuntimeToolProvider` path.

## Browser Validation Logging

- N/A. This subbundle has no browser-visible surface. Record N/A in the execution report unless implementation touches a host-visible or browser-visible surface.

## Progression Gate

- Downstream subbundles may start only after SB16 proof is recorded, the acceptance checklist passes, and no phase-gate blocker remains.

## Completion Proof

- Manifest: `bundle://proof/SB16/manifest.md`
- Semantic invariants: `bundle://proof/SB16/semantic-invariants.md`
- Failing-first transcript: `bundle://proof/SB16/transcripts/failing-first-memory-tool-provider-tests.txt`
- Focused MAF memory tool provider tests: `bundle://proof/SB16/transcripts/passing-memory-agent-runtime-tool-provider-tests.txt`
- Native dependency audit: `bundle://proof/SB16/transcripts/source-audit-memory-tool-provider-boundary.txt`
- Dispatch boundary audit: `bundle://proof/SB16/transcripts/source-audit-memory-tool-provider-dispatch-boundary.txt`
- Solution build: `bundle://proof/SB16/transcripts/passing-solution-build.txt`
- Browser validation: `N/A`

## Suggested Agent Prompt

```text
Implement subbundle SB16 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
