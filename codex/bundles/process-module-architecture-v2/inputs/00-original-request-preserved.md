# Original Request Preserved

This file preserves the source intent for v2. The compatible validator file remains `inputs/00-original-request.md`; this file exists because the improvement instruction package requested an explicit preserved-source entry.

The original user request requires a complete architecture proposal for a new Process module, not an implementation patch. The current Process implementation is unreliable, slow, and not generic enough, but it reveals the core areas the new architecture must solve: artifact sharing, subprocess steps, artifact recovery and resupply, runtime orchestration, monitoring, templates, switch/branch steps, manager-driven recovery, escalation, and subprocess manager communication.

The Process UI/UX direction should be preserved as an anchor. Everything underneath it, including the dispatcher/runtime architecture and current drivers, may be refactored or replaced. Current drivers are useful evidence, not authoritative target design.

The Process module must be treated as a small operating system. The architecture must separate generic process core, runtime execution, dispatcher responsibilities, process instance construction, process templates, domain-specific drivers, domain-specific strategies, process manager behavior, subprocess management, artifact management, monitoring/snapshots, and UI-facing projections for live and historical views.

The core must be generic while supporting layered domain drivers. Driver hierarchies can include broad drivers and narrow sub-drivers, but domain vocabulary must remain behind driver and strategy boundaries. The builder must compose process definition, instance plan, roles, artifacts, steps, subprocesses, selected drivers, selected strategies, recovery behavior, branch behavior, manager behavior, and monitoring configuration before runtime execution starts.

The architecture must model completed steps and artifacts as retained runtime history. Later steps, branches, managers, and subprocesses may need artifacts from any earlier step. Artifact ownership, sharing, availability, dependency, recovery, resupply, parent/child references, provenance, trust, sensitivity, retention, freshness, and validation must be explicit.

The architecture must handle errors and exceptions deliberately. Raw agent, workflow, driver, or subprocess diagnostics can be too detailed for users, so managers must preprocess them into actionable incidents. Automatic recovery may be configured through strategies, but must be bounded by budgets, approval rules, idempotency checks, and escalation limits.

Monitoring must use runtime events, asynchronous observers/projectors, live snapshot cache, history persistence, and UI-friendly projections. Live views must not reload all runtime state from scratch. History filters must be honored, including last-hour behavior.

Templates must use JSON as source of truth and support reusable global components, local overrides, global update publication, conflict detection, manual conflict resolution, schema/content versions, deterministic migrations, and Git-backed files with database indexing. Markdown and Mermaid should be generated/exported projections, not canonical source.

A typed Git wrapper and generic Git UI components are required for configuration versioning, process-run change tracking, unauthorized agent change audits, diffs, commits, merges, status, and conflict resolution.

Switch/branch steps must support generic definitions, domain-provided branch families, user overrides, backward routes, loop budgets, path fingerprints, and escalation when repeated fixes do not help.

Future implementation must happen on a new branch. Before removing the old implementation, the old Process module, related tests, templates, and integration references must be copied into reference material with a manifest and hashes. Only then should active old Process projects/tests be removed and the new module rebuilt project by project with tests.
