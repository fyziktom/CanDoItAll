# Current State

- Probe sessions persist only actor and policy profile, then reconstruct ask-time policy as low-risk project access with restricted content disabled.
- Probe ask requests do not forward projection collection, projection profile, or embedding profile, so vector recall diagnostics are incomplete and Qdrant-backed recall is hard to validate from probes.
- Database transfer has handlers for projects, process definitions, providers, and agents, but no handler for cognitive-memory source manifests/items/evidence/external ingestion records.
- `/api/cognitive-memory/status` reports the active profile but omits profile resolution source, runtime-lock state, parsed database host/name/port, projection defaults, and static host diagnostics.
- Dream aggregate titles use the first cluster key and canonical text mostly mirrors memory summaries; validation needs stronger primary-key selection and concrete source-support context without copying restricted text.
- Scheduled automation performs at most one consolidation run and generates timestamp idempotency keys, which makes longer validation cycles hard to resume or audit.
