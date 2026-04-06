# CanDoItAll plugin-wave architecture review bundle v13

This bundle captures a detailed manual review after Codex claimed bundle12 was complete.

## Verdict

- Phase10 / phase11 / phase12 gates pass on the current repo.
- The repo is **not yet execution-grade for the upcoming plugin wave**.
- Bundle12 is therefore **functionally ahead of bundle11/12 gate expectations, but still architecturally incomplete** in several hidden runtime-hardening areas that the current gates do not detect.

## Hidden blockers found in manual review

1. `AutomationRuntimeOptions` are registered but **not bound from production configuration**, so MQTT, poll intervals, and runtime tuning cannot be configured without code changes.
2. Durable idempotency for internal envelopes, ingress envelopes, and connector outbox commands is still implemented as **read-then-insert** without atomic conflict handling.
3. The runtime workers still use **single-instance / no-claim acquisition**, including full-table materialization in hot paths.
4. Hosted worker loops still have **no iteration-level exception isolation**, so an unexpected exception exits the loop.
5. Production code still exposes the **legacy background-job queue seam**, and the bridge only observes/logs queue items instead of closing the seam.

## Contents

- detailed review notes,
- current gate outputs,
- a new `gate_check_phase13.py`,
- execution-grade subbundles `P13-001` through `P13-005`.
