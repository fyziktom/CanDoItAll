# M01 — Backward-compatible persisted process plans and capability migration

## Mission

Prevent old persisted plans from failing hash verification or silently becoming capability-free.

## Entry

Follow the preceding subbundle/checkpoint and verify its GO decision. Preserve the exact anchor and invalidation ledger.

## Handoff

Complete `templates/subbundle-result.md`, update the invalidation ledger, and name the next eligible subbundle.
