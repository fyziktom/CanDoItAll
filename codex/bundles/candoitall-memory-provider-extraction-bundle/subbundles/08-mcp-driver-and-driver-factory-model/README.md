# 08 Mcp Driver And Driver Factory Model

## Status

- `Completed`

## Objective

- Add MCP-style memory driver abstractions and driver factory model for future MCP or MCP-like memory providers.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R03
- R05

## Prerequisites

- SB06 completed

## Exact Source References

- `repo://src/MAF/Mcp/CanDoItAll.AgentFramework.Mcp.Abstractions/CanDoItAll.AgentFramework.Mcp.Abstractions.csproj`
- `repo://src/Memory/CanDoItAll.Memory.Mcp/CanDoItAll.Memory.Mcp.csproj`
- `repo://src/Memory/CanDoItAll.Memory.Mcp/McpMemoryProviderDriver.cs`
- `bundle://architecture/02-protocol-contract-model.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Define MCP-style memory driver interfaces and mapping from Memory Protocol v1 capabilities to MCP/MCP-like tool/resource operations.
- Add driver factory support for MCP profiles and capability negotiation without coupling the generic memory module to any single MCP implementation detail.
- Add adapter contracts for query, ingestion, source request, feedback, event polling, and operation status where supported.
- Add unsupported-capability behavior for MCP memories that only provide simple context retrieval.
- Document how future MCP provider implementations plug into the generic registry.

## Dependency Impact

- Future providers and capability negotiation depend on driver factory correctness.

## Validation Depth

- `Driver contract`

## Implementation Steps

1. Review existing MCP abstraction package and prior MCP isolation bundle for current style and dependency constraints.
2. Create MCP driver abstractions/adapters that depend on generic memory contracts and MCP abstractions only.
3. Map provider manifests to Memory Protocol capabilities and record effective capability versions.
4. Add fake MCP transport tests for simple query, unsupported ingestion, async status, and provider event polling when available.
5. Update provider template with an MCP-style provider profile example.

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
- MCP-style providers are optional and selected through the same provider registry as HTTP/mock/native providers.
- Unsupported MCP capabilities fail during selection or dispatch with a structured diagnostic.
- No direct dependency from generic memory contracts to concrete MCP implementation packages is introduced.

## Proof Required

- Create `proof/SB08/manifest.md` or an execution-report proof row with changed files, validation commands, and source assertions for this subbundle.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Run MCP driver contract tests and provider-manifest mapping tests.
- Run dependency audit proving MCP driver code does not reference native Cognitive Memory.

## Browser Validation Logging

- N/A. This subbundle has no browser-visible surface. Record N/A in the execution report unless implementation touches a host-visible or browser-visible surface.

## Progression Gate

- Downstream subbundles may start only after SB08 proof is recorded, the acceptance checklist passes, and no phase-gate blocker remains.

## Suggested Agent Prompt

```text
Implement subbundle SB08 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
