# P12-005 — add durable internal message plane with retries and dead-letter

## Problem
The repo still has no application-owned durable internal message plane for commands/events/wakeups with fan-out, retries, and restart-safe delivery.

## Why it matters
Cross-plugin orchestration and trigger wakeups need a durable runtime transport rather than in-memory or page-local logic.

## Required outcome
Codex must fully implement this subbundle and supply the required evidence and tests.
