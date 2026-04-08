# P11-001 — operational messages must not be canonical Workbench nodes

## Problem
The platform still lacks an explicit execution-plane distinction between user-visible business artifacts and operational orchestration envelopes. It also still consumes automation signals through a singular provider shape that is not open-world enough for plugins.

## Why it matters
This is a platform-level requirement for the next plugin wave.
Without it, each plugin will tend to invent its own orchestration mechanics and the platform will fragment.

## Required outcome
Codex must fully implement this subbundle and supply the required evidence and tests.
