# Repeat-offender summary

These blockers were already called out in earlier bundles and are still present now:

- persisted Workbench parallel truth
- overloaded node carrier
- fragmented node-kind semantics
- in-place reclassification without history
- closed enum/switch connector seam
- missing hard architecture closure checks

## What v7 changes to force closure

This bundle intentionally raises the bar:

1. **Hard exit gates**  
   The branch must pass explicit architectural gates before plugin work is allowed.

2. **Forbidden-pattern checks**  
   The bundle includes a repo-level static script that fails when the repeated patterns are still present.

3. **Per-item closure proof**  
   Each repeated blocker requires file-level proof, required tests, and guardrail evidence.

4. **No ADR-only closure**  
   Design notes are necessary but not sufficient. Repeated blockers require code-level closure.

5. **Senior QA stop condition**  
   If hard gates still fail, the bundle must remain open regardless of how many local tests pass.
