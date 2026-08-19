# Deterministic process-plan hash classification

## Goal

Remove time as an algorithm-authority signal while preserving exact V1/V2 hashes and fail-closed execution.

## Entry

Read the root execution prompt, findings, requirements, invariants and
validation strategy. Reconfirm the exact repository anchor before editing.

## Tasks

1. Introduce one structured payload-shape classifier shared conceptually by runtime mapping and migration tests.
2. Classify no-V2-shape rows as LegacyV1 regardless of CreatedAtUtc.
3. Classify complete valid V2 shape as HostCapabilitiesV2.
4. Classify partial, malformed or conflicting shapes as Unknown.
5. Keep LegacyV1 as NeedsRecompile with HostCapabilitiesWereNotSealed.
6. Add a corrective idempotent migration for databases that already applied AddProcessPlanHashVersioning.
7. Use PostgreSQL jsonb structure checks rather than LIKE and wall-clock cutoffs.
8. Prove existing V1 and V2 hash fixtures remain byte-for-byte stable.

## Rules

- Preserve unrelated changes.
- Use focused failing-first tests.
- Keep source comments in English.
- Do not push or merge.
- Do not weaken a validator to make evidence pass.
