# Current Dispatch Route Map

Codex must fill this with live source details in SB02 before production movement.

Initial observed route sequence from `DispatchAsync`:

1. Load candidate headers.
2. Acquire in-memory step guard.
3. Try durable step dispatch claim.
4. Start heartbeat and renew lease callback.
5. Hydrate candidate.
6. Skip fresh recovery redispatch.
7. Block for database requirement if needed.
8. Request missing upstream artifact materialization.
9. Try stranded missing completion artifact recovery.
10. Route subprocess step.
11. Start step as InProgress.
12. Try workflow execution.
13. Execute direct agent until settled.
14. Resolve competing active execution.
15. Check run closed to automation.
16. Finalize completion.
17. Apply finalized transition.
18. Handle claim lost/cancellation/failure.
