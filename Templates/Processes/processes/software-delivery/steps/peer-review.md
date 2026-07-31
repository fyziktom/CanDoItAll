# Complete peer review and integration readiness

Review the change set against the approved design, integration consequences, and release assumptions.

This review runs before browser, screenshot, runtime, and QA evidence is collected. Downstream QA owns browser and runtime evidence. Judge the source, design alignment, build/test evidence, and implementation handoff that are available at this stage; do not require downstream browser, console, computed-style, changed-state, screenshot, runtime, release, or QA receipts. Record suspected visual/runtime acceptance gaps and repairable product or design discrepancies as explicit QA repair findings so the QA branch can validate them and route confirmed defects through `quality-repair`.

Return Completed when the independent review is finished and the package is safe to enter downstream validation, including when downstream proof is still pending or a review finding is repairable by the existing QA route. Return `Blocked` only for a concrete safety or execution boundary: a policy, access, tool, environment, or contradictory-contract condition that makes continued automated validation unsafe or impossible. Missing downstream evidence and repairable product findings are not blockers and must not be escalated from this stage.

Before returning Completed, read the required upstream managed artifact refs and write or update `artifacts/process-runs/<current-process-run-id>/steps/peer-review.md`. Use managed process refs, project-structure node ids, and current-run tool receipt refs as evidence. Do not put native absolute product paths, scoped storage paths, managed-files paths, project-media paths, tool-runs paths, SourceDocLink values, or ungrounded external-target child paths in the artifact body, reason, summary, next actions, or final `evidenceRefs`. If a review finding needs to discuss a product file, describe the component or behavior without a path-like string, or first create a current-run read/validation receipt that grounds the exact ref and cite that receipt. Final `evidenceRefs` must include the peer-review artifact ref plus exact current-run receipt refs for any validation tools run.

## Contract
- Inputs: Implementation package, architecture decision record, and changed-surface inventory.
- Outputs: Peer-reviewed change set with explicit residual risk and follow-up items.
- Evidence: Review notes, unresolved issues list, approved follow-up actions, the current-run peer-review managed artifact ref, and exact current-run validation/read receipt refs when tools run.
- Operation target scope: `ExternalProductTargetReadOnly`
