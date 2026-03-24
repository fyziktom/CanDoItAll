# 13. QA Gap Review Round 2

Role:

- Senior C# and MCP QA inspector

Review target:

- bundle after the first remediation pass and after the workflow-discipline follow-up requirement

## Findings

### 1. Codex workflow steering was still implicit

Risk:

- the bundle improved runtime correctness, but it still left Codex to infer the preferred "small change, validate, then continue" loop by itself

Required remediation:

- add an explicit workflow-steering layer to the architecture and contract
- define when the MCP should recommend fast-path watch iteration versus atomic candidate work

### 2. Guidance payload budget and emission scope were not defined

Risk:

- a naive implementation could inject reminders into every response and waste the same context that log reduction is trying to preserve

Required remediation:

- add a compact guidance shape
- define selected emitters and explicit non-emitters
- add a measurable payload budget

### 3. Tool descriptions were too operational and not instructional enough

Risk:

- Codex may discover the tools, but still miss the intended working discipline

Required remediation:

- require a short static workflow sentence on the key tools where it helps agent behavior

### 4. Validation did not yet prove that steering helps without polluting high-volume payloads

Risk:

- the team could claim the guidance exists without proving that it is accurate, compact, and absent from logs/events

Required remediation:

- add unit and integration gates for guidance selection, suppression, and size
- add evidence artifacts showing both positive guidance and deliberate omission

### 5. The planning pass still reproduced generic direct-tool failures

Risk:

- guidance alone would not solve the core Codex experience if the bridge still degrades into generic invocation failures

Required remediation:

- keep guidance as an addition to bridge hardening, not a substitute
- record the reproduced generic failures as current-state evidence

## QA verdict after round 2

Conditionally rejected until the above items are folded back into the bundle.
