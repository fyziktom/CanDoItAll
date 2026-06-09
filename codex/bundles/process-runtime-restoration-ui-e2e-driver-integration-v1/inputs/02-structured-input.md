# Structured Input

## Primary objective

Restore confidence that the generic process system can still be launched and used from the application after the large Process Core / driver refactor sequence.

## Concrete outcome expected

A user should again be able to use the application UI to choose a process/template from a project or project-structure context and start it. At least two representative process families must be validated:

1. Software development process, using a `.NET` application creation/modification scenario.
2. Generic business-analysis process, not tied to software development.

## Secondary objective

Stabilize the new read-only domain driver layer without allowing it to become an uncontrolled runtime host. Introduce integration only where it helps process manager / verification behavior and only with explicit read-only permission boundaries.

## Hard constraints

- Do not allow tests or source code to depend on `codex/bundles/<bundle-name>` paths.
- Do not remove existing process functionality.
- Do not replace live process proof with docs-only proof.
- Do not introduce a generic runtime driver host, registry, selector, manager command, scheduler hook, workflow hook, shell execution, Graph/Office calls, workspace writes, storage writes, process-state mutation, claim mutation, transition mutation, finalizer application, or retry scheduling unless the subbundle explicitly approves and proves that precise boundary.
- Initial UI proof is large-screen desktop only. Do not spend time on small/medium/mobile screenshots.
- Keep Process Core deterministic and dependency-clean.
