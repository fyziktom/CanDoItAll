# P12-003 — add operational execution plane and multi-source automation signals

## Problem
The repo still has no explicit execution-plane distinction between operational envelopes and Workbench nodes, and the automation workspace still consumes a singular signal provider.

## Why it matters
Plugins need a shared execution-plane model and an open-world signal aggregation seam before agent-like behavior can scale safely.

## Required outcome
Codex must fully implement this subbundle and supply the required evidence and tests.
