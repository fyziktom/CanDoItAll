# Lineage And Trust Invariants

The following behaviors must remain stable:

- Duplicate projections must not create duplicate process artifacts for the same external reference key.
- Lineage must preserve source execution run id, source artifact id when available, source external reference key, projection source kind, and carry-forward lineage when present.
- Required artifact expectations must not be satisfied by missing, stale, unavailable, wrong-producer, placeholder-only, or hash-mismatched evidence.
- Artifact trust status must remain tied to expectation match and completion status.
- Projection must not delete the only useful lineage evidence.
- Response text artifacts must not mask missing required file artifacts unless current rules explicitly allowed it before.
