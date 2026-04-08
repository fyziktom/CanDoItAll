# P11-003 — add durable internal message bus, outbox/inbox, and subscriptions

## Problem
The repo still has no durable internal pub-sub/message runtime. Without it, trigger wakeups, plugin-to-plugin events, approvals, retries, and cross-module orchestration cannot be handled consistently.

## Why it matters
This is a platform-level requirement for the next plugin wave.
Without it, each plugin will tend to invent its own orchestration mechanics and the platform will fragment.

## Required outcome
Codex must fully implement this subbundle and supply the required evidence and tests.
