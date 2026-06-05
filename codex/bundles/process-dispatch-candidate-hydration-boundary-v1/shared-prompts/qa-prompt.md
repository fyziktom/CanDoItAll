# QA Prompt

Review the implementation of `process-dispatch-candidate-hydration-boundary-v1`.

Check:

- no Process Core or production driver API was introduced,
- candidate header ordering and eligibility are unchanged,
- candidate hydration creates identical subprocess/workflow/direct-agent candidates,
- technical-agent binding/access mutation is still explicit and tested,
- manual recovery directive and recoverable execution behavior are unchanged,
- no UI files or prohibited viewport proof artifacts were created,
- full build and focused dispatch tests passed,
- execution report closes every raw note.
