# Discovered Repair Subbundle Template

Use this template when a real execution cycle discovers a defect that was not knowable during bundle preparation.

Required title pattern:

`NN-repair-{short-defect-name}`

Required opening block:

```text
Observed during: Stage {Sxx}, project {project-key}, cycle evidence {path}
Failure class: chunking | source-reference | duplicate | contradiction | review-ui | recall | vector-projection | chat-integration | other
Blocking severity: blocker | high | medium | low
Repair decision: fix now | document blocker | defer with explicit reason
```

Required proof:

- Reproduction evidence.
- Code or data scope.
- Focused validation.
- Rerun of the affected stage or chat probe.
- Updated `reviews/01-execution-report.md`.
