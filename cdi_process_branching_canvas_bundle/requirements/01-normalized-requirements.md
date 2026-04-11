# Normalized Requirements

## Canvas Contract Requirements

- `BRANCH-CANVAS-01`
  - Add an optional advanced canvas node type that supports multiple independently addressable inputs and outputs.
  - Legacy nodes and legacy single-anchor link behavior must remain supported without requiring migration.
- `BRANCH-CANVAS-02`
  - Extend the canvas link contract additively so a link can reference named source and target ports when the participating nodes support them.
- `BRANCH-CANVAS-03`
  - Advanced node rendering must expose readable port labels and curve anchors that visually resemble the user-provided screenshot pattern closely enough to make branching explicit on the canvas.

## Process Authoring Requirements

- `BRANCH-PROCESS-01`
  - Right-click add-branch on a process step must create a new branch node connected to the step that was clicked.
- `BRANCH-PROCESS-02`
  - The branch node must be a first-class projected canvas node, not only metadata rendered inside the source step.
- `BRANCH-PROCESS-03`
  - The branch node must expose one connectable output for each matched branch outcome.
- `BRANCH-PROCESS-04`
  - The branch node must also expose one additional default output and one additional error output.
- `BRANCH-PROCESS-05`
  - Downstream process nodes must be able to connect to those branch-node outputs.
- `BRANCH-PROCESS-06`
  - Decision-maker branches must support an incoming connection from a role-definition node.

## Example And Architecture Requirements

- `BRANCH-EXAMPLE-01`
  - Add realistic software-development branching examples such as code review back-to-repair loops, QA rework loops, and approval chains before merge.
- `BRANCH-ARCH-01`
  - Record architecture troubles or missing foundations encountered while implementing the feature.
- `BRANCH-ARCH-02`
  - Start with scenario definition and gap analysis before feature implementation, and treat missing foundations as early-phase work instead of burying them at the end.

## Validation Requirements

- `BRANCH-VALIDATE-01`
  - Execute the work through a detailed bundle with numbered subbundles and real gates.
- `BRANCH-VALIDATE-02`
  - Run real Playwright validation on the browser surface and capture screenshots.
- `BRANCH-VALIDATE-03`
  - Use screenshot review, especially for the canvas, to judge layout, readability, spacing, and connection clarity.
