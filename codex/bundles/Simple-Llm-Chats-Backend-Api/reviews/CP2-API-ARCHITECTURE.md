# CP2 — API architecture review

Status: Pass

## Checklist

- [x] Ordinary chat routes are separate from `/api/agents`.
- [x] Endpoint adapters are thin.
- [x] DTOs do not expose EF entities or internal transcript documents.
- [x] Full `ProviderProfile`, credentials, endpoints, and local paths are rejected.
- [x] Provider options expose model-specific thinking-effort capability/default safely and mutation DTOs reject unsupported or duplicate effort inputs.
- [x] Definition and conversation concurrency is explicit.
- [x] Turn operation identity is explicit and retry-safe.
- [x] Errors are stable and sanitized.
- [x] Lists/messages are bounded and pageable.
- [x] Authorization follows canonical Web conventions.
- [x] Real-host PostgreSQL tests cover the primary path.
- [x] OpenAPI matches behavior.
- [x] No UI/Razor change.
- [x] No broad test command was executed.

## Verdict

- [x] Pass — unlock SB10
- [ ] Reopen owning subbundle
- [ ] Stop bundle

## Evidence

- Focused CP2 command passed 3/3: real-host PostgreSQL HTTP lifecycle, complete database-transfer
  round trip, and immediately-previous-schema migration.
- CodeAnalytics snapshot `snap-20260814191734-4c429922`: four scoped projects, 467 types,
  3,193 members, 21 service registrations, zero cycles, zero diagnostics, zero blocking errors,
  and zero open questions.
- The two complexity warnings are non-blocking: the 452-line catalog API remains a transport-only
  adapter; the 494-line operation service is cohesive around one durable operation lifecycle and is
  already decomposed through repositories, evidence, conversation-engine, cancellation, and unit-of-work
  ports. Splitting either at CP2 would add indirection without changing a boundary.
