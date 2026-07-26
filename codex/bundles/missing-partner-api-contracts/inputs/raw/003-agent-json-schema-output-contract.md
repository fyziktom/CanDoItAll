# 003 — Portable JSON Schema output contract for agent runs

Status: **missing in the pinned public DTO**  
Priority: **high**

## Observed contract

`POST /api/agents/{agentId}/execution-runs` exposes `structuredOutput`, but its
`AgentStructuredOutputContract` describes the output with a .NET `Type`. That is not a
portable request shape for JavaScript, Python, Java, or generated OpenAPI clients.

The partner examples set `structuredOutput` to `null`, request JSON through instructions,
and validate `responseText` outside CanDoItAll.

## Needed API

Add an explicitly versioned JSON Schema request shape, for example:

```json
{
  "structuredOutput": {
    "kind": "json-schema",
    "name": "crm_note_classification",
    "schema": {
      "type": "object",
      "additionalProperties": false,
      "properties": {
        "classification": { "type": "string" },
        "confidence": { "type": "number", "minimum": 0, "maximum": 1 }
      },
      "required": ["classification", "confidence"]
    },
    "strict": true
  }
}
```

The execution result should include parsed data, raw provider output, schema hash, and
validation status without discarding audit evidence.

## Required behavior

- enforce size and complexity limits;
- document provider/model fallback behavior;
- reject unsupported contracts before billable execution where possible;
- distinguish provider refusal, malformed output, and schema-validation failure;
- preserve the exact schema and hash with the run evidence.

## Acceptance

A non-.NET generated client can submit a JSON Schema, receive validated JSON, and
deterministically identify contract failures.
