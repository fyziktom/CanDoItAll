# SB02 — Agent Tool and Approval Safety on MAF 1.18

## Status

Prepared

## Outcome

Close the MAF upgrade wave by making serial tool execution an explicit CanDoItAll policy and proving that existing approval/session, usage, streaming, and telemetry behavior remains safe on 1.18.

## Owned requirements

RQ-005 through RQ-012, the behavioral part of RQ-043, and Wave A independence in RQ-044.

## Non-goals

- enabling concurrent tool execution for any tool;
- introducing a configurable concurrency scheduler;
- disabling a provider's ability to return multiple tool calls;
- enabling declaration-only tool storage;
- creating a `ToolApprovalAgent` wrapper when the app does not use one;
- workflow HITL implementation;
- unrelated provider refactoring.

## Prerequisites

SB01 passed with a coherent 1.18 package graph.

## Reopen triggers

- IK-03 direct `ToolApprovalAgent` usage;
- IK-04 custom/provided invocation client bypass;
- IK-05 any true concurrent setting;
- IK-06 mixed declared/executable tools are an active requirement;
- approval/session tests regress;
- usage/telemetry behavior changes beyond a local adaptation.

## Exact sources and discovery

Inspect all results of:

```bash
rg -n "new ChatClientAgentOptions|AllowConcurrentInvocation|FunctionInvokingChatClient|UseProvidedChatClientAsIs|AsAIAgent"
rg -n "ToolApprovalAgent|ToolApprovalAgentOptions|MaxAutoApprovalIterations"
rg -n "StoreInvocableFunctionCallsForFutureTurns"
```

Primary surfaces:

- `MafChatClientAgentOptionsFactory.cs`
- `MafProviderAgentFactory.cs`
- `MafRuntimeAgentFactory.cs`
- `MafStreamingTurnExecutor.cs`
- approval continuation/session serialization code
- usage compatibility projection and workflow/agent telemetry code
- `MafApprovalSessionRoundTripTests.cs`
- streaming recovery tests
- usage analytics tests.

## Implementation boundary

1. Set `AllowConcurrentInvocation = false` in the central 1.18 options factory.
2. Ensure every bypassing application-owned options path uses the same policy.
3. Inspect custom/provided `FunctionInvokingChatClient` instances:
   - leave serial or explicitly set false;
   - stop if an existing true value has a documented dependency.
4. Add a meaningful scripted multi-tool order/overlap test.
5. Preserve provider multiple-call support while proving serial execution.
6. Rerun and repair only actual 1.18 approval/session regressions.
7. If direct `ToolApprovalAgent` use exists:
   - configure an application-owned finite cap based on current workflow needs;
   - test cap behavior;
   - do not change upstream default without a reason.
8. Keep `StoreInvocableFunctionCallsForFutureTurns` false.
9. Add a telemetry serialization containment regression when CanDoItAll wraps that event path.
10. Update docs with the serial policy and deferred concurrency capability.

## Acceptance criteria

- all application-owned agent construction is serial by default;
- no active source sets `AllowConcurrentInvocation = true`;
- no active source enables declaration-only tool storage;
- scripted multiple calls complete in deterministic order;
- max simultaneous invocation count is one;
- the probe proves it would detect overlap;
- provider multiple-tool output is not discarded merely to enforce serial execution;
- pending approval is a dependency barrier;
- streaming and non-streaming approval round trips pass;
- replay, cross-session, unknown, consecutive, hosted MCP, and tampered argument cases pass;
- usage aggregation remains correct;
- telemetry serialization failure does not fail the governed operation where applicable;
- Wave A can be reviewed without any workflow HITL implementation.

## Proof tier

Behavioral

## Focused validation

Static guard:

```bash
python <bundle>/scripts/check_maf_upgrade.py <repo-root> --mode upgraded
```

Unit filters:

- `MafToolInvocationConcurrencyPolicyTests`
- `MafApprovalSessionRoundTripTests`
- `MafStreamingTurnExecutorRecoveryPolicyTests`
- relevant usage/telemetry classes from SB00.

Expected discovery:

- retained classes: at least SB00 count;
- new concurrency policy class: exact count recorded after implementation and greater than zero;
- every run records max active invocation count assertion.

Do not run full solution or UI tests.

## Invalidation keys

IK-03, IK-04, IK-05, IK-06, IK-16, IK-17.

## Broad-gate decision

No broad gate. Wave A closes on affected builds plus focused behavioral proof.

## Closure record

Not executed.

Record:

- central serial setting:
- custom invocation audit:
- direct ToolApprovalAgent decision:
- declaration-only experiment:
- order/overlap test:
- approval/session tests/counts:
- usage/telemetry tests:
- Wave A files/diff:
- blockers/deviations:
