# ADR-006: Narrow runtime ports and a thin MAF adapter

- Status: Accepted for implementation
- Date: 2026-08-06

## Context

`IAgentRuntime` combines execution, continuation, provider diagnostics, and provider model administration. `MafAgentRuntime`, its factory, and the capability composer also perform construction, policy, session, finalizer, and recovery work.

## Decision

Create an SDK-free runtime contracts project, preferably:

`CanDoItAll.AgentFramework.Runtime.Abstractions`

Expose narrow ports:

- `IAgentExecutionRuntime`
- `IAgentContinuationRuntime`
- `IHostedAgentFactory` only for hosting scenarios that truly require it
- `IProviderDiagnosticsRuntime`
- `IProviderModelAdministrationRuntime`

The MAF assembly implements these agent/runtime ports through cohesive adapters. The separate lightweight LLM boundary is defined by ADR-010 and is implemented over the provider runtime rather than the MAF agent runtime. MAF owns only:

- mapping runtime-neutral requests to MAF SDK calls,
- provider/session/tool protocol execution,
- MAF-native streaming and event mapping,
- MAF runtime-state serialization,
- generic finalizer protocol mechanics.

It does not own product authority, process outcomes, managed artifact paths, product provider-selection policy, or application completion gates.

The old `IAgentRuntime`/`MafAgentRuntime` may exist temporarily as a delegating compatibility facade. It must have a removal subbundle, no new callers, and source assertions that production paths use the narrow ports.

## Consequences

- Workflow LLM calls migrate through the separate provider-backed lightweight port defined by ADR-010 and no longer require a temporary agent/session.
- Provider diagnostics can be tested and hosted separately.
- Application orchestration can support another runtime adapter later.
- Constructor dependencies become explicit.

## Proof

- Runtime contracts contain no `Microsoft.Agents.*`, `Microsoft.Extensions.AI`, OpenAI, MAF workflow, product module, or UI types.
- Core callers compile against narrow ports.
- MAF adapter components have direct unit tests.
- The broad facade has no production caller before final deletion.
