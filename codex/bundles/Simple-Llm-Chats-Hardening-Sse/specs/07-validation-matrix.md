# Validation matrix

| Behavior | Unit | PostgreSQL | HTTP/SSE | Portability |
|---|---|---|---|---|
| Canonical create/rename transaction | mapper/invariant | failure injection | optional | Linux DB |
| Admission/finalization atomicity | reducer | crash windows | status | Linux DB |
| Idempotent replay | fingerprint/reducer | same/different ID races | repeated POST | all hosts |
| Cancellation ordering | reducer | concurrent commits | second client cancel | all hosts |
| Compensation/recovery | reducer | injected failures/restart | recovery endpoint | all hosts |
| Profile switch | scope policy | switch before each commit | stream closes | all hosts |
| Multi-instance lease | claim policy | two service providers | two hosts if feasible | Linux primary |
| Bounded queries | paging policy | large transcript/query count | page endpoints | all hosts |
| OpenAI/Azure parsing | parser fixtures | no | fake transport | newline/UTF-8 |
| Ollama parsing | parser fixtures | no | fake transport | LF/CRLF chunks |
| Retry after delta | policy | event/attempt rows | terminal event | all hosts |
| Event sequence/replay | coalescer | sequence/gap/retention | Last-Event-ID | all hosts |
| Request disconnect | no | worker keeps operation | disconnect/reconnect | Linux primary |
| Authorization/origin | policy | audit row | scopes/DTO | all hosts |
