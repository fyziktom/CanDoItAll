# Dispatch Loop And Claim Lifecycle Boundary

## Why this boundary comes before Process Core

A Process Core split should not inherit EF-backed claim operations, route sequencing and exception closure directly from `Dispatch.cs`. The next safe step is to isolate these concerns while staying inside the Processes module.

## Proposed internal boundaries

### ProcessDispatchClaimStore

Owns EF persistence for:

- Try claim step dispatch.
- Renew claim lease.
- Check claim still held.
- Release claim.

### ProcessDispatchClaimCoordinator

Owns claim-store + option policy + logging behavior. This should be the only caller of claim-store methods.

### ProcessDispatchHeartbeatCoordinator

Wraps `ProcessDispatchLeaseHeartbeat` start/renew/claim-lost behavior and makes the cancellation token lifecycle explicit.

### ProcessDispatchRouteExecutionContext

Holds run id, trigger, claim, candidate, route snapshot, renewal callback and cancellation token for the current claimed step.

### ProcessDispatchRoutePipeline

Executes route stages in current order:

1. fresh recovery skip;
2. database requirement;
3. upstream artifact materialization;
4. stranded artifact recovery;
5. subprocess;
6. start transition;
7. workflow;
8. direct-agent execution;
9. competing execution guard;
10. run closed guard;
11. finalizer and transition.

### ProcessDispatchExceptionClosureCoordinator

Owns exception classification and failure transition request construction. It must preserve claim-lost and cancellation semantics exactly.

## Driver readiness

This bundle does not create drivers. It only documents future concepts:

- `DispatchRouteEvidence`;
- `ClaimLeaseEvidence`;
- `AutomationRouteOutcome`;
- `AutomationSafetyGuardEvidence`;
- `DispatchFailureEvidence`.

These are documentation-only terms for a later driver readiness map.
