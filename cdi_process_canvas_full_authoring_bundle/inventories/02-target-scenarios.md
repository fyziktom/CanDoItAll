# Target Scenarios

## Purpose

- These scenarios define the minimum realistic software-development flows that execution must be able to author and then prove on the canvas.

## Scenario 1: Code Review With Repair And QA Loop

- Roles
  - Review lead
  - QA lead
  - Security reviewer
  - Solution architect
  - Developer
- Required semantics
  - Role participation on review, repair, QA, and approval steps
  - Branch-router outcomes from review disposition
  - Many upstream joins into a revalidation step
  - Explicit decision-authority assignment for disposition routing
  - Artifact outputs such as review notes, test evidence, and merge recommendation

## Scenario 2: Pull Request Approval And Merge Window

- Roles
  - Review lead
  - QA lead
  - Release manager
  - Security reviewer
- Required semantics
  - Multiple approver and reviewer assignments across different steps
  - Delivery and approval step-kind coverage
  - Optional default and error branch paths
  - Branch and direct dependency combinations

## Scenario 3: Hotfix Rollout With Parallel Evidence Inputs

- Roles
  - Incident commander
  - Developer
  - QA lead
  - Release manager
- Required semantics
  - One rollout gate step waiting on multiple upstream artifact or evidence sources
  - Many-to-single joins into a final approval or deploy step
  - Artifact-consumption semantics if the model extension lands

## Scenario 4: Incident Escalation And Closure

- Roles
  - Incident commander
  - Solution architect
  - Operations reviewer
  - QA lead
- Required semantics
  - Decision step with escalation routing
  - Review and approval step kinds
  - Runtime projection clarity after the definition graph is authored

## Closure Expectation

- Final execution proof must show at least one seeded scenario where:
  - role participation links were authored from the canvas
  - step dependencies were authored from the canvas
  - branch routing was authored from the canvas
  - the resulting layout persists across save and reload
