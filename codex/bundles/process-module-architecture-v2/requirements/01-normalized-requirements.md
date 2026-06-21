# Normalized Requirements

## Core Architecture

REQ-001: Define a generic Process Core that contains only domain-neutral process concepts.

REQ-002: Define a Runtime Engine responsible for state transitions, scheduling, cancellation, persistence coordination, and runtime event emission.

REQ-003: Define a Dispatcher responsible for selecting executable work, claiming it safely, invoking the assigned execution strategy, and handing results back to the runtime.

REQ-004: Define process instance composition as an explicit build product, not an implicit side effect of runtime execution.

REQ-005: Separate definition, template, instance, runtime state, event, snapshot, and UI projection models.

## Drivers And Strategies

REQ-006: Support layered domain drivers such as broad software-development, .NET, Blazor, and Blazor WASM drivers without domain vocabulary in the core.

REQ-007: Allow drivers to provide capabilities, strategy factories, branch definitions, artifact recovery policies, manager policies, and template fragments.

REQ-008: Assign step execution strategies at build time for normal steps, subprocess steps, workflows, agents, multi-agent collaboration, and automatic handoff flows.

REQ-009: Use strategy interfaces for step execution, manager decisions, recovery, artifact resupply, error preprocessing, branch decisions, subprocess manager communication, and loop escalation.

## Builder And Instance Composition

REQ-010: Build process instances from definition/template inputs plus run-specific context.

REQ-011: Compose roles, artifacts, steps, subprocess instances, selected drivers, selected strategies, recovery behavior, branch/switch behavior, manager behavior, and monitoring configuration.

REQ-012: Recursively build subprocess instances when a step is a subprocess.

REQ-013: Enforce subprocess depth, cycle, and parent/child compatibility checks during composition.

REQ-014: Persist the composed instance plan before execution starts.

## Artifacts

REQ-015: Model artifact ownership, sharing, availability, dependency, recovery, resupply, and cross-process references.

REQ-016: Retain completed step results and artifact ledgers for later consumers.

REQ-017: Allow later steps, branch logic, managers, and subprocesses to reference artifacts from any earlier step when permitted by policy.

REQ-018: Route missing artifact handling through manager and driver/strategy mechanisms.

REQ-019: Track artifact provenance, trust, sensitivity, retention, freshness, and validation status.

## Errors, Recovery, And Manager

REQ-020: Model runtime errors and domain diagnostics separately.

REQ-021: Preprocess detailed errors into user-actionable manager incidents.

REQ-022: Support configured automatic recovery for selected error types through strategies.

REQ-023: Prevent uncontrolled recovery loops with explicit budgets and escalation gates.

REQ-024: Define a generic process manager with domain-specific behavior supplied by strategies and drivers.

REQ-025: Support parent manager and subprocess manager communication.

## Monitoring

REQ-026: Emit typed runtime events for all relevant state changes and decisions.

REQ-027: Use observers/subscribers without allowing monitoring to block runtime execution.

REQ-028: Maintain current/live snapshot cache and historical projections.

REQ-029: Apply time-range filters at the projection/query boundary so Live Hour does not show stale historical events unless requested.

REQ-030: Provide UI-friendly live and history read models.

## Templates

REQ-031: Use JSON as the source of truth for templates and process configuration.

REQ-032: Treat Markdown and Mermaid as generated projections unless explicitly exported by the user.

REQ-033: Support global template components and local overrides.

REQ-034: Support publishing global updates to usages, detecting conflicts, and resolving conflicts manually.

REQ-035: Version template schemas and content.

REQ-036: Provide deterministic template migrations and handle skipped intermediate versions.

REQ-037: Store template/configuration files in Git, with database indexing for search and UI performance.

## Git

REQ-038: Create a typed Git wrapper project instead of implementing Git semantics directly.

REQ-039: Use the Git wrapper for versioning templates, instructions, skills, workflow definitions, process definitions, and runtime change tracking.

REQ-040: Let the process manager verify whether agents modified unauthorized files.

REQ-041: Provide reusable Git UI components for status, diffs, commits, merges, conflicts, and conflict resolution.

## Branching

REQ-042: Support generic branch/switch steps.

REQ-043: Support domain-specific branch definitions and user overrides.

REQ-044: Allow branch routes to previous steps.

REQ-045: Protect backward routes with loop budgets, path fingerprints, and escalation.

## Rewrite

REQ-046: Version architecture bundles by changing `.gitignore`.

REQ-047: Start implementation later on a new branch.

REQ-048: Copy old Process implementation into bundle/reference material before deleting it.

REQ-049: Remove old Process projects/tests before rebuilding to avoid accidental coupling.

REQ-050: Rebuild with tests project by project and phase by phase.

