# Observability and log redaction

## 1. Goals
Observabilita musí odpovědět na otázky:
- Co se právě děje?
- Co se stalo předtím?
- Který session/operation to způsobila?
- Jaké evidence máme pro diagnózu?
- Unikají v tom tajná data?

## 2. Required correlation fields
Každá log entry by měla mít minimálně:
- `sequence`
- `timestampUtc`
- `correlationId`
- `ownerKind` (`AppSession`, `Operation`, `System`)
- `ownerId`
- `sessionVersion` (pro app logs, když relevantní)
- `source`
- `stream`
- `text`

## 3. Log categories
- `System`
- `ProcessStdOut`
- `ProcessStdErr`
- `HealthProbe`
- `Diagnostics`
- `Cleanup`

## 4. Persistence recommendation
- in-memory ring buffer for low-latency MCP access
- optional file persistence in NDJSON
- bounded retention by size and/or age

## 5. Redaction policy
Redigovat minimálně:
- bearer tokens
- API keys
- password assignments
- connection strings with password-like fields
- secret-looking env variables

### Suggested patterns
- `(?i)bearer\s+[A-Za-z0-9\-\._=]+`
- `(?i)(password|pwd|secret|token|api[_-]?key)\s*[:=]\s*[^\s;]+`
- `(?i)(User ID|UserId|UID)=[^;]+;.*(Password|Pwd)=[^;]+`

Poznámka:
- Patterny musí být konzervativní, ale ne destruktivní pro běžné diagnostické informace.

## 6. Redaction behavior
Příklad:
- input: `Authorization: Bearer eyJhbGciOi...`
- output: `Authorization: Bearer ***redacted***`

Příklad:
- input: `Password=MySuperSecret123;`
- output: `Password=***redacted***;`

## 7. Cleanup audit trail
Každá cleanup akce musí logovat:
- reason
- target pid
- owner metadata
- whether it was killed or skipped
- safety explanation for skip

## 8. Timeout evidence
Při wait timeoutu nebo operation timeoutu log a response musí vrátit:
- condition
- elapsed time
- last known state
- relevant last log entries nebo cursor

## 9. Privacy boundary
I když je server lokální, logy mohou být:
- čtené agentem,
- přikládané k review,
- kopírované do issue/PR.

Proto redaction není volitelný detail.

## 10. Review checklist
- jsou correlation IDs všude?
- lze propojit cleanup akci s původní session?
- jsou timeouts akční?
- zůstávají logy čitelné po redakci?
