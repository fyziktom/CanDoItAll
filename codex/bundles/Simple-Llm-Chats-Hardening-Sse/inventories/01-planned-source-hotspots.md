# Planned source hotspots

| Responsibility | Current files to inspect first | Expected disposition |
|---|---|---|
| Definition/revision invariants | `Definitions/*` | Mostly preserve; strengthen tests |
| Conversation commands | application conversation service, EF repositories/store | Replace choreography with atomic command methods |
| Operation state | operation entity/service | Extract/centralize pure reducer and legal transitions |
| Evidence/audit | evidence service, audited invocation port | Replace approximate callbacks with real attempt/event facts |
| Profile scope | runtime lease/engine | Expand to whole-use-case scope |
| Cancellation | in-memory registry | Keep local signal only; add durable canonical evidence |
| Dispatcher | synchronous operation service/API | Add durable claim worker; endpoint stops awaiting provider |
| Queries | EF conversation store/list paths | Add SQL read models/keyset paging/bounded context loader |
| Streaming contracts | LLM invocation contracts | Add additive typed streaming port/updates |
| Provider contracts | provider capability contracts | Add explicit streaming capability |
| OpenAI/Azure driver | OpenAI driver | Parse true provider stream |
| Ollama driver | Ollama driver | Request/parse NDJSON stream |
| Durable events | operation rows/configs/repos | Add sequence journal/coalescing/retention |
| SSE API | LLM operation API, existing streaming helpers | Add operation events endpoint, reuse writer/profile stream |
| Security | API access options/policies | Add read/manage/execute scopes, server-owned origin |
| Migration | PostgreSQL migrations/model snapshot | Append new migration only |
| Tests | Unit/Integration projects | Add focused classes named by subbundles |
