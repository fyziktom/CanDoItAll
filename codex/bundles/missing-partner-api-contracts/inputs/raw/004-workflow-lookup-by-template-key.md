# 004 — Workflow lookup by stable template/external key

Status: **missing in the pinned catalog API**
Priority: **high**

## Observed contract

`GET /api/workflows/definitions` returns workflow/version IDs and display metadata, but
not the source template key. The copied seed has key `sales-lead-qualification`, while
the materialized display name is `Example: Sales Lead Qualification`.

The example must match that display name exactly. Display names are mutable and are not
safe integration identities.

## Needed API

Expose stable lookup and provenance, for example:

```http
GET /api/workflows/definitions/by-template-key/{templateKey}
GET /api/workflows/definitions?externalNamespace=partner&externalKey=sales-lead-qualification
```

Catalog/detail responses should include:

```json
{
  "workflowId": "uuid",
  "versionId": "uuid",
  "templateKey": "sales-lead-qualification",
  "templatePackKey": "candoitall-workflow-template-pack",
  "templatePackVersion": "version",
  "sourceHash": "sha256",
  "externalKey": null
}
```

Define uniqueness per tenant/workspace and distinguish system seed identity from
partner-owned external identity.

## Acceptance

A client can resolve the intended workflow without display-name matching, detect multiple
or stale materializations, and pin a specific runnable version.
