# Gap analysis toward generic process driver runtime host

## Ready enough now
- Generic Process Core remains dependency-light and deterministic.
- Process runtime can run deterministic and live OpenAI process-run paths.
- Verification-only runtime host beta exists.
- Explicit lane registry/selector exists without fallback/reflection.
- Manager facade/readback exists.
- Dry-run execution planning exists as module-local model.

## Still missing before generic runtime host can be trusted
1. Stable runtime host contracts outside accidental module-local model sprawl.
2. Durable audit lifecycle with EF configuration/index/readback/retention proof.
3. Host lifecycle status, emergency disable, health and readiness consumable by operator/API/UI.
4. Real scheduler/workflow read-only verification job execution path.
5. Dry-run sandbox plans tied to future execution-capable gates and audit records.
6. Strong separation between verification-only domain drivers and future effectful domain drivers.
7. A concrete no-effect execution request model that can later be upgraded safely.
8. Regression coverage showing Process Core stays generic and no domain terms leak into it.

## Explicit not-yet-approved future
Execution-capable drivers remain blocked until sandbox, allowlist, authorization, approval/revocation, emergency stop, audit, lifecycle, cancellation, timeout, failure handoff, red-team proof and operator visibility are all source-backed.
