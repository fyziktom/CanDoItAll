# 10. QA Gap Review Round 1

Role:

- Senior C# and MCP QA inspector

Review target:

- initial bundle draft before remediation

## Findings

### 1. Atomicity semantics were initially too ambiguous

Risk:

- "atomic update" could be misread as full zero-downtime port continuity, which the proposed bundle did not actually guarantee

Required remediation:

- explicitly define bundle 1 atomicity as logical runtime atomicity for Codex
- state stable-port continuity as a non-goal for this bundle

### 2. Rollback was mentioned but not explicit enough

Risk:

- an implementation agent could treat rollback as optional

Required remediation:

- define rollback contract, state transitions, tool shape, and validation gates explicitly

### 3. Bridge repair needed stricter retry governance

Risk:

- a naive repair loop could duplicate non-idempotent work

Required remediation:

- define idempotency-key policy and safe retry matrix

### 4. Resource coordination needed named scope examples

Risk:

- "replace the global lock" is directionally correct but too weak for safe implementation

Required remediation:

- document concrete scope names and acquisition intent

### 5. Validation criteria needed measurable thresholds

Risk:

- without timing and behavior thresholds, final approval would become subjective

Required remediation:

- add explicit pass/fail gates for bridge, watch fluency, atomic prepare, commit, and rollback

### 6. Migration and backward compatibility needed stronger protection

Risk:

- the implementation could break current `WatchRun` clients while chasing atomic features

Required remediation:

- make compatibility a strict pass criterion, not an informal preference

### 7. Candidate endpoint allocation was under-specified

Risk:

- isolated candidate runtimes need explicit port leasing or the implementation will drift into collisions and flaky validation

Required remediation:

- define endpoint allocation as an architectural concern, not an implementation detail
- add validation rules for collision-free candidate runtime startup

### 8. Self-host validation isolation was not explicit enough

Risk:

- the repo could still improve runtime atomicity while leaving the MCP server unable to validate itself safely during live development

Required remediation:

- make self-host build/test isolation a required behavior and a validation gate

## QA verdict after round 1

Conditionally rejected until the above items are folded back into the bundle.
