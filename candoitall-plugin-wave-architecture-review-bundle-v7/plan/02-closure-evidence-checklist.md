# Closure evidence checklist

Codex must attach all of the following before claiming the refactor is done:

1. file list of changed canonical model files
2. file list of changed tests
3. output of `python scripts/gate_check_phase7.py --repo .`
4. build/test output from a real .NET environment
5. one short proof note per hard blocker explaining:
   - what changed
   - which tests prove it
   - which code search output proves the forbidden pattern is gone
6. updated ADRs only if the implementation really changed
7. final QA sign-off

## Important rule

For repeated blockers, **ADR text alone is not a valid closure signal**.
