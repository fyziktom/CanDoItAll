# Assumptions And Risks

## Assumptions

- PostgreSQL and model-provider availability may vary by machine state; API validation should record exact status instead of pretending provider smoke ran when it did not.
- The prior LB4U validation evidence is useful but not a substitute for fresh smoke proof after code changes.
- Query-shape repairs should preserve public API contracts and persisted schema.

## Critical Path Risks

- If recall candidate activation still over-fetches, memory quality validation can look good on small test data while becoming noisy or slow on real LB4U data.
- If signal paging happens before recency and access filters, agents can miss the newest correction, risk, or calibration signals.
- If API validation cannot start the web app or reach a database profile, closure must record the blocker as environment proof, not pass the behavior silently.

## Validation Risks

- In-memory EF tests catch ordering semantics but not SQL shape. Use integration tests and code inspection for query-shape proof.
- Live API recall without a seeded source corpus proves endpoint health, not memory quality. Use a small truth-source corpus or existing snapshot data before assessing answer usefulness.
- Browser proof is not relevant unless UI files change; adding browser work here would be process noise.

## Reopen Triggers

- Any targeted Cognitive Memory unit or integration test fails.
- Live API status reports no usable database profile and no local fallback profile can be created safely.
- Recall returns context without source references or with noisy contact/secret-like content.
- Completed-stage bundle validation fails after execution report updates.
