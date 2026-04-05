# Senior QA review

## Verdict

Reject the branch as the base for the large connector/plugin wave.

## Why

The same structural blockers are still visible in code, not just in theory:
- persisted projection sync into canonical Workbench storage
- overloaded node carrier
- missing node-kind registry
- in-place reclassification without history
- editable hierarchy dual-write
- metadata foreign-id leakage
- closed connector seam
- missing guardrail enforcement

## Required action

Proceed with the phase7 refactor bundle and do not start the large connector/plugin wave until the hard gates pass.
