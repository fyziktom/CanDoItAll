# P11-006 — add execution policy, observability, and optional MQTT telemetry bridge

## Problem
A plugin runtime needs operator visibility and execution control before agent-like plugins are added. MQTT may help later, but it must remain optional.

## Why it matters
This is a platform-level requirement for the next plugin wave.
Without it, each plugin will tend to invent its own orchestration mechanics and the platform will fragment.

## Required outcome
Codex must fully implement this subbundle and supply the required evidence and tests.
