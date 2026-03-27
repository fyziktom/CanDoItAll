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
- the bundle still matches the repo state

Return to preparation when:

- the bundle has missing or vague subbundles
- the repo changed enough that source references are stale
- execution exposes missing requirements or false assumptions
