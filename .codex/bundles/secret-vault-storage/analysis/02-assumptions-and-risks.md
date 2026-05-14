# Assumptions And Risks

## Assumptions

- The first complete implementation optimizes for local Windows desktop users; DPAPI `CurrentUser` is the correct default.
- Existing database rows may contain DataProtection-protected payloads. This bundle may keep a migration bridge during read/update only if it is explicit and does not silently continue writing old-format values.
- Provider metadata can store vault keys or secret ids, but it must not store raw values.
- The existing workflow executor framework is the right place to resolve HTTP API-key references at execution time.

## Critical Path Risks

- Changing secret storage can orphan existing records if no compatibility/read-migration strategy is added.
- Process-wide environment promotion leaks resolved provider secrets longer than necessary and should be removed or narrowed.
- Workflow HTTP settings can accidentally preserve raw `Authorization` values if the UI still invites users to paste headers JSON without a safer selector.
- UI proof is required because dialogs, floating windows, and timed reveal/copy controls can easily clip or keep secret text visible longer than intended.

## Validation Risks

- DPAPI tests only pass on Windows; non-Windows CI needs explicit skip or unsupported-provider tests.
- Browser proof may be blocked by local database/profile setup; if blocked, record host proof and component/build proof instead of pretending UI was verified.
- Full-solution tests may exceed the turn budget; targeted tests must cover changed contracts and a final build must still run.

## Reopen Triggers

- Any raw secret appears in JSON settings, logs, activity records, workflow definitions, screenshots, or test output.
- A non-Windows provider silently falls back instead of throwing a provider-specific `NotSupportedException`.
- Agent or workflow runtime can resolve a stored secret without an explicit allowed-reference setting.
- The timed reveal remains visible past 30 seconds or survives component disposal/navigation.
- Project-structure secret references store values instead of id/name/purpose metadata.
