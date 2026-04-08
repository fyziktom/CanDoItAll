# P11-002 — add canonical trigger registry and Quartz-backed scheduler projection

## Problem
The repo has no canonical trigger registry and no scheduler seam. Plugins that need hourly/daily/cron wakeups would otherwise each invent their own timers or pollers.

## Why it matters
This is a platform-level requirement for the next plugin wave.
Without it, each plugin will tend to invent its own orchestration mechanics and the platform will fragment.

## Required outcome
Codex must fully implement this subbundle and supply the required evidence and tests.
