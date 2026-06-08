# Next Architecture Roadmap

## Completed Milestones
- Deterministic Process Core exists.
- Driver abstractions package exists.
- Transcript verifier alpha exists.
- Runtime evidence verifier alpha exists.
- Process module has read-only adapters.

## Current Bundle Milestones
1. Repair/triage full-unit debt.
2. Harden and decompose current verifier/adapters.
3. Add a controlled multi-domain verification gateway without runtime registration.
4. Add read-only Office and business-analysis alpha verifiers over supplied evidence only.
5. Add artifact/projection/validation evidence verifier.
6. Create shared verification test harness.
7. Stabilize API compatibility and release docs.
8. Close with broad smoke and red-team proof.

## Future Milestones After This Bundle
- Only after this bundle: consider a production verification host registration with explicit allow-list.
- Later: manager-visible read-only verification results.
- Later: scheduler/workflow integration only after ownership/audit/storage policy is designed.
- Much later: execution-capable drivers after sandbox, command allowlist, timeout, output hashing, secret masking, approval, audit persistence, and lifecycle ownership exist.
