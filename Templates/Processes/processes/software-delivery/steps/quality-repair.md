# Repair validation findings

Repair concrete defects, missing workflows, failed validation, or proof gaps identified by QA without expanding beyond the approved delivery scope.

## Contract
- Inputs: QA repair-required disposition, reviewed implementation package, and failing proof details.
- Outputs: Repaired change set and validation notes ready for QA recheck.
- Evidence: Changed files or deliverables, repair rationale, rerun validation, and remaining risks.
- Operation target scope: `ExternalProductTargetMutable`

When the QA finding is about runtime behavior, screenshots, browser state, console output, or launch/cleanup evidence, repair the concrete defect and rerun the smallest runtime or browser proof that demonstrates the same failing behavior is fixed. Capture current-run managed artifacts for that proof, including screenshot or browser state evidence and console output when a visible browser workflow is involved. Stop any runtime started only for this repair step before finalizing the artifact.
