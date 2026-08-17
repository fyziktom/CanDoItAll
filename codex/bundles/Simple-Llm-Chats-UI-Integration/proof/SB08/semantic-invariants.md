# SB08 Semantic Invariants

- `SB08-INV-01` — Conversation creation offers only Active definitions and preserves the pinned definition revision returned by the application contract.
- `SB08-INV-02` — Conversation listing and transcript loading use bounded keyset pages; presentation never unboundedly materializes durable history.
- `SB08-INV-03` — Only canonical User and Assistant transcript entries are rendered; System messages remain absent.
- `SB08-INV-04` — Rename and archive mutations carry authoritative concurrency and transcript revision values.
- `SB08-INV-05` — One logical send owns one stable operation id across retry attempts.
- `SB08-INV-06` — Pending User presentation is added exactly once and only after successful operation admission.
- `SB08-INV-07` — Failures cross the UI boundary only as sanitized failures; unexpected exception logs expose identifiers, not message content.
- `SB08-INV-08` — LlmChats.Ui owns transient workspace state while durable conversation and transcript behavior remain application-owned.
- `SB08-INV-09` — The conversation workspace is one dominant bounded rail/transcript/composer surface built from shared wrappers.
- `SB08-INV-10` — No context, attachment, voice, tools, skills, Memory, route, navigation, streaming follower, or floating integration is activated in SB08.
