# Validate tests and targeted proof

Review child-run tests and run targeted validation without mutating product files.

This step owns the slice validation branch decision:

- Select `slice-accepted` only when child implementation evidence and focused proof satisfy the chosen slice behavior.
- Select `slice-repair-required` when tests are missing or inadequate, build/runtime/browser proof fails, accepted child handoff evidence is missing, or proof does not map to the chosen slice behavior.
- Return a completed process-step outcome with the selected branch outcome. Do not return `Blocked` only because product proof failed and can be repaired by a bounded implementation subprocess.
- Return `Blocked` only when an environment, permission, unavailable tool, or process-contract issue prevents validation or repair routing.

QA does not add or edit product tests in this step.
