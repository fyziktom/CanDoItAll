# UI And Process E2E Target Flows

## Large desktop only

Use large desktop viewport. Do not spend time on small/medium/mobile proof unless UI files change unexpectedly and the bundle is re-scoped.

## Required flows

1. `/processes`
   - load page
   - select/import template
   - create launch plan
   - execute launch plan
   - verify run appears in run list/detail

2. `/projects/{projectId}/processes`
   - open scoped process workspace
   - verify project context is retained
   - start process

3. Project-structure start path
   - open a project structure node
   - start linked process
   - verify process run has project/node context

4. Live run detail
   - inspect steps, assignments, artifacts, diagnostics, manager/read-only verification projection
   - no seeded baseline run may be accepted as live proof

5. Recovery/blocked run
   - force deterministic missing artifact or validation issue
   - verify UI shows correct recovery/diagnostic state without mutating via drivers
