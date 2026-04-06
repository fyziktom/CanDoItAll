# P10-004 — Manifest-driven plugin editor regression proof is still too weak for the next plugin wave

Severity: **High**  
Gate: **HG-10-05**  
Module area: **Resources / Workspace**

## Problem
The shared editor exists, but the current proof still exercises only today's built-in manifests. That is not enough for a large upcoming plugin wave.

## Required architectural end-state
Add unknown-provider and unknown-resource test plugins/manifests that use all shared field types and prove:
- render,
- edit,
- save,
- load,
- round-trip

without page-specific code changes.
