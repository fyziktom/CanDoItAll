# Repair validation findings

**Process:** `software-delivery` / Multi-team software delivery and release governance  
**Step key:** `quality-repair`  
**Kind:** Work

## Purpose
Repair concrete defects, missing workflows, failed validation, or proof gaps identified by QA without expanding beyond the approved delivery scope.

## Inputs
- QA repair-required disposition and regression evidence.
- Reviewed implementation package and peer-review notes.
- Architecture and project-structure context that defines the intended product root and scope.

## Outputs
- Quality repair change set.
- Rerun validation notes and remaining risk.

## Governance
The implementation owner repairs the deliverable in place and reruns the relevant validation after the last mutation. If repair requires scope expansion, dependency changes, or environment access outside the approved boundary, escalate instead of hiding the change in the repair note.
