# Stable project identity

Projects may carry an external namespace/key pair for repeatable provisioning. The pair
is independent of the display name, unique within the active database profile, and
remains reserved while the project is archived. There is no display-name, metadata-marker
or local-file fallback.

## Create and resolve

Send `externalNamespace` and `externalKey` with `POST /api/projects/`, alongside the
normal project editor fields. Both tokens are trimmed and lowercased. Each must contain
1–100 ASCII letters, digits, dots, underscores or hyphens, with an alphanumeric first
and last character. For example:

```json
{
  "name": "Operations workspace",
  "externalNamespace": "partner.integration",
  "externalKey": "operations-workspace"
}
```

The save returns the project UUID. Resolve it directly with:

```http
GET /api/projects/by-external-key/partner.integration/operations-workspace
```

The response contains `projectId`, `lifetimeId`, `externalNamespace` and `externalKey`.
Supply `?expectedLifetimeId=<previous lifetime>` on later resolutions to reject a
replaced binding. Read the editor by UUID and verify its `expectedLifetimeId` matches
the resolution before applying changes. Return that lifetime unchanged on save; the
owner checks it inside the mutation transaction. The older UUID editor route still
returns a blank template for a missing project; a provisioning client must not treat
that as a successful read.

The lookup requires `api.projects.read`; saving requires `api.projects.write`.
These are the existing operator HTTP scopes, not per-project grants. Internal agents
still require their separate project mutation authorization and lifetime admission.

## Update and conflicts

- Omitting both fields, or sending both as null, preserves an existing identity and
  leaves a new project unbound. Older clients keep their existing behavior.
- Providing either field requires a valid complete pair. Empty strings cannot clear it.
- Assigning a pair to an unbound existing project, or saving an already bound pair,
  requires the current `expectedLifetimeId`. An assigned pair cannot be changed.
- Renaming, editing or archiving the project preserves its pair. Physical project
  deletion releases it. Recreating it creates a new lifetime; a stale resolution gets
  an explicit conflict.
- Duplicate creation is a conflict, not an upsert. After an ambiguous network failure
  or a conflict, resolve the identity and inspect the current project before deciding
  whether to continue. Do not automatically retry a mutation with new preconditions.

| Failure | HTTP | Code |
| --- | --- | --- |
| Invalid or incomplete pair, empty expected lookup lifetime | 400 | `projects.external-identity-invalid` |
| Existing project save with a pair but no nonempty lifetime | 400 | `projects.external-identity-lifetime-required` |
| Unknown identity or explicitly missing save UUID | 404 | `projects.not-found` |
| Duplicate/ambiguous binding or concurrent identity save | 409 | `projects.external-identity-conflict` |
| Attempt to replace an assigned pair | 409 | `projects.external-identity-immutable` |
| Lookup with a replaced lifetime | 409 | `projects.lifetime-changed` |
| Editor save with a stale lifetime (existing contract) | 400 | `projects.lifetime-changed` |

Errors use the standard `errors` array. Request binding can return a framework 400
before the handler runs. No provider/database diagnostics are exposed by the new
identity conflict handling.

## Persistence and transfer

`20261007193102_AddProjectExternalIdentity` adds two nullable columns, a paired format
constraint and a filtered unique index. Existing projects remain unbound, with their
UUIDs and lifetimes unchanged. Application locks coordinate cooperating writers; the
database also arbitrates independent writers. Downgrade refuses to remove assigned
identities. Back up an authoritative database before upgrading it.

Profile and project-package transfers preserve the external pair. As before, imported
projects receive fresh target lifetimes. A target collision fails rather than silently
renaming, merging or discarding an identity. Creation compensation fingerprints include
the pair, so a later binding prevents an old creation receipt from deleting the project.

The project module owns normalization, persistence and identity queries; HTTP only maps
typed results to responses. The change adds no dependency on agent/workflow identity
implementations and no new project service abstraction.

## Validation

The focused integration classes are `ProjectExternalIdentityApiIntegrationTests` and
`ProjectExternalIdentityPersistenceTests` (25 source-defined cases). They exercise real
HTTP, scope enforcement, canonical OpenAPI, PostgreSQL upgrade/restart/downgrade,
independent concurrent writers, transfer and compensation. Use an isolated PostgreSQL
18 test server and the discovery procedure in [Testing](testing.md). Production builds,
documentation checks, migration model parity and final `portability-static` enforcement
are also required.
