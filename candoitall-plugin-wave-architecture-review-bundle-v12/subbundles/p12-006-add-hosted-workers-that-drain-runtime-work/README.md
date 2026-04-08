# P12-006 — add hosted workers that drain runtime work

## Problem
The repo can persist some pending work, but it still has no active runtime workers that drain it automatically.

## Why it matters
The platform needs actual runtime execution, not just persisted pending state and manual admin calls.

## Required outcome
Codex must fully implement this subbundle and supply the required evidence and tests.
