# QA Prompt

QA the assigned subbundle against the raw request and the bundle gates.

Check:

- The subbundle owns the claimed requirements and does not skip raw input coverage.
- Source route/DTO/tool claims match current C# files.
- Docs and skills use exact routes, current DTO fields, and explicit base-path guidance.
- Runtime tool additions include policy, approval, descriptor, service-call, and test coverage.
- Active skill copies are synchronized when repo skills are edited.
- Proof in `reviews/01-execution-report.md` includes commands, exit status, artifact paths, and residual risks.
- UI changes, if any, include browser analytics and screenshots.

Block closure when:

- Proof is only prose and lacks source/test/artifact references.
- A route or DTO appears in source but is missing from the intended docs/skills/test coverage with no explicit exception.
- The workbook is stale after route/DTO/tool changes.
- Prepared or completed bundle validation fails.
