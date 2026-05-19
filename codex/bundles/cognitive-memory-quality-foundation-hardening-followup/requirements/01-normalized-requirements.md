# Normalized Requirements

| ID | Requirement | Priority |
|---|---|---|
| H-01 | Produce a re-entry audit with failing or pending regression tests for the hardening gaps before refactoring behavior. | Critical |
| H-02 | Split the monolithic quality implementation into focused files/classes without changing public contracts unnecessarily. | High |
| H-03 | Make cluster planning idempotent: repeated planning must return persisted cluster IDs, update or preserve keys/members predictably, and never hand downstream services transient IDs for existing hashes. | Critical |
| H-04 | Decide and implement the source-item substrate honestly: either support source-item cluster members with tests or narrow the public contract with an explicit documented exception. | Critical |
| H-05 | Make dream-run lifecycle transactional and failure-aware: failed runs must be marked `Failed` with actionable failure state, and partial data must not masquerade as completed work. | Critical |
| H-06 | Make `PersistChanges = false` a real dry-run/no-write contract, or remove/rename it if dry-run semantics are not supported. | Critical |
| H-07 | Replace broad default dream-mode behavior with explicit typed mode policies for every supported explicit mode and predictable rejection for unsupported modes. | Critical |
| H-08 | Improve aggregate candidate creation so aggregate text is cluster-level synthesis with grounded source mappings, not merely a per-record dump. | Critical |
| H-09 | Harden validation against contradictions, stale/superseded sources, generated-only evidence, restricted/redacted content, source-map gaps, and access-policy violations. | Critical |
| H-10 | Harden aggregate application idempotency, race behavior, source/evidence provenance writes, and no-duplicate apply semantics. | Critical |
| H-11 | Make recall synthesis produce concise grounded briefs with per-statement source refs, not just first-line selected context bullets. | Critical |
| H-12 | Harden reference-on-demand behavior so unauthorized references reveal no sensitive locator/summary content and return explicit exclusion reasons. | Critical |
| H-13 | Add diagnostics and logging with actionable state, masked sensitive data, and no silent fallback paths. | High |
| H-14 | Prove SQLite and PostgreSQL migration projects still build after any persistence changes. | Critical |
| H-15 | Build an end-to-end regression corpus covering duplicates, repeat runs, contradictions, temporal supersession, multi-project isolation, restricted/redacted content, generated-only inputs, unsupported modes, and dry runs. | Critical |
| H-16 | Update the original bundle closure or create a closure note so the prior "completed" claim is qualified by this follow-up outcome. | High |
