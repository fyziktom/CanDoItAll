# Pre-Execution Boundary Staging

## Stage 1: inventory and guardrails

No production movement. Create scans and tests that fail if code is moved unsafely.

## Stage 2: pure decisions

Move only pure decisions:

- missing inputs
- target selection
- block reason
- directive
- fingerprint
- request builders

## Stage 3: side-effect coordinators

Move side effects into explicitly named coordinators:

- database block transition
- journal record/dedup
- upstream rerun request

## Stage 4: handler facade

Only after Stage 2 and 3 pass, create a thin handler facade used by `Dispatch.cs`.

The facade must not become a mini-dispatcher. It only handles pre-execution guards before subprocess/workflow/agent execution route.
