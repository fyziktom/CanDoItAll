# Assess UI screenshot evidence disposition

Read the screenshot target manifest, storage receipts, current-run browser evidence, and current-run image inspection and analysis receipts when the target has a UI. Inspect the delivered screenshot during this execution before choosing a UI disposition; an upstream description, image name, or project-structure asset id is not enough.

Select `visual-accepted` when UI evidence satisfies the declared route and visual target. Select `visual-defect-observed` when current-run evidence demonstrates a blank, error, wrong-route, scaffold, unstyled, non-interactive, or source-target-mismatched UI. This is a completed diagnostic outcome, not `Blocked`: record the route, screenshot, observed state, comparison method, and exact receipt refs for parent QA. Select `no-ui-evidence-recorded` only when the applicability manifest explicitly classifies the target as having no browser-visible UI and cites its supporting evidence. That is a completed evidence disposition, not an acceptance of the product and not a replacement for the parent’s non-browser QA checks.

Return `Blocked` only when a concrete launch, browser, image-analysis, access, policy, or provider failure prevents the applicable grounded disposition. A missing browser, image tool, or storage tool for a UI target is not no-UI evidence. Do not change the product and do not direct implementation work from this evidence-assessment step.

## Contract

- Inputs: Screenshot target manifest and durable screenshot storage receipts.
- Outputs: A decision artifact with exactly `visual-accepted`, `visual-defect-observed`, or `no-ui-evidence-recorded`.
- Evidence: Current-run screenshot refs, route, accepted or diagnostic asset ids, image inspection and analysis receipts, and source-target comparison for UI dispositions; explicit no-UI classification evidence for `no-ui-evidence-recorded`.
- Operation target scope: `ExternalProductTargetReadOnly`
