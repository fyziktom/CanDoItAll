# P12-004 — add canonical trigger registry and Quartz-backed scheduler projection

## Problem
The repo has no application-owned canonical trigger registry and no scheduler seam.

## Why it matters
Plugins that need cron/hourly/daily wakeups need a stable platform boundary rather than one-off timers or pollers.

## Required outcome
Codex must fully implement this subbundle and supply the required evidence and tests.
