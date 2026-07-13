# Architecture Review Addendum Before Implementation Planning

The previous architecture package was directionally correct. This bundle adds the implementation-level details that were not yet strict enough for safe execution:

1. Introduce a strangler bridge before physical extraction. The current module is too large to move safely in one pass, so the generic provider boundary must first wrap the existing in-process memory implementation.
2. Split the generic memory foundation into four separate critical subbundles: protocol envelope, provider registry, operation/feedback/event ledgers, and source snapshot contracts.
3. Add mandatory checkpoint/refactoring subbundles after every major phase. Each checkpoint must search for helper bloat, duplicate mapping, hidden native references, cyclic dependencies, async misuse, overgrown files, and test-only behavior.
4. Treat the Memory Source Gateway as a separate foundation, not as an implementation detail of ingestion. It is the host boundary that prevents direct `AppDbContext` leakage into providers.
5. Define delayed feedback as a ledgered lifecycle from the start. Without it, context packs returned by providers cannot be linked to later process, customer, or economic outcomes.
6. Require a provider event inbox/outbox and loop guard before enabling proactive memory events. This prevents memory-agent-memory feedback loops.
7. Treat UI projection as a separate phase. Common UI, provider-specific RCL, and iframe/external provider UI have different validation needs.
8. Add final dependency-removal subbundles after native service migration. The migration is not done until composition, migrations, tests, and direct source references no longer force native Cognitive Memory into the main application.
