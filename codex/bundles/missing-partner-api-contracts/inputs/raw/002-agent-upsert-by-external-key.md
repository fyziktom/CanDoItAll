# 002 — Idempotent agent provisioning by external key

Status: **missing in the pinned contract**
Priority: **high**

## Observed contract

`POST /api/agents` saves `AgentEditorModel`. Creation has no client idempotency key or
partner-owned stable key. Updates require the CanDoItAll ID and
`expectedUpdatedAtUtc`. Names and `templateKey` are not a documented external identity
contract.

The examples call `GET /api/agents?includeTemplates=false` and stop when an exact name
already exists. This avoids accidental duplicates but is race-prone and cannot safely
reconcile configuration.

## Needed API

One possible contract:

```http
PUT /api/agents/by-external-key/{namespace}/{key}
If-Match: "configuration-version"
Idempotency-Key: provisioning-operation-id
Content-Type: application/json
```

The body can reuse an external-safe agent editor DTO. The response should include:

```json
{
  "agentId": "uuid",
  "externalKey": "crm-note-classifier",
  "configurationVersion": "opaque-version",
  "created": true,
  "warnings": []
}
```

Also provide:

- `GET /api/agents/by-external-key/{namespace}/{key}`;
- a conflict response when the key belongs to another tenant/workspace;
- a guarded delete/archive operation with expected version.

## Acceptance

Two concurrent retries with the same tenant, external key, payload, and idempotency key
produce one agent. A different payload with a stale expected version returns a conflict
and never silently overwrites changes.
