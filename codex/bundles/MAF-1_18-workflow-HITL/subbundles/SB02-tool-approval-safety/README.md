# SB02 — Agent Tool and Approval Safety on MAF 1.18

## Status

Proven

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

Passed on 2026-08-20.

- Central serial setting: `MafChatClientAgentOptionsFactory` explicitly sets
  `AllowConcurrentInvocation = false`; runtime construction and repair execution both use
  that factory.
- Custom invocation audit: one production `ChatClientAgentOptions` construction site; no
  `FunctionInvokingChatClient`, no `UseProvidedChatClientAsIs = true`, and no production
  bypass. Provider `AllowMultipleToolCalls` support remains independent and unchanged.
- Direct `ToolApprovalAgent` decision: none exists, so no wrapper or iteration cap was
  invented.
- Declaration-only experiment: no active
  `StoreInvocableFunctionCallsForFutureTurns = true`; the experiment remains disabled.
- Order/overlap proof: three new tests exercise the real MAF invocation decorator.
  Streaming and non-streaming calls complete `A → B → C` exactly once with maximum active
  count one. The negative fixture deliberately opts the same path into concurrency and
  observes an active count greater than one.
- Failing-first proof: the renamed 1.18 factory regression failed because the source lacked
  an explicit serial assignment, then passed after the one-line production change.
- Approval/session proof: 12/12 approval round-trip tests passed, covering streaming and
  non-streaming restart, replay, cross-session, unknown response, consecutive requests,
  hosted MCP, and argument binding/tamper protection.
- Usage/telemetry proof: streaming recovery 2/2, workflow usage 9/9, response-assembler
  usage 2/2, provider update pump 4/4, and architecture/runtime safety 52/52 passed. The
  runtime uses OpenTelemetry activities directly and has no application-owned telemetry
  event-serialization wrapper, so the conditional serialization-containment test is N/A.
- Total focused result: 84/84 passed after exact discovery. The upgraded static scanner
  reports no error, warning, or unsafe finding.
- Documentation: `docs/agent-runtime-tool-surface.md` records serial execution, retained
  multiple-call output, approval barriers, and deferred concurrency; documentation
  validation passed for 187 maintained Markdown files.
- Wave A files: central MAF version props, central agent options factory, the renamed/static
  factory regression, the new concurrency policy fixture, maintained tool-surface docs, and
  bundle execution state. No workflow HITL implementation is mixed into this diff.
- Blockers/deviations: none. The unit gate used Debug to avoid the pre-existing Release Web
  output lock; the production MAF project passed in Release with zero warnings/errors.
