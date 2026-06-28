# 05 Capability Core Hardening Checkpoint

## Status

- `Completed`

## Objective

- Harden, refactor, and performance-review the isolated Tool, Skill, MCP, exposure descriptor, and access policy evaluator implementation projects before template loading or MAF runtime paths consume them.

## Success Criteria

- Tool, Skill, and MCP services are mockable, isolated from MAF/UI, and split into focused domain folders.
- Capability exposure descriptors are consistent across tools, skills, MCP servers, and MCP tools.
- The access policy evaluator is deterministic, mockable, and covered for deny/require/allow precedence before templates or MAF consume it.
- Structured diagnostics are implemented consistently for loader, call, setup, lifecycle, timeout, cancellation, and cleanup failures.
- Focused performance review finds no obvious per-call template parsing, unbounded output reads, sync-over-async, uncached serializer options in hot paths, or unnecessary large LINQ materialization in dispatch.
- New large files, methods, dependency cycles, or stringly identifier switches are refactored or recorded as accepted risks with a concrete follow-up.

## Covered Inputs

- R01, R04, R05, R07, R08, R09, R11, R12, R13, R14, R15.
- User requirement to harden/refactor before reconnecting the rest of the app.

## Prerequisites

- SB02 tool proof passes.
- SB03 skill proof passes.
- SB04 MCP proof passes.

## Exact Source References

- `bundle://architecture/03-error-and-diagnostics-model.md`
- `bundle://architecture/04-implementation-quality-guardrails.md`
- `bundle://architecture/05-capability-access-policy.md`
- `bundle://inventories/04-capability-access-policy-test-inventory.md`
- `bundle://analysis/03-codeanalytics-and-performance-review.md`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Capabilities`
- `repo://src/CanDoItAll.AgentFramework.Tooling/IAgentRuntimeToolProvider.cs`
- `repo://src/CanDoItAll.Modules.Workbench/AgentTools/ProjectStructureAgentRuntimeToolProvider.cs`

## Deliverables

- Hardening report covering tools, skills, MCPs, exposure descriptors, access policy evaluator, diagnostics, dependency direction, file sizes, and performance scan results.
- Refactors needed to keep implementation projects focused and testable.
- Additional negative tests for external tool and MCP failure categories discovered during review.
- Additional policy tests for generic suppression of a fake new capability descriptor without evaluator code changes.
- Accepted-risk table for any deferred size/performance/cycle concern.

## Dependency Impact

- SB06 cannot build template materialization against unstable or happy-path-only services.
- SB08 cannot reconnect MAF if foundational services still hide errors or have direct MAF dependencies.

## Validation Depth

- `Mandatory hardening checkpoint`

## Implementation Steps

1. Run static searches for direct MAF/UI references from new abstraction and implementation projects.
2. Review all public interfaces for real boundaries and remove trivial abstractions that do not enable testing, transport separation, policy evaluation, or independent implementation.
3. Review exposure descriptors for common shape, typed identifiers, typed tags, and no raw selector string comparisons.
4. Review access evaluator precedence and split selector matching, precedence, diagnostics, and conversion helpers if they are becoming overgrown.
5. Split files over the guardrail threshold or document an accepted risk with a follow-up.
6. Run the focused performance scan from `analysis/03-codeanalytics-and-performance-review.md` against changed capability projects.
7. Add or repair tests for unhappy-path diagnostics across one external process tool, one external HTTP tool, one file/registered skill, one MCP setup flow, and one denied required capability.
8. Add a fake future capability descriptor test proving suppression by kind/tag does not require evaluator changes.
9. Verify serializer options/context reuse, bounded process/protocol output, cancellation propagation, cleanup behavior, and no per-call policy parsing.
10. Update `proof/SB05/manifest.md` and `proof/SB05/semantic-invariants.md` with hardening results.

## Scope Exceptions

- Do not add template materialization in this checkpoint.
- Do not reconnect MAF or UI in this checkpoint.

## Do Not Do

- Do not waive file-size, dependency, or performance concerns without an accepted-risk note.
- Do not use this checkpoint for unrelated broad refactors.
- Do not close with only successful call/load tests; negative diagnostics are required.
- Do not let capability-kind-specific suppressors survive outside the shared evaluator unless they are compatibility adapters with tests and removal notes.

## Acceptance Checklist

- No new capability abstraction or implementation project references MAF or Blazor.
- Every capability kind exposes common policy metadata and participates in the same evaluator.
- Deny wins over allow, required denied capabilities fail explicitly, and allow does not grant unassigned capabilities.
- External tool and MCP diagnostics include category, key/kind, transport, bounded masked detail, correlation ID, and repair hint.
- Cancellation and cleanup are tested for external tool and MCP flows.
- Focused performance scan findings are fixed or recorded.
- New files remain focused or have an accepted split plan.

## Proof Required

- Unit/integration test transcripts for added negative tests.
- Static scan summary for dependency direction and overgrown files.
- Focused performance scan summary.
- Access policy hardening summary and generic fake capability participation proof.
- `proof/SB05/manifest.md`
- `proof/SB05/semantic-invariants.md`

## Execution Proof

- Added SB05 hardening tests for typed diagnostics, cancellation, direct bearer-token masking, policy precedence, generic future capability suppression, and common exposure descriptor shape.
- Fixed external tool diagnostics masking so bearer tokens are masked before generic assignment masking.
- Split overgrown foundation files:
  - `Capabilities.cs` became focused enum, identifier, model, text, and name-rule files.
  - `CapabilityTemplateModels.cs` became template DTO, template validator, and policy compiler files.
- Verified dependency direction: no MAF, Blazor/UI, Radzen, `Microsoft.Agents`, or `ModelContextProtocol` references in isolated capability projects.
- Verified focused performance patterns: no sync-over-async, blocking read, ad hoc serializer-options, reflection, or service-locator matches.
- Verified file-size gate: all isolated capability foundation files are below 500 lines.
- Targeted SB05 hardening tests passed: `bundle://proof/SB05/transcripts/passing-capability-hardening-tests.txt`.
- Existing SB02 tool implementation regression tests passed: `bundle://proof/SB05/transcripts/regression-tool-implementation-contracts.txt`.
- Full solution build passed with 0 warnings and 0 errors: `bundle://proof/SB05/transcripts/dotnet-build-solution.txt`.
- Critical proof manifest and semantic invariants are recorded at `bundle://proof/SB05/manifest.md` and `bundle://proof/SB05/semantic-invariants.md`.

## Browser Validation Logging

- N/A. This checkpoint has no browser-visible surface.

## Progression Gate

- Passed. SB06 is unblocked because isolated capability services are hardened for dependency direction, diagnostics, policy behavior, file size, and performance guardrails.

## Suggested Agent Prompt

```text
Implement subbundle SB05 only. Harden the isolated Tool, Skill, MCP, exposure descriptor, and access policy foundation before templates or MAF consume it. Focus on dependency direction, structured diagnostics, unhappy-path tests, file size, cancellation/cleanup, generic suppression behavior, and performance scan findings. Do not add template loading or reconnect MAF.
```
