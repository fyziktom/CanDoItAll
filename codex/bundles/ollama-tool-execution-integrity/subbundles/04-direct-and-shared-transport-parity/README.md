# SB04 — Direct And Shared Transport Parity

## Status

- `Ready` — specification ready; implementation has not started. Prerequisites still gate entry.

## Objective

Prove equivalent tool semantics through direct native Ollama and the actual shared-provider OpenAI-compatible route, fixing only demonstrated adapter defects.

## Covered Inputs

- N05, N06, N07; R06, R10; transport findings and F01/F03 regression surface.

## Prerequisites

- SB00 post-upgrade SDK baseline and SB01–SB03 closure gates passed.
- Confirm actual SDK/package versions, shared publication capabilities and source relay routes; retain captured schema baseline.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/MafProviderAgentFactory.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Maf/Runtime/Providers/OllamaToolResultProtocolHandler.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderRuntimeProfileMaterializer.cs`
- `repo://src/App/CanDoItAll.Composition/SharedProviderRuntimeHttpClientSelector.cs`
- `repo://src/Integration/CanDoItAll.SharedProviders.Abstractions/SharedProviderProtocol.cs`
- `repo://src/Integration/CanDoItAll.SharedProviders.Http/SharedProviderRelayRequestPolicy.cs`
- `repo://src/Integration/CanDoItAll.SharedProviders.Http/SharedProviderRelayPolicies.cs`
- `repo://src/Integration/CanDoItAll.SharedProviders.Http/SharedProviderRelayAdapterRegistry.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/SharedProviderRelayPolicyTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/OllamaToolResultProtocolHandlerTests.cs`

## Deliverables

- Tests through actual SDK serializers and the real shared source endpoint with a scripted external upstream.
- Recorded canonical schema comparison for nested request, required keys, enums, descriptions and size limits.
- Correlated call/result/continuation and error/streaming parity evidence; only necessary fixes in existing transport adapters.

## Dependency Impact

- SB06 live acceptance depends on this protocol proof. Adapter or SDK changes invalidate earlier boundary/projection tests that depend on message shape.

## Validation Depth

- Proof tier: `Behavioral`.
- Test project/filter/expected exact cases: V04 in [validation-plan.md](../../plan/validation-plan.md). Planned new cases are explicitly identified there; no test has been implemented or passed during preparation.
- Selection reason: Fake only the remote provider server. Shared test traverses actual consumer client, Web source endpoint, request policy and relay adapter.
- Invalidation keys: SDK/package, relay policy, publication capability, streaming parser, tool-result normalization or history projection changes reopen SB04 and SB06.
- Broad-gate decision: Not required in this phase; shared receipt/persistence contract trigger is consolidated at the final frozen SB06 checkpoint.
- SB06 live acceptance depends on this protocol proof. Adapter or SDK changes invalidate earlier boundary/projection tests that depend on message shape.
- Every protected source/test change requires the portability procedure and final enforcement without --write-baseline.

## Implementation Steps

1. Turn the isolated diagnostic schema capture into stable focused tests with actual published-model routing and source relay validation.
2. Script well-formed and malformed calls, two sequential tool results, supported multiple calls, missing/duplicate correlation IDs and streamed call fragments.
3. Assert equivalent safe error content and typed application outcomes across native/shared paths; preserve protocol differences inside adapters.
4. Test explicit capability rejection, upstream failure and cancellation without local mutation, credential exposure or invented success.
5. If all transport assertions pass, ship only the proof; source changes require a failing transport fixture.
6. Run V04 and selected existing protocol/relay regressions; record wire evidence with credentials and content redacted.

## C# Architecture Impact

SDK details in Maf Runtime/Providers; relay protocol in SharedProviders.Http; shared profile mapping only selects endpoint/capabilities. Follow [architecture checkpoints](../../plan/architecture-checkpoints.md).

## Boundary Ownership

SDK details in Maf Runtime/Providers; relay protocol in SharedProviders.Http; shared profile mapping only selects endpoint/capabilities.

## Dependency Direction

Preserve [the approved project directions](../../architecture/02-csharp-dependency-direction.md). No new project reference is planned; no Core-to-Maf/Workbench/Web or neutral-contract-to-SDK dependency.

## Pattern Decision

Existing protocol adapters and request policies; no new provider abstraction or application policy branch.

## Testability Contract

Fake only the remote provider server. Shared test traverses actual consumer client, Web source endpoint, request policy and relay adapter. Expected discovery must match V04; test-created success artifacts cannot substitute for production producer/consumer proof.

## Partial Class Policy

No new partial-file architecture. Touched orchestration partials may delegate to cohesive top-level policies; existing facade roles remain. Document the actual responsibility removed from a hotspot.

## Architecture Proof Required

- Record actual changed types, callers, constructor dependencies and before/after project references.
- Run relevant CodeAnalytics or explicit dependency review, affected builds and the C# architecture gate.
- Reject wrapper-only extraction, service-locator wiring, unused abstractions and untyped context bags.

## UI Composition Contract

N/A — backend contract phase; user-visible status/refresh proof is owned by SB05/SB06.

## Scope Exceptions

- The initial investigation proves the captured direct run only. Shared live behavior is pending SB06.
- User requested preparation only; all implementation and product validation in this specification are future work.

## Do Not Do

- Do not treat a raw OpenAI SDK fake handler as the full shared relay. Do not change agent business policy per ProviderKind, replace providers, silently downgrade unsupported features or upgrade SDKs speculatively.

## Acceptance Checklist

- projectId/request and nested parentNodeKey/sourceWorkspacePath survive both complete routes.
- Tool names, arguments and call-result identity remain intact through streaming and sequential calls.
- Malformed/unmatched tool results fail explicitly without executing a different tool.
- Prior scoped outcome evidence and current safe binding feedback reach both clients equivalently.
- Unsupported capabilities and upstream failures remain explicit; no secret headers or raw errors enter public evidence.

## Proof Required

- V04 plus exact existing protocol/relay cases, changed-owner builds and static gate when protected code/tests change.
- Sanitized native request/response and shared consumer/source/upstream message evidence with asserted correlation and schema fields.
- Record direct/native versus shared/OpenAI route identity and capability configuration; distinguish deterministic relay proof from later live model proof.
- Record exact commands, expected/actual discovery and exit codes. Zero or unexpected discovery is a failed proof.
- Use [semantic evidence rules](../../plan/validation-plan.md#semantic-evidence) and preserve both positive and adversarial negative results.

## Browser Validation Logging

- N/A — no browser-visible markup change in this phase. SB05/SB06 own browser proof.

## Progression Gate

- Proceed to live acceptance only when both full transport paths preserve the tool contract and error evidence.
- Any demonstrated protocol mismatch reopens the owning adapter and affected upstream boundary assertions.

## Reopen Triggers

- SDK/package, relay policy, publication capability, streaming parser, tool-result normalization or history projection changes reopen SB04 and SB06.

## Suggested Agent Prompt

```text
Execute this subbundle only after the user authorizes implementation. Verify prerequisites and current source. Preserve the outcome, ownership and scope contracts. Capture the required production-path proof, update the execution report, and stop progression if its gate fails. Do not infer implementation permission from this prepared bundle.
```
