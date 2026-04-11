# Normalized Requirements

## Canvas Contract Requirements

- `BRANCH-CANVAS-01`
  - Add an optional advanced canvas node type that supports multiple independently addressable inputs and outputs.
  - Legacy nodes and legacy single-anchor link behavior must remain supported without requiring migration.
- `BRANCH-CANVAS-02`
  - Extend the canvas link contract additively so a link can reference named source and target ports when the participating nodes support them.
- `BRANCH-CANVAS-03`
  - Advanced node rendering must expose readable port labels and curve anchors that visually resemble the user-provided screenshot pattern closely enough to make branching explicit on the canvas.
- `BRANCH-CANVAS-04`
  - Connector authoring must start with left click on a visible connector circle and finish with left click on a visible target connector circle.
  - The gesture must not depend on right click once this follow-up scope closes.
- `BRANCH-CANVAS-05`
  - Connector circles must be positioned on the badges that explain the corresponding input or output.
  - If a badge-backed port is rendered, its circle must also be rendered and targetable.

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
- `BRANCH-PROCESS-07`
  - The process authoring model must support many-to-many routing semantics when the canvas shows one logical input collecting outputs from multiple upstream nodes.
  - If the current process model cannot support that canonically, the implementation must reopen architecture and record the blocker instead of drawing fake UI-only joins.
- `BRANCH-PROCESS-08`
  - Role, router, and other derived node movements must persist correctly through editor interactions, surface rebuilds, and reloads.
- `BRANCH-PROCESS-09`
  - The router-side decision-role badge must expose its own connector circle when the decision role exists.

## Example And Architecture Requirements

- `BRANCH-EXAMPLE-01`
  - Add realistic software-development branching examples such as code review back-to-repair loops, QA rework loops, and approval chains before merge.
- `BRANCH-EXAMPLE-02`
  - Add at least one realistic software-development example that exercises join-style input collection or, if the domain cannot support it yet, documents the exact canonical limitation in the scenario and trouble log.
- `BRANCH-ARCH-01`
  - Record architecture troubles or missing foundations encountered while implementing the feature.
- `BRANCH-ARCH-02`
  - Start with scenario definition and gap analysis before feature implementation, and treat missing foundations as early-phase work instead of burying them at the end.
- `BRANCH-ARCH-03`
  - Audit the persistence path for moved derived nodes and resolve any snap-back behavior through canonical storage, not only browser memory.

## Validation Requirements

- `BRANCH-VALIDATE-01`
  - Execute the work through a detailed bundle with numbered subbundles and real gates.
- `BRANCH-VALIDATE-02`
  - Run real Playwright validation on the browser surface and capture screenshots.
- `BRANCH-VALIDATE-03`
  - Use screenshot review, especially for the canvas, to judge layout, readability, spacing, and connection clarity.
- `BRANCH-VALIDATE-04`
  - Browser proof must explicitly cover left-click start and left-click finish of connector authoring.
- `BRANCH-VALIDATE-05`
  - Browser and code proof must explicitly cover exact badge-circle alignment, the missing `Review lead` router circle, and moved-node persistence after a rerender-triggering interaction.
