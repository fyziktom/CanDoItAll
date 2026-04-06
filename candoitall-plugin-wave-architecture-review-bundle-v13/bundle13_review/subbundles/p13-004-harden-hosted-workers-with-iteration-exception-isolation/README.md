# P13-004 — Harden hosted workers with iteration exception isolation

Current worker loops have no iteration-level resilience wrapper. One unexpected exception can stop the loop and leave the runtime plane undrained.

## Status

Completed.
