# Exit criteria

Phase10 exits only when all of these are true:

1. HG-10-01 through HG-10-05 are satisfied.
2. `scripts/gate_check_phase10.py` reports no hard-gate failures.
3. The exact required test names exist.
4. Runtime validation is attached from a real .NET environment.
5. The final report explains:
   - where stale projection cleanup moved,
   - why that seam is no longer read-reachable,
   - which files changed,
   - which tests prove the new behavior.
