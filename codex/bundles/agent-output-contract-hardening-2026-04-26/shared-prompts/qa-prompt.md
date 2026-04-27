# QA Prompt

Review the completed work as a senior .NET workflow automation reviewer.

Check:

- Does any process-critical decision still read assistant markdown as the source of truth?
- Is every structured payload deserialized into a wrapper DTO rather than a primitive or top-level list?
- Do validators reject missing reason, invalid status, invalid branch outcome, missing evidence, and inconsistent outcome/actions?
- Is repair bounded and followed by re-validation?
- Are raw outputs captured for diagnostics without leaking secrets?
- Do tests prove malformed JSON cannot persist a successful process result?
- Does `docs/agent-output-contracts.md` explain how to add new typed contracts?

Record findings in `reviews/01-execution-report.md`.
