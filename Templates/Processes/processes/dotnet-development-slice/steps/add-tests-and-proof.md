# Validate tests and targeted proof

Review child-run tests and run targeted validation without mutating product files.

This step owns the slice validation branch decision:

- Select `slice-accepted` only when child implementation evidence and focused proof satisfy the chosen slice behavior.
- Select `slice-repair-required` when tests are missing or inadequate, build/runtime/browser proof fails, accepted child handoff evidence is missing, or proof does not map to the chosen slice behavior.
- When selecting `slice-repair-required`, write a repair target packet in this step artifact. It must include the exact failed acceptance criteria, failing command or browser metrics, child run id, child step artifact refs, and the smallest proof that would close the defect. If a child `feature-repair-escalation` or `targeted-recheck` artifact exists, read it and quote its concrete defect in your own words instead of summarizing it as generic missing handoff.
- Return a completed process-step outcome with the selected branch outcome. Do not return `Blocked` only because product proof failed and can be repaired by a bounded implementation subprocess.
- Return `Blocked` only when an environment, permission, unavailable tool, or process-contract issue prevents validation or repair routing.

QA does not add or edit product tests in this step.

When visual target ImageAsset ids or media paths are part of the slice scope, include source-target comparison in the proof. The validation artifact must name the target image asset, the delivered screenshot, and the comparison result before selecting `slice-accepted`.
