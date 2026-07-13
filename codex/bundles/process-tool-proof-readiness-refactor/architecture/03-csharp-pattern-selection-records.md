# C# Pattern Selection Records

## PSR-1 Contract Compiler

- Pattern: compiler plus immutable value objects.
- Applies to: process template and driver data converted into `ProcessStepCapabilityContract`.
- Reason: process definitions are data-heavy and need deterministic, cacheable conversion into runtime contracts.
- Rejected alternative: prompt-only instructions, because they cannot drive readiness or receipt gates.

## PSR-2 Readiness Evaluator

- Pattern: strategy-free application service with typed result records.
- Applies to: HR matching and launch/dispatch readiness checks.
- Reason: readiness is a deterministic comparison between selected agent/runtime capabilities and step contract.
- Rejected alternative: embedding readiness rules in Blazor components, because it duplicates logic and weakens testability.

## PSR-3 Receipt Gate

- Pattern: policy evaluator with explicit allow/block/escalate result.
- Applies to: post-attempt finalization.
- Reason: a step outcome must be checked against recorded receipts before acceptance.
- Rejected alternative: finalizer prompt repair, because missing proof is a state condition, not a language-only problem.

## PSR-4 Manager Fallback Planner

- Pattern: chain of focused process fallback strategies contributed by drivers.
- Applies to: proof redispatch, reassignment, driver recovery, and NeedsAttention decisions.
- Reason: different domains can recover differently while sharing a common diagnostic model.
- Rejected alternative: one monolithic recovery service, because it would mix artifact recovery, tool access, and domain-specific proof capture.

## PSR-5 Metadata Adapter

- Pattern: adapter at module boundary.
- Applies to: translating process contract to `AgentRuntimeCapabilityScopeOverride` and required receipt metadata.
- Reason: process runtime and MAF use different language and must stay decoupled.
- Rejected alternative: direct MAF references in process templates or process contracts.
