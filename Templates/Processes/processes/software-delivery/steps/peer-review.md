# Complete peer review and integration readiness

Review the change set against the approved design, integration consequences, and release assumptions.

Before returning Completed, read the required upstream managed artifact refs and write or update `artifacts/process-runs/<current-process-run-id>/steps/peer-review.md`. Use managed process refs, project-structure node ids, and current-run tool receipt refs as evidence. Do not put native absolute product paths, scoped storage paths, managed-files paths, project-media paths, tool-runs paths, SourceDocLink values, or ungrounded external-target child paths in the artifact body, reason, summary, next actions, or final `evidenceRefs`. If a review finding needs to discuss a product file, describe the component or behavior without a path-like string, or first create a current-run read/validation receipt that grounds the exact ref and cite that receipt. Final `evidenceRefs` must include the peer-review artifact ref plus exact current-run receipt refs for any validation tools run.

## Contract
- Inputs: Implementation package, architecture decision record, and changed-surface inventory.
- Outputs: Peer-reviewed change set with explicit residual risk and follow-up items.
- Evidence: Review notes, unresolved issues list, approved follow-up actions, the current-run peer-review managed artifact ref, and exact current-run validation/read receipt refs when tools run.
- Operation target scope: `ExternalProductTargetReadOnly`
