# Feature block review checklist

## Scope
- What exactly changed?
- Which folders, projects, or namespaces own the change?
- Which types are new?
- Which existing types were modified?

## Classification
For each important changed type, classify as one primary role:
- Domain Core
- Policy / Authorization
- Application Workflow
- Projection / View / Read Model
- Integration Adapter / DTO
- UI State / View Layer
- Runtime / Dev Tooling

## Fit
- Did the block add canonical truth?
- Did it add only a projection?
- Did it create a second truth?
- Did it overfill an existing universal type?

## Boundaries
- Is the new code placed in the right project?
- Did the block widen dependencies?
- Did infrastructure leak into the core?

## Invariants
- What must always hold true?
- Where is that enforced?
- Is there a test for it?

## Wave stabilization
- What must be fixed before the next major block lands?
- What can be postponed?
