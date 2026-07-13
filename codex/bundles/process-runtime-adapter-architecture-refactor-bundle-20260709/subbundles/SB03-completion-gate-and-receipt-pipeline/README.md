# SB03 - Completion Gate And Receipt Pipeline

## Status

- Status: `Completed`

## Objective

Extract branch-aware completion gate and required receipt behavior from the adapter.

## Covered Inputs

- GPTPro branch-aware receipt gate findings.
- GPTPro completion gate short-circuit finding.
- User requirement to avoid runtime/dispatcher domain leaks.

## Prerequisites

- SB01 characterization complete.
- SB02 contract placement complete.

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessCompletionGateEvaluator.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/ProcessRequiredToolReceiptGate.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.CompletionGates.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.ProductCompletionReceipts.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessExecutionAdapter.CompletionIssueResults.cs`

## Dependency Impact

- May move generic gate/receipt contracts into runtime/driver abstractions.
- Requires CodeAnalytics if project references change.

## Validation Depth

- Direct unit tests.
- Negative tests.
- Branch applicability tests.
- Dedupe tests.
- Adapter delegation proof.

## Do Not Do

- Do not hardcode branch keys in generic code.
- Do not weaken required receipts.
- Do not keep duplicate moved logic in adapter.

## Acceptance Checklist

- [ ] Gate pipeline aggregates issues.
- [ ] Receipt matcher is branch-aware.
- [ ] Branch route decisions are metadata driven.
- [ ] Adapter delegates to pipeline.
- [ ] Old adapter methods removed.

## Proof Required

- Proof manifest with direct test transcript.
- Negative tests.
- Source assertions.
- No-new-partial proof.

## Browser Validation Logging

- Not applicable for unit extraction.
- Final process E2E is in SB07.

## Progression Gate

- SB05 and SB06 may consume the new pipeline only after direct tests pass.

## Suggested Agent Prompt

Implement SB03 only. Extract gate and receipt behavior into top-level services and delete moved adapter code.

## Goal

Extract completion gate evaluation, required receipt matching, branch applicability, duplicate product/process receipt handling, and completion issue routing from the adapter partial cluster into focused top-level services.

## Scope

- `ProcessCompletionGateEvaluator`
- `ProcessRequiredToolReceiptGate`
- Product/process receipt rule parsing and dedupe
- Branch-aware receipt applicability
- Completion issue ordering and route decision
- Runtime gate findings evidence contract

## Implementation Steps

1. Use SB01 characterization tests as baseline.
2. Create top-level gate context/result/issue records in the correct project from SB02.
3. Replace delegate list gates with named gate classes or narrowly scoped functions owned by `ProcessCompletionGatePipeline`.
4. Extract required receipt matching into `IRequiredToolReceiptMatcher`.
5. Extract receipt expectation resolution into `IProcessReceiptExpectationResolver`.
6. Add branch applicability for both legacy and structured receipt rules.
7. Add product/process receipt dedupe by typed expectation identity.
8. Extract completion issue routing into `IProcessCompletionIssueRouter`.
9. Update adapter to call the pipeline and router.
10. Delete moved gate/receipt/routing methods from adapter partial files.
11. Add direct unit tests and negative tests.
12. Run targeted tests, build, and CodeAnalytics if project references changed.

## C# Architecture Impact

This subbundle removes the root branch/receipt/gate failure class from adapter-private logic and makes it reusable/testable.

## Boundary Ownership

Generic gate and receipt algorithms should live in `Processes.Runtime` or `Drivers.Abstractions` if consumed by drivers. MAF-specific receipt model adaptation should stay in `Modules.Processes`.

Domain-specific receipt expectations are data from templates/drivers, not hardcoded in the pipeline.

## Dependency Direction

The pipeline must not reference concrete domain driver implementations. If MAF receipt types block placement in runtime, introduce a small adapter-local normalized receipt record.

## Pattern Decision

Use Chain of Responsibility for gates. Use Strategy for domain-specific receipt expectation/classification if required.

## Testability Contract

Required direct tests:

- Multiple gate issues are aggregated.
- Primary issue ordering is stable.
- Branch-inapplicable receipt rule is skipped.
- Product/process duplicate receipt rule is deduped.
- Missing required receipt records missing expectation details.
- Failed receipt can satisfy a defect-proof expectation when configured.
- Branch-routable deterministic issue produces route decision instead of manager escalation.

## Partial Class Policy

Delete or shrink:

- `AgentFrameworkProcessExecutionAdapter.CompletionGates.cs`
- Relevant methods in `ProductCompletionReceipts.cs`
- Relevant methods in `CompletionIssueResults.cs`
- Relevant route/helper methods in `ResultConversion.cs`

No new partials.

## Architecture Proof Required

- Source assertion showing moved methods no longer live in adapter partial files.
- Direct unit test transcript.
- Negative test transcript.
- Adapter production path calls extracted pipeline.
- Domain-boundary assertion for forbidden terms in generic pipeline code.
- No-new-partial proof.
