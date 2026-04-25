# QA Prompt

Review the assigned subbundle implementation from the perspective of a process operator running agent-backed work from the UI.

Check:

- Can the user understand the current run state without logs or database access?
- Can the user see which agent attempt produced which result?
- Can the user see which required artifacts are satisfied or missing?
- Can the user recover from missing artifacts, agent crash, context loss, and outbox dead-letter state?
- Does the implementation preserve strict governed completion?
- Are backend, component, and browser tests aligned with the risk of the changed behavior?

Reject the subbundle if the UI hides an actionable failure behind logs, if required artifacts can be silently skipped, or if retries can complete work without durable proof.
