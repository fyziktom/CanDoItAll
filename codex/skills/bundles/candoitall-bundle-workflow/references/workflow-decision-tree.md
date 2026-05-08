# Workflow Decision Tree

## Start Here

Ask:

1. Is there already a bundle for this task?
2. Is that bundle still accurate and implementation-ready?
3. Does the requested work still match the bundle?

## If No Bundle Exists

Use bundle preparation.

Typical signals:

- raw prompt only
- docx feedback only
- screenshots with comments
- broad migration or architecture request
- user explicitly asks for a bundle first

## If A Bundle Exists

Use bundle execution when:

- the bundle has concrete subbundles
- proof rules are defined
- prerequisites and progression gates are defined
- the dependency map and critical path are explicit
- the bundle still matches the repo state

Return to preparation when:

- the bundle has missing or vague subbundles
- the bundle has no usable dependency map or critical foundation plan
- the repo changed enough that source references are stale
- execution exposes missing requirements or false assumptions

## If State Was Lost

When the conversation was compacted, interrupted, or resumed without clear current state:

- reread the root README, phase plan, selected subbundle, and execution report
- trust bundle files and fresh repo observations over conversational memory
- continue from the latest proven gate
- repair the bundle before executing if the durable state is incomplete
