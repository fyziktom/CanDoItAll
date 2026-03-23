# Ukázky tool response

## Krátká úspěšná operace

```json
{
  "correlationId": "01HQ8S3BTF2JYWEK7N9K7M8F8C",
  "target": "staging-01",
  "kind": "target_test",
  "status": "success",
  "summary": "SSH connectivity and target policy checks passed.",
  "data": {
    "hostFingerprint": "SHA256:example",
    "os": "Ubuntu 24.04.2 LTS",
    "dockerAvailable": true
  },
  "nextSteps": [
    "Run compose_validate before the first deploy."
  ]
}
```

## Dlouhá operace

```json
{
  "correlationId": "01HQ8S4CP7H1A1D5D9CBN3S88F",
  "target": "staging-01",
  "kind": "compose_apply",
  "status": "running",
  "summary": "Stack apply has started as a detached remote job.",
  "operationId": "op_01HQ8S4CP7H1A1D5D9CBN3S88F",
  "data": {
    "stackName": "api-staging",
    "mode": "detached"
  },
  "nextSteps": [
    "Call operation_wait.",
    "If the wait times out, call operation_logs."
  ]
}
```

## Chyba politiky

```json
{
  "correlationId": "01HQ8S5G7BQ49TBK8K2VQ6A1CE",
  "target": "staging-01",
  "kind": "fs_read_text",
  "status": "path_not_allowed",
  "summary": "The requested path is outside the allowed roots for this target.",
  "errors": [
    {
      "code": "path_not_allowed",
      "message": "Path '/etc/shadow' is not allowed for target 'staging-01'."
    }
  ],
  "nextSteps": [
    "Use one of the target allowed roots.",
    "Review target configuration if this path should be permitted."
  ]
}
```
