# SB027 Semantic Invariants

- Invariant ID: SB027_INV_001
- Phase: P09 - Live OpenAI process-run proof hardening
- The implementation must prove real source behavior, not report-only claims.
- Verification host/drivers must not mutate process state, transitions, finalizers, retries, claims, workspace, storage, Office/Graph, CRM, or external systems.
- Process Core must remain dependency-clean.
- Any new production signal must include a production behavior artifact matrix in this file and in the manifest.