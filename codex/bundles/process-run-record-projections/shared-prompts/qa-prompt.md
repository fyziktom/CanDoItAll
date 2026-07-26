# QA Prompt

Review the assigned subbundle against its requirements, architecture checkpoint, success criteria, do-not-do rules, and proof tier.

Verify positive and negative behavioral tests, dependency direction, query/call shape, cancellation, explicit error/completeness semantics, data privacy, migration/index model, and compatibility with downstream consumers. Treat compiler success without behavioral evidence as insufficient.

For data-source-only UI work, component/service tests are sufficient. If rendered markup changes, require the documented 1600x900 Runs/Graphs/Analytics browser pass, assertions, screenshots, first-viewport result, and scroll-owner review.

Update the execution report and raw-note closure only for proven outcomes. If evidence is missing, mark the gate failed or blocked, record the exact reason, and reopen the owning/upstream subbundle.
