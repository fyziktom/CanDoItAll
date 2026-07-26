# 001 — Remote agent package upload/import

Status: **missing in the pinned contract**
Priority: **high**

## Observed contract

`POST /api/agents/import` accepts:

```json
{
  "packagePath": "server-local-path"
}
```

The route passes `packagePath` to the workspace import service. An external partner cannot
upload the package bytes through this command and should not know or control a server
filesystem path.

## Needed API

Provide one remote-safe option, for example:

```http
POST /api/agents/import-package
Content-Type: multipart/form-data
Idempotency-Key: partner-agent-catalog:v4
```

Parts:

- `package`: the exported agent archive;
- `mode`: `create`, `replace-exact-version`, or `clone`;
- `externalKey`: partner-owned stable identifier;
- optional expected package hash.

An alternative JSON endpoint may accept a bounded base64 payload, but multipart/streaming
is preferred for explicit size limits.

## Required behavior

- validate archive type, entry count, expanded size, paths, hashes, and schema version;
- reject traversal, symlinks, executable payloads, and unrecognized secret material;
- resolve provider/capability references explicitly rather than guessing;
- never import raw provider secrets;
- support authorization, audit, idempotency, and optimistic concurrency;
- return agent ID, imported version/hash, unresolved prerequisites, and warnings.

## Acceptance

An external client can export an agent from one permitted environment and import it into
another without server filesystem access. Replaying the same idempotency key cannot create
a duplicate.
