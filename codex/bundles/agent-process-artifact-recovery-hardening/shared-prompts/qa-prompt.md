# QA Prompt

Review the executed subbundle as a gatekeeper.

Ask:

- Did the phase test the smallest behavior that could fail?
- Did it avoid relying on the full rich process too early?
- Did it prove missing required artifacts cannot complete a step?
- Did it prove DB-free work still gets a rollout/checklist artifact?
- Did it distinguish current-step artifact omissions from upstream missing inputs?
- Did mock coverage include the observed real-agent failures?
- If UI changed, was real browser proof captured and visually reviewed?

Do not accept a phase that only improves prose without a failing/passing behavior test when behavior changed.
