# Priority findings

## Critical

1. **HITL correctness:** Do not pre-block any workflow just because a `HumanInput` node exists. Human input must be reached by graph execution or modelled through MAF request/response.
2. **Approval runtime:** Approval-required executors are now correctly blocked without a gate, but product approval/resume is not implemented.
3. **Backend honesty:** DurableTask/AzureFunctions are shown as conceptual backends but are not registered production backends.
4. **Plugin observer composition:** Null/plugin observer selection is currently vulnerable to module registration order.

## High

5. **MAF 1.8 upgrade:** The repo should not keep hardening runtime behavior on a stale MAF baseline without at least one staged upgrade attempt.
6. **Event fidelity:** Persist typed event payloads, executor IDs, node IDs, request IDs, and redacted exception data rather than generic `ToString()`.
7. **Checkpoint trust boundary:** Add checkpoint abstraction and storage policy before durable/resume features are exposed.
8. **Artifact payload policy:** Apply inline/artifact splitting to all event/output paths, not only selected configured file paths.

## Medium

9. **Descriptor/schema consistency:** Move away from `{ "type": "object" }` placeholder JSON schemas where user-facing setup or import/export depends on executor settings.
10. **Proof cleanup:** The previous bundle contains huge source-scan/build proof transcripts. Future proof should be smaller and more targeted.
11. **CI status:** No combined CI statuses were observed through the connector for the reviewed head. Add or document a repeatable CI gate if the repo is expected to enforce workflow runtime quality.
