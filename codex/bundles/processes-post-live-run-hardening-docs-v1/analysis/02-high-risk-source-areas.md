# High-Risk Source Areas

## Process dispatch and grounding

Current source has a lot of path and project-structure logic inside `ProcessRunAutomationDispatchService.ProjectPaths.cs`. It includes ancestor/sibling planning-node focus, external target hint selection, Windows-path parsing, annotation stripping, required artifact path extraction, and priority scoring.

Risk: this logic is powerful but regex-heavy and difficult to reason about.

Target: extract a dedicated service such as `ProcessProjectStructureGroundingService` with typed results, explainable scoring, and adversarial tests.

## Artifact validation and read model

Artifact validation has improved substantially, with typed statuses and read-model parity. The next risk is drift: finalizer, read model, recovery, health, API, and UI can diverge again.

Target: consolidate validation/status projection into a shared service and add status-matrix tests.

## Manager chat

Manager chat now resolves assignments, but fallback scoring still uses manager-like text tokens (`process manager`, `delivery manager`, `manager`, `orchestrator`, `lead`).

Risk: ambiguous or wrong manager selection in complex projects.

Target: capability/tag/assignment-first resolver with reason codes, confidence, and visible diagnostics.

## Project-structure run folder projection

The latest bundle improved folder collapse. Remaining risk is that folder projection policy is implicit and can drift when new artifact storage paths appear.

Target: explicit run-folder projection policy with examples: managed artifact root, product output root, external final delivery root, tool receipt folders, and ignored internals.

## Tests and proof

Broad tests timed out or failed in prior reports. Current proof is mostly focused. That is useful for iteration, but broad readiness needs a test taxonomy that avoids one huge timeout-prone filter.
