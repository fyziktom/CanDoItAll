# P10-002 — Stale projection repair must move to an explicit maintenance boundary

Severity: **High**  
Gate: **HG-10-02**  
Module area: **Workbench**

## Problem
The cleanup responsibility is still real, but it is currently hiding in the read seam. That makes repair timing implicit and unsafe.

## Required architectural end-state
Move stale system-managed projection retirement and orphan layout cleanup to an explicit repair seam that is:
- deliberate,
- idempotent,
- independently testable,
- not reachable from structure reads.
