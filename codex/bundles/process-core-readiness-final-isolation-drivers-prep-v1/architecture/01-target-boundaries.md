# Architecture Direction

## Goal

Advance from dispatcher-local isolation toward a later safe `Process Core` extraction without creating the Core project yet.

## Target boundaries for this bundle

### A. Route adapter burn-down

Reduce reliance on `ProcessDispatchRouteModelAdapters` and `ProcessRunAutomationDispatchService` forwarding from route services. Keep a small explicit adapter only at the dispatcher edge.

### B. Hydration application boundary

Split `ProcessDispatchCandidateHydrationService` into smaller collaborators:
- hydration query/read service,
- artifact input preparation,
- assignment route decision,
- direct-agent binding,
- recovery directive lookup,
- execution-run availability/recovery selection,
- candidate assembly.

This stays application-local.

### C. Pre-execution/materialization boundary

Move database requirement, upstream materialization block/request, and start transition/reload to explicit services that take route models where possible.

### D. Subprocess runtime boundary

Split subprocess orchestration from subprocess artifact projection persistence and reduce dispatcher nested alias usage.

### E. Finalizer and failure closure boundary

Create route/application-facing finalizer and failure closure models so route execution does not need dispatcher finalizer aliases.

### F. Static wrapper burn-down

Reduce remaining dispatcher static helper wrappers where they only forward to already-existing rule classes.

### G. Core readiness and driver readiness

Prepare final readiness matrices:
- what can move to Core later,
- what must stay in Process module,
- what belongs to future helper drivers,
- what belongs to AgentFramework or tools.

## Non-goals

- No `CanDoItAll.Processes.Core` project.
- No production driver APIs.
- No UI changes.
- No mobile/small/medium browser proof.
