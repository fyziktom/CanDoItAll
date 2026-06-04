# SB07 Semantic Invariants

1. Workflow editor UI must consume executor descriptor metadata and display adapters for availability, side effects, approval, deterministic preview, and retry-safety state.

2. UI must not duplicate workflow retry-safety policy. Retry-safety display remains derived from `WorkflowExecutorSideEffectPolicy.IsRetryPolicySafe`.

3. Unavailable executors must remain visible to users. Selector labels may mark them unavailable, but catalog UI must not hide them.

4. Capability proof status labels and tones must be centralized in a display adapter so component surfaces do not drift independently.

5. Provider profile status labels and tooltips must be centralized in a display adapter so tree and panel UI share enabled/health semantics.

6. Live process observation must carry provider usage state alongside token/tool/cost metrics so UI can distinguish known usage from incomplete usage.

7. Process UI must not show precise actual cost when provider usage is incomplete or inferred only from legacy metrics.

8. Process cost graphs must omit the actual-cost series when exact usage is not known.

9. Browser proof must include both workflow editor and process observability routes, with desktop and narrow viewport captures when layout changes.

10. Visual screenshots must be inspected for readability. A passing DOM snapshot is not sufficient when text or badges can collapse visually.

11. UI hardening must remain a display-layer change. Runtime behavior may only change where DTO/display state is needed to expose canonical process or provider usage state.

12. Component tests must cover canonical display adapters directly so status and cost semantics fail fast on drift.
