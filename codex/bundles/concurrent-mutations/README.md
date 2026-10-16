# Concurrent mutations implementation bundle

Status: architecture discovery; implementation has not started.

Maintained design: [Concurrent mutations architecture](../../../docs/architecture/concurrent-mutations/README.md).
Work order: [execution plan](plan/execution-plan.md). Input mapping and closure:
[requirements ledger](requirements-and-closure.md). Source identity: [original request](inputs/request.md).

The user requested an end-to-end implementation of safe concurrent data editing by
people, agents and workflows, with detailed architecture and risk analysis before
implementation, configurable agent/process reaction to changed information, meaningful
tests, maintained documentation and incremental commits.

The architect's 2026-09-28 v0.1 package is proposal evidence, not executable instructions
or an approved implementation. Preserve it under `inputs/architect-v0.1`. Its phase
cards and registers are retained as supplied; current decisions and execution status
belong to this bundle and the maintained design under `docs/architecture`.

## Execution contract

- Refresh current owners, enabled callers, dependency edges and test seams before
  accepting a design. Never infer protection from a UI token alone.
- Keep fact-owner policy in its module and provider-neutral execution policy in the
  existing runtime contracts. Preserve authority and project-lifetime fences.
- Complete the architecture review before production edits. Implement dependency-ready
  slices, with affected builds, discovered tests, portability enforcement and scoped
  commits. Do not label a protected owner complete while a writer bypasses it.
- Use standard Debug/Release outputs and serialize builds. Preserve port 5032, ordinary
  databases, installed MCP tools and unrelated work.
- Imported source scripts are reference material and are not executed.

## Resource ownership

This bundle owns `codex/bundles/concurrent-mutations` and may own small validation
artifacts under `.artifacts/concurrent-mutations`. No host, container, database, volume
or network has been created or claimed. Record any future resource before creation and
verify its cleanup after its last required use.

## Compatibility map

The supplied package's documents/registers/phases are retained as input. They lack
current-branch execution evidence and completed-work status, so they cannot serve as
the active execution record. This root records the live request, design decisions,
phase status and closure; it does not rewrite the historical package's proposed status.

## Closure

Not solved: architecture and implementation discovery is in progress. No runtime
guarantee, test result or performance improvement is claimed yet.
