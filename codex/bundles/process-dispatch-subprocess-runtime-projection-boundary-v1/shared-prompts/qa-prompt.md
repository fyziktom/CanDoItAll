# QA Prompt

Review the implementation against the bundle gates.

Reject the implementation if:

- Process Core appears.
- Production process driver API appears.
- UI files change without explicit approval.
- Small/medium/mobile proof artifacts are created.
- Subprocess artifact keys, lineage, markdown content, journal dedupe, or transition statuses change without focused parity proof.
- Source scans are used instead of tests for behavior-heavy changes.
- Side effects are hidden in pure helper names.
