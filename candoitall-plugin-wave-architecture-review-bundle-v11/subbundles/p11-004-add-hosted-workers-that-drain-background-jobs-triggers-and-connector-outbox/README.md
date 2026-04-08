# P11-004 — add hosted workers that drain background jobs, triggers, and connector outbox

## Problem
The current repo can persist pending work, but it still lacks active runtime workers that drain it automatically.

## Why it matters
This is a platform-level requirement for the next plugin wave.
Without it, each plugin will tend to invent its own orchestration mechanics and the platform will fragment.

## Required outcome
Codex must fully implement this subbundle and supply the required evidence and tests.
