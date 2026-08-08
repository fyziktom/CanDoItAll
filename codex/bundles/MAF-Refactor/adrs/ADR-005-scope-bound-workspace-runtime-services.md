# ADR-005: Workspace runtime services are created as one scope-bound bundle

- Status: Accepted for implementation
- Date: 2026-08-06

## Context

`RuntimeCapabilityComposer` currently has a constructor workspace scope and also receives a per-run context scope. `MafRuntimeDependencyResolver` may return already registered services or construct fallbacks. A single runtime build can therefore combine services created for different scope identities.

## Decision

Introduce `WorkspaceExecutionScope` and `IWorkspaceRuntimeServicesFactory`.

`WorkspaceExecutionScope` includes:

- workspace root,
- `WorkspaceScopeDescriptor`,
- database profile ID and generation,
- authority ID/fingerprint,
- execution run ID when available.

The factory creates one owned `WorkspaceRuntimeServices` bundle containing all scope-bound services used by the turn:

- file service,
- path resolver,
- command service,
- process host,
- artifact/document/image services,
- receipt/audit writers,
- any MCP/browser artifact path service requiring workspace identity.

Every member exposes or is validated against the same immutable scope identity. The bundle owns disposal. Runtime code receives the bundle; it does not look up competing services from a root `IServiceProvider`.

## Consequences

- Project, organization, and sandbox runs cannot accidentally mix services.
- Runtime capability composition becomes deterministic and directly testable.
- Manual workspace creation and DI-based creation converge on one factory.
- Scope mismatches fail at construction rather than during a tool call.

## Proof

- Organization and Project bundles report distinct identities.
- Every file, command, MCP, artifact, and receipt path in one run uses the same identity.
- No fallback service construction remains in `MafRuntimeDependencyResolver`.
- No runtime field retains `IServiceProvider`.
