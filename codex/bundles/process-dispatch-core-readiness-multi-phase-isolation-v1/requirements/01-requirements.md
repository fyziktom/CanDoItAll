# Requirements

## Functional preservation

- REQ-001 Preserve all current process dispatch behavior.
- REQ-002 Preserve route order and terminal behavior.
- REQ-003 Preserve claim acquisition, renew, held-check, heartbeat, claim-lost, and release semantics.
- REQ-004 Preserve workflow route behavior.
- REQ-005 Preserve subprocess route behavior and subprocess artifact projection.
- REQ-006 Preserve direct-agent execution, competing execution guard, run-closed guard, finalizer transition, and failure closure behavior.
- REQ-007 Preserve upstream artifact materialization and database requirement blocking behavior.
- REQ-008 Preserve artifact projection/finalizer/validation interactions.

## Architecture requirements

- REQ-009 Do not create Process Core in this bundle.
- REQ-010 Do not create production driver APIs.
- REQ-011 Move from dispatcher-centered adapters toward module-local services and models.
- REQ-012 Reduce wrapper-only dispatcher forwarding.
- REQ-013 Make side effects explicit: EF writes, transition writes, storage/file IO, agent save, process service calls, finalizer calls.
- REQ-014 Add architecture tests/source scans preventing backsliding.
- REQ-015 Prepare a final Core-readiness decision matrix but do not execute Core split.

## Proof requirements

- REQ-016 Every subbundle must have an execution-report row.
- REQ-017 Every phase gate must include build/test/source-scan proof.
- REQ-018 Browser validation is N/A unless UI is accidentally touched; if UI is touched, stop and report scope drift.
- REQ-019 No small/medium/mobile validation.
- REQ-020 The final bundle closure must include a red-team review and next-cutline decision.
